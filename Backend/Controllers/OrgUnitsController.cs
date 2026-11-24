using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/org-units")]
    [Authorize(Roles = "Admin")]
    public class OrgUnitsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public OrgUnitsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var units = await _db.OrgUnits
                .OrderBy(unit => unit.Title)
                .Select(unit => new OrgUnitOutDto(
                    unit.Id,
                    unit.Title,
                    unit.Type.ToString(),
                    unit.Scope.ToString(),
                    unit.ParentOrgUnitId,
                    unit.ParentOrgUnit == null ? null : unit.ParentOrgUnit.Title))
                .ToListAsync();
            return Ok(units);
        }

        [HttpPost]
        public async Task<IActionResult> Create(OrgUnitUpsertDto dto)
        {
            if (!TryParse(dto, out var title, out var type, out var scope, out var error))
                return BadRequest(new { detail = error });

            if (await _db.OrgUnits.AnyAsync(unit => unit.Title == title))
                return Conflict(new { detail = "واحد سازمانی با این عنوان قبلا ثبت شده است" });
            if (dto.ParentOrgUnitId.HasValue &&
                !await _db.OrgUnits.AnyAsync(unit => unit.Id == dto.ParentOrgUnitId.Value))
                return BadRequest(new { detail = "واحد سازمانی والد معتبر نیست" });

            var unit = new OrgUnit
            {
                Id = Guid.NewGuid(),
                Title = title,
                Type = type,
                Scope = scope,
                ParentOrgUnitId = dto.ParentOrgUnitId
            };
            _db.OrgUnits.Add(unit);
            await _db.SaveChangesAsync();
            var parentTitle = dto.ParentOrgUnitId.HasValue
                ? await _db.OrgUnits
                    .Where(candidate => candidate.Id == dto.ParentOrgUnitId.Value)
                    .Select(candidate => candidate.Title)
                    .SingleOrDefaultAsync()
                : null;
            return CreatedAtAction(nameof(List), ToOutDto(unit, parentTitle));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, OrgUnitUpsertDto dto)
        {
            var unit = await _db.OrgUnits.FindAsync(id);
            if (unit == null)
                return NotFound(new { detail = "واحد سازمانی یافت نشد" });

            if (!TryParse(dto, out var title, out var type, out var scope, out var error))
                return BadRequest(new { detail = error });
            if (await _db.OrgUnits.AnyAsync(candidate => candidate.Id != id && candidate.Title == title))
                return Conflict(new { detail = "واحد سازمانی با این عنوان قبلا ثبت شده است" });
            if (dto.ParentOrgUnitId.HasValue)
            {
                if (!await _db.OrgUnits.AnyAsync(candidate => candidate.Id == dto.ParentOrgUnitId.Value))
                    return BadRequest(new { detail = "واحد سازمانی والد معتبر نیست" });
                if (await WouldCreateCycleAsync(id, dto.ParentOrgUnitId.Value))
                    return BadRequest(new { detail = "واحد والد نمی‌تواند خود واحد یا یکی از زیرمجموعه‌های آن باشد" });
            }

            unit.Title = title;
            unit.Type = type;
            unit.Scope = scope;
            unit.ParentOrgUnitId = dto.ParentOrgUnitId;
            await _db.SaveChangesAsync();

            var parentTitle = dto.ParentOrgUnitId.HasValue
                ? await _db.OrgUnits
                    .Where(candidate => candidate.Id == dto.ParentOrgUnitId.Value)
                    .Select(candidate => candidate.Title)
                    .SingleOrDefaultAsync()
                : null;
            return Ok(ToOutDto(unit, parentTitle));
        }

        [HttpGet("import/template")]
        public IActionResult DownloadImportTemplate() =>
            File(
                OrgUnitImportWorkbook.CreateTemplate(),
                OrgUnitImportWorkbook.ContentType,
                "org-units-import-template.xlsx");

        [HttpPost("import")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<OrgUnitImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = OrgUnitImportWorkbook.Read(stream);
            }
            catch (InvalidDataException ex)
            {
                return BadRequest(new { detail = ex.Message });
            }
            catch (ArgumentException)
            {
                return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
            }
            catch (IOException)
            {
                return BadRequest(new { detail = "خواندن فایل اکسل با خطا مواجه شد" });
            }
            catch (OpenXmlPackageException)
            {
                return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
            }

            var existingUnits = await _db.OrgUnits
                .Select(unit => new { unit.Id, unit.Title })
                .ToListAsync();
            var existingByTitle = existingUnits.ToDictionary(
                unit => NormalizeTitle(unit.Title),
                unit => unit.Id,
                StringComparer.Ordinal);
            var candidates = new Dictionary<string, ImportCandidate>(StringComparer.Ordinal);
            var errors = new List<OrgUnitImportErrorDto>();
            var skippedExisting = 0;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Title))
                {
                    errors.Add(new OrgUnitImportErrorDto(row.RowNumber, "عنوان واحد سازمانی الزامی است."));
                    continue;
                }

                var title = NormalizeTitle(row.Title);
                if (existingByTitle.ContainsKey(title) || candidates.ContainsKey(title))
                {
                    skippedExisting++;
                    continue;
                }

                if (!TryParseEnum(row.Type, out OrgUnitType type))
                {
                    errors.Add(new OrgUnitImportErrorDto(row.RowNumber, "نوع واحد سازمانی معتبر نیست."));
                    continue;
                }

                if (!TryParseEnum(row.Scope, out OrgUnitScope scope))
                {
                    errors.Add(new OrgUnitImportErrorDto(row.RowNumber, "دامنه واحد سازمانی معتبر نیست."));
                    continue;
                }

                candidates.Add(title, new ImportCandidate(
                    row.RowNumber,
                    title,
                    type,
                    scope,
                    NormalizeTitle(row.ParentTitle)));
            }

            foreach (var candidate in candidates.Values)
            {
                if (candidate.ParentTitle.Length == 0)
                    continue;
                if (existingByTitle.TryGetValue(candidate.ParentTitle, out var existingParentId))
                {
                    candidate.ParentId = existingParentId;
                    continue;
                }
                if (!candidates.TryGetValue(candidate.ParentTitle, out var parent))
                {
                    candidate.Error = "واحد سازمانی والد با این عنوان یافت نشد.";
                    continue;
                }

                candidate.ParentCandidate = parent;
            }

            var states = new Dictionary<ImportCandidate, int>();
            var stack = new List<ImportCandidate>();
            foreach (var candidate in candidates.Values)
                ResolveParents(candidate, states, stack);

            foreach (var candidate in candidates.Values)
            {
                if (candidate.Error != null)
                {
                    errors.Add(new OrgUnitImportErrorDto(candidate.RowNumber, candidate.Error));
                    continue;
                }

                _db.OrgUnits.Add(new OrgUnit
                {
                    Id = candidate.Id,
                    Title = candidate.Title,
                    Type = candidate.Type,
                    Scope = candidate.Scope,
                    ParentOrgUnitId = candidate.ParentId ?? candidate.ParentCandidate?.Id
                });
            }

            var created = candidates.Values.Count(candidate => candidate.Error == null);
            await _db.SaveChangesAsync();
            return Ok(new OrgUnitImportResultDto(created, skippedExisting, errors.Count, errors));
        }

        private async Task<bool> WouldCreateCycleAsync(Guid unitId, Guid parentId)
        {
            var parents = await _db.OrgUnits
                .Select(unit => new { unit.Id, unit.ParentOrgUnitId })
                .ToDictionaryAsync(unit => unit.Id, unit => unit.ParentOrgUnitId);
            var visited = new HashSet<Guid>();
            var currentId = (Guid?)parentId;

            while (currentId.HasValue)
            {
                if (currentId.Value == unitId || !visited.Add(currentId.Value))
                    return true;
                if (!parents.TryGetValue(currentId.Value, out currentId))
                    return true;
            }

            return false;
        }

        private static bool ResolveParents(
            ImportCandidate candidate,
            IDictionary<ImportCandidate, int> states,
            IList<ImportCandidate> stack)
        {
            if (candidate.Error != null)
                return false;
            if (states.TryGetValue(candidate, out var state))
            {
                if (state == 2)
                    return candidate.Error == null;

                var cycleStart = stack.IndexOf(candidate);
                for (var index = cycleStart; index < stack.Count; index++)
                    stack[index].Error ??= "واحدهای سازمانی نمی‌توانند چرخه والد ایجاد کنند.";
                return false;
            }

            states[candidate] = 1;
            stack.Add(candidate);
            if (candidate.ParentCandidate != null &&
                !ResolveParents(candidate.ParentCandidate, states, stack) &&
                candidate.Error == null)
                candidate.Error = "واحد سازمانی والد معتبر نیست.";

            stack.RemoveAt(stack.Count - 1);
            states[candidate] = 2;
            return candidate.Error == null;
        }

        private static bool TryParse(
            OrgUnitUpsertDto dto,
            out string title,
            out OrgUnitType type,
            out OrgUnitScope scope,
            out string error)
        {
            title = NormalizeTitle(dto.Title);
            if (title.Length == 0)
            {
                type = default;
                scope = default;
                error = "عنوان واحد سازمانی الزامی است";
                return false;
            }
            if (!TryParseEnum(dto.Type, out type))
            {
                scope = default;
                error = "نوع واحد سازمانی معتبر نیست";
                return false;
            }
            if (!TryParseEnum(dto.Scope, out scope))
            {
                error = "دامنه واحد سازمانی معتبر نیست";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryParseEnum<TEnum>(string? value, out TEnum result)
            where TEnum : struct, Enum
        {
            var normalizedValue = value?.Trim();
            foreach (var name in Enum.GetNames<TEnum>())
            {
                if (string.Equals(name, normalizedValue, StringComparison.OrdinalIgnoreCase))
                {
                    result = Enum.Parse<TEnum>(name);
                    return true;
                }
            }

            foreach (var field in typeof(TEnum).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (string.Equals(
                    field.GetCustomAttribute<DisplayAttribute>()?.GetName(),
                    normalizedValue,
                    StringComparison.Ordinal))
                {
                    result = (TEnum)field.GetValue(null)!;
                    return true;
                }
            }

            result = default;
            return false;
        }

        private static string NormalizeTitle(string? title) => title?.Trim() ?? string.Empty;

        private static OrgUnitOutDto ToOutDto(OrgUnit unit, string? parentTitle) =>
            new(unit.Id, unit.Title, unit.Type.ToString(), unit.Scope.ToString(), unit.ParentOrgUnitId, parentTitle);

        private sealed class ImportCandidate(
            int rowNumber,
            string title,
            OrgUnitType type,
            OrgUnitScope scope,
            string parentTitle)
        {
            public Guid Id { get; } = Guid.NewGuid();
            public int RowNumber { get; } = rowNumber;
            public string Title { get; } = title;
            public OrgUnitType Type { get; } = type;
            public OrgUnitScope Scope { get; } = scope;
            public string ParentTitle { get; } = parentTitle;
            public Guid? ParentId { get; set; }
            public ImportCandidate? ParentCandidate { get; set; }
            public string? Error { get; set; }
        }
    }
}
