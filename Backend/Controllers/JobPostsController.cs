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
    [Route("api/job-posts")]
    [Authorize]
    public class JobPostsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public JobPostsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var posts = await _db.JobPosts
                .OrderBy(post => post.Title)
                .Select(post => new JobPostOutDto(
                    post.Id,
                    post.JobPostTypeId,
                    post.OrgUnitId,
                    post.TypeCode,
                    post.JobPostCode,
                    post.JobPostUniqueId,
                    post.Title,
                    post.ManagerType.ToString(),
                    post.BreakOut))
                .ToListAsync();
            return Ok(posts);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });
            return Ok(ToOutDto(post));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(JobPostUpsertDto dto)
        {
            if (!TryParseJobPost(
                    dto, out var typeCode, out var jobPostCode, out var jobPostUniqueId,
                    out var title, out var managerType, out var error))
                return BadRequest(new { detail = error });

            if (!await ReferencesExistAsync(dto.JobPostTypeId, dto.OrgUnitId))
                return BadRequest(new { detail = "نوع پست یا واحد سازمانی انتخاب‌شده معتبر نیست" });

            if (await _db.JobPosts.AnyAsync(post => post.Title == title))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });
            if (await _db.JobPosts.AnyAsync(post => post.JobPostCode == jobPostCode))
                return Conflict(new { detail = "شماره پست قبلا ثبت شده است" });
            if (await _db.JobPosts.AnyAsync(post => post.JobPostUniqueId == jobPostUniqueId))
                return Conflict(new { detail = "شناسه یکتا پست قبلا ثبت شده است" });

            var post = new JobPost
            {
                Id = Guid.NewGuid(),
                JobPostTypeId = dto.JobPostTypeId,
                OrgUnitId = dto.OrgUnitId,
                TypeCode = typeCode,
                JobPostCode = jobPostCode,
                JobPostUniqueId = jobPostUniqueId,
                Title = title,
                ManagerType = managerType,
                BreakOut = dto.BreakOut
            };
            _db.JobPosts.Add(post);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = post.Id }, ToOutDto(post));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, JobPostUpsertDto dto)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });

            if (!TryParseJobPost(
                    dto, out var typeCode, out var jobPostCode, out var jobPostUniqueId,
                    out var title, out var managerType, out var error))
                return BadRequest(new { detail = error });

            if (!await ReferencesExistAsync(dto.JobPostTypeId, dto.OrgUnitId))
                return BadRequest(new { detail = "نوع پست یا واحد سازمانی انتخاب‌شده معتبر نیست" });

            if (await _db.JobPosts.AnyAsync(candidate => candidate.Id != id && candidate.Title == title))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });
            if (await _db.JobPosts.AnyAsync(candidate =>
                    candidate.Id != id && candidate.JobPostCode == jobPostCode))
                return Conflict(new { detail = "شماره پست قبلا ثبت شده است" });
            if (await _db.JobPosts.AnyAsync(candidate =>
                    candidate.Id != id && candidate.JobPostUniqueId == jobPostUniqueId))
                return Conflict(new { detail = "شناسه یکتا پست قبلا ثبت شده است" });

            post.JobPostTypeId = dto.JobPostTypeId;
            post.OrgUnitId = dto.OrgUnitId;
            post.TypeCode = typeCode;
            post.JobPostCode = jobPostCode;
            post.JobPostUniqueId = jobPostUniqueId;
            post.Title = title;
            post.ManagerType = managerType;
            post.BreakOut = dto.BreakOut;
            await _db.SaveChangesAsync();

            return Ok(ToOutDto(post));
        }

        [HttpGet("import/template")]
        [Authorize(Roles = "Admin")]
        public IActionResult DownloadImportTemplate() =>
            File(
                JobPostImportWorkbook.CreateTemplate(),
                JobPostImportWorkbook.ContentType,
                "job-posts-import-template.xlsx");

        [HttpPost("import")]
        [Authorize(Roles = "Admin")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<JobPostImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = JobPostImportWorkbook.Read(stream);
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

            var jobPostTypes = await _db.JobPostTypes
                .Select(type => new { type.Id, type.Title })
                .ToDictionaryAsync(type => type.Title, type => type.Id, StringComparer.Ordinal);
            var orgUnits = await _db.OrgUnits
                .Select(unit => new { unit.Id, unit.Title })
                .ToDictionaryAsync(unit => unit.Title, unit => unit.Id, StringComparer.Ordinal);
            var knownTitles = new HashSet<string>(
                await _db.JobPosts.Select(post => post.Title).ToListAsync(),
                StringComparer.Ordinal);
            var knownJobPostCodes = new HashSet<string>(
                await _db.JobPosts.Select(post => post.JobPostCode).ToListAsync(),
                StringComparer.Ordinal);
            var knownJobPostUniqueIds = new HashSet<string>(
                await _db.JobPosts.Select(post => post.JobPostUniqueId).ToListAsync(),
                StringComparer.Ordinal);
            var errors = new List<JobPostImportErrorDto>();
            var created = 0;
            var skippedExisting = 0;

            foreach (var row in rows)
            {
                var title = row.Title?.Trim() ?? string.Empty;
                var typeCode = row.TypeCode?.Trim() ?? string.Empty;
                var jobPostCode = row.JobPostCode?.Trim() ?? string.Empty;
                var jobPostUniqueId = row.JobPostUniqueId?.Trim() ?? string.Empty;
                if (title.Length is 0 or > 160)
                {
                    errors.Add(new JobPostImportErrorDto(
                        row.RowNumber,
                        "عنوان پست الزامی است و باید حداکثر ۱۶۰ نویسه باشد."));
                    continue;
                }
                if (typeCode.Length == 0)
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "کد نوع پست الزامی است."));
                    continue;
                }
                if (jobPostCode.Length == 0)
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "شماره پست الزامی است."));
                    continue;
                }
                if (jobPostUniqueId.Length == 0)
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "شناسه یکتا پست الزامی است."));
                    continue;
                }
                if (!jobPostTypes.TryGetValue(row.JobPostTypeTitle?.Trim() ?? string.Empty, out var jobPostTypeId))
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "نوع پست با این عنوان یافت نشد."));
                    continue;
                }
                if (!orgUnits.TryGetValue(row.OrgUnitTitle?.Trim() ?? string.Empty, out var orgUnitId))
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "واحد سازمانی با این عنوان یافت نشد."));
                    continue;
                }
                if (!TryParseManagerType(row.ManagerType, out var managerType))
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "نوع مدیر حرفه‌ای معتبر نیست."));
                    continue;
                }
                if (!TryParseBreakOut(row.BreakOut, out var breakOut))
                {
                    errors.Add(new JobPostImportErrorDto(
                        row.RowNumber,
                        "شکسته‌شده باید یکی از مقادیر بله، خیر یا نامشخص باشد."));
                    continue;
                }
                if (knownTitles.Contains(title))
                {
                    skippedExisting++;
                    continue;
                }
                if (knownJobPostCodes.Contains(jobPostCode))
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "شماره پست تکراری است."));
                    continue;
                }
                if (knownJobPostUniqueIds.Contains(jobPostUniqueId))
                {
                    errors.Add(new JobPostImportErrorDto(row.RowNumber, "شناسه یکتا پست تکراری است."));
                    continue;
                }

                knownTitles.Add(title);
                knownJobPostCodes.Add(jobPostCode);
                knownJobPostUniqueIds.Add(jobPostUniqueId);
                _db.JobPosts.Add(new JobPost
                {
                    Id = Guid.NewGuid(),
                    JobPostTypeId = jobPostTypeId,
                    OrgUnitId = orgUnitId,
                    TypeCode = typeCode,
                    JobPostCode = jobPostCode,
                    JobPostUniqueId = jobPostUniqueId,
                    Title = title,
                    ManagerType = managerType,
                    BreakOut = breakOut
                });
                created++;
            }

            await _db.SaveChangesAsync();
            return Ok(new JobPostImportResultDto(created, skippedExisting, errors.Count, errors));
        }

        private async Task<bool> ReferencesExistAsync(Guid jobPostTypeId, Guid orgUnitId) =>
            await _db.JobPostTypes.AnyAsync(type => type.Id == jobPostTypeId) &&
            await _db.OrgUnits.AnyAsync(unit => unit.Id == orgUnitId);

        private static bool TryParseJobPost(
            JobPostUpsertDto dto,
            out string typeCode,
            out string jobPostCode,
            out string jobPostUniqueId,
            out string title,
            out JobPostManagerType managerType,
            out string error)
        {
            typeCode = dto.TypeCode?.Trim() ?? string.Empty;
            jobPostCode = dto.JobPostCode?.Trim() ?? string.Empty;
            jobPostUniqueId = dto.JobPostUniqueId?.Trim() ?? string.Empty;
            title = dto.Title?.Trim() ?? string.Empty;

            if (typeCode.Length == 0 || jobPostCode.Length == 0 || jobPostUniqueId.Length == 0)
            {
                managerType = default;
                error = "کد نوع پست، شماره پست و شناسه یکتا پست الزامی هستند";
                return false;
            }

            if (title.Length is 0 or > 160)
            {
                managerType = default;
                error = "عنوان پست الزامی است و باید حداکثر ۱۶۰ نویسه باشد";
                return false;
            }

            if (!Enum.TryParse(dto.ManagerType, out managerType) || !Enum.IsDefined(managerType))
            {
                error = "نوع مدیر حرفه‌ای معتبر نیست";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static bool TryParseManagerType(string? value, out JobPostManagerType managerType)
        {
            if (Enum.TryParse(value, true, out managerType) && Enum.IsDefined(managerType))
                return true;

            foreach (var field in typeof(JobPostManagerType).GetFields(
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
            {
                if (string.Equals(
                    field.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                        .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>()
                        .SingleOrDefault()?.GetName(),
                    value?.Trim(),
                    StringComparison.Ordinal))
                {
                    managerType = (JobPostManagerType)field.GetValue(null)!;
                    return true;
                }
            }

            managerType = default;
            return false;
        }

        private static bool TryParseBreakOut(string? value, out bool? breakOut)
        {
            var normalizedValue = value?.Trim();
            if (string.Equals(normalizedValue, "null", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedValue, "نامشخص", StringComparison.Ordinal))
            {
                breakOut = null;
                return true;
            }

            if (string.Equals(normalizedValue, "true", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedValue, "بله", StringComparison.Ordinal))
            {
                breakOut = true;
                return true;
            }

            if (string.Equals(normalizedValue, "false", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(normalizedValue, "خیر", StringComparison.Ordinal))
            {
                breakOut = false;
                return true;
            }

            breakOut = null;
            return false;
        }

        private static JobPostOutDto ToOutDto(JobPost post) =>
            new(
                post.Id,
                post.JobPostTypeId,
                post.OrgUnitId,
                post.TypeCode,
                post.JobPostCode,
                post.JobPostUniqueId,
                post.Title,
                post.ManagerType.ToString(),
                post.BreakOut);
    }
}
