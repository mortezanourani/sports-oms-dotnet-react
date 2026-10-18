using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.Packaging;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/champions")]
    [Authorize]
    public class ChampionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ScopeService _scope;
        private const string ChampionImportRoles = "Admin,Manager,SportsManager,SportsOfficer";

        public ChampionsController(ApplicationDbContext db, ScopeService scope)
        {
            _db = db;
            _scope = scope;
        }

        [HttpGet("import/template")]
        [Authorize(Roles = ChampionImportRoles)]
        public IActionResult DownloadImportTemplate()
        {
            return File(
                ChampionImportWorkbook.CreateTemplate(),
                ChampionImportWorkbook.ContentType,
                "champions-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = ChampionImportRoles)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (!HasChampionPermission())
                return Forbid("شما اجازه وارد کردن قهرمانان را ندارید");
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<ChampionImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = ChampionImportWorkbook.Read(stream);
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

            var provinces = await _db.Provinces.ToListAsync();
            var cities = await _db.Cities.ToListAsync();
            var federations = await _db.Federations
                .Where(federation => federation.Level == FederationLevel.National && federation.Name != null)
                .ToListAsync();
            var existingChampions = await _db.Champions
                .Select(champion => new
                {
                    champion.Id,
                    champion.FullName,
                    champion.Gender,
                    champion.FederationId,
                    champion.CityId
                })
                .ToListAsync();
            var championIdsByKey = existingChampions
                .GroupBy(champion => (
                    Name: ChampionImportWorkbook.NormalizeName(champion.FullName),
                    champion.Gender,
                    champion.FederationId,
                    champion.CityId))
                .ToDictionary(group => group.Key, group => group.First().Id);
            var medalKeys = (await _db.Medals
                    .Select(medal => new
                    {
                        medal.ChampionId,
                        medal.MedalType,
                        medal.AgeGroup,
                        medal.Level,
                        medal.CompetitionName,
                        medal.Year,
                        medal.Field
                    })
                    .ToListAsync())
                .Select(medal => (
                    medal.ChampionId,
                    medal.MedalType,
                    medal.AgeGroup,
                    medal.Level,
                    Competition: ChampionImportWorkbook.NormalizeName(medal.CompetitionName),
                    medal.Year,
                    Field: medal.Field == null ? string.Empty : ChampionImportWorkbook.NormalizeName(medal.Field)))
                .ToHashSet();

            var createdChampions = 0;
            var existingChampionRows = 0;
            var createdMedals = 0;
            var skippedExistingMedals = 0;
            var skippedInvalid = 0;
            var skippedOutOfScope = 0;
            var errors = new List<ChampionImportRowError>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.FullName) ||
                    string.IsNullOrWhiteSpace(row.FederationName) ||
                    !ChampionImportWorkbook.TryParseGender(row.Gender, out var gender))
                {
                    skippedInvalid++;
                    errors.Add(new ChampionImportRowError(
                        row.RowNumber, "نام کامل، جنسیت معتبر و نام فدراسیون الزامی است."));
                    continue;
                }

                Province? province = null;
                City? city = null;
                if (row.CityName != null && row.ProvinceName == null)
                {
                    skippedInvalid++;
                    errors.Add(new ChampionImportRowError(row.RowNumber, "برای تعیین شهرستان، نام استان نیز الزامی است."));
                    continue;
                }

                if (row.ProvinceName != null)
                {
                    var provinceKey = ChampionImportWorkbook.NormalizeName(row.ProvinceName);
                    var provinceMatches = provinces.Where(item =>
                        ChampionImportWorkbook.NormalizeName(item.Name) == provinceKey).ToList();
                    if (provinceMatches.Count != 1)
                    {
                        skippedInvalid++;
                        errors.Add(new ChampionImportRowError(row.RowNumber, "نام استان معتبر نیست یا تکراری است."));
                        continue;
                    }

                    province = provinceMatches[0];
                    if (row.CityName == null)
                    {
                        skippedInvalid++;
                        errors.Add(new ChampionImportRowError(
                            row.RowNumber, "ثبت قهرمان در سطح استان پشتیبانی نمی‌شود؛ نام شهرستان الزامی است."));
                        continue;
                    }
                }

                if (row.CityName != null)
                {
                    var cityKey = ChampionImportWorkbook.NormalizeName(row.CityName);
                    var cityMatches = cities.Where(item =>
                        item.ProvinceId == province!.Id &&
                        ChampionImportWorkbook.NormalizeName(item.Name) == cityKey).ToList();
                    if (cityMatches.Count != 1)
                    {
                        skippedInvalid++;
                        errors.Add(new ChampionImportRowError(
                            row.RowNumber, "نام شهرستان با استان مطابقت ندارد یا معتبر نیست."));
                        continue;
                    }

                    city = cityMatches[0];
                }

                if (!CanImportChampionAtScope(province?.Id, city?.Id))
                {
                    skippedOutOfScope++;
                    continue;
                }

                var federationName = ChampionImportWorkbook.NormalizeName(row.FederationName);
                var federationMatches = federations.Where(item =>
                    ChampionImportWorkbook.NormalizeName(item.Name!) == federationName).ToList();
                if (federationMatches.Count != 1)
                {
                    skippedInvalid++;
                    errors.Add(new ChampionImportRowError(
                        row.RowNumber, "نام فدراسیون کشوری معتبر نیست یا تکراری است."));
                    continue;
                }

                var federation = federationMatches[0];
                var championKey = (
                    Name: ChampionImportWorkbook.NormalizeName(row.FullName),
                    Gender: gender,
                    FederationId: federation.Id,
                    CityId: city?.Id);
                var championExists = championIdsByKey.TryGetValue(championKey, out var existingChampionId);
                var champion = championExists ? null : new Champion
                {
                    FullName = row.FullName.Trim(),
                    Gender = gender,
                    BirthDate = ChampionImportWorkbook.TryParseSolarDate(row.BirthDate, out var birthDate)
                        ? TehranDateTime.ToUtc(birthDate)
                        : null,
                    FederationId = federation.Id,
                    CityId = city?.Id
                };

                if (championExists)
                    existingChampionRows++;

                Medal? medal = null;
                string? medalValidationError = null;
                if (string.IsNullOrWhiteSpace(row.MedalType) ||
                    string.IsNullOrWhiteSpace(row.AgeGroup) ||
                    string.IsNullOrWhiteSpace(row.TournamentLevel) ||
                    string.IsNullOrWhiteSpace(row.Year) ||
                    string.IsNullOrWhiteSpace(row.Count) ||
                    string.IsNullOrWhiteSpace(row.CompetitionName) ||
                    !ChampionImportWorkbook.TryParseMedalType(row.MedalType, out var medalType) ||
                    !ChampionImportWorkbook.TryParseAgeGroup(row.AgeGroup, out var ageGroup) ||
                    !ChampionImportWorkbook.TryParseTournamentLevel(row.TournamentLevel, out var level) ||
                    !ChampionImportWorkbook.TryParseInteger(row.Year, out var year) || year <= 0 ||
                    !ChampionImportWorkbook.TryParseInteger(row.Count, out var count) || count < 1)
                {
                    medalValidationError = "اطلاعات الزامی مدال ناقص یا نامعتبر است؛ اطلاعات معتبر قهرمان ثبت شد.";
                }
                else if (championExists)
                {
                    var medalKey = (
                        ChampionId: existingChampionId,
                        MedalType: medalType,
                        AgeGroup: ageGroup,
                        Level: level,
                        Competition: ChampionImportWorkbook.NormalizeName(row.CompetitionName),
                        Year: year,
                        Field: row.Field == null ? string.Empty : ChampionImportWorkbook.NormalizeName(row.Field));
                    if (medalKeys.Contains(medalKey))
                    {
                        skippedExistingMedals++;
                        continue;
                    }
                    medal = new Medal
                    {
                        ChampionId = medalKey.ChampionId,
                        MedalType = medalType,
                        AgeGroup = ageGroup,
                        Level = level,
                        CompetitionName = row.CompetitionName.Trim(),
                        Year = year,
                        Count = count,
                        Field = string.IsNullOrWhiteSpace(row.Field) ? null : row.Field.Trim()
                    };
                }
                else
                {
                    medal = new Medal
                    {
                        Champion = champion!,
                        MedalType = medalType,
                        AgeGroup = ageGroup,
                        Level = level,
                        CompetitionName = row.CompetitionName.Trim(),
                        Year = year,
                        Count = count,
                        Field = string.IsNullOrWhiteSpace(row.Field) ? null : row.Field.Trim()
                    };
                }

                if (champion != null)
                    _db.Champions.Add(champion);
                if (medal != null)
                    _db.Medals.Add(medal);
                if (champion != null || medal != null)
                {
                    await _db.SaveChangesAsync();
                    if (champion != null)
                    {
                        championIdsByKey.Add(championKey, champion.Id);
                        createdChampions++;
                    }
                    if (medal != null)
                    {
                        var championId = champion?.Id ?? medal.ChampionId;
                        medalKeys.Add((
                            ChampionId: championId,
                            medal.MedalType,
                            medal.AgeGroup,
                            medal.Level,
                            Competition: ChampionImportWorkbook.NormalizeName(medal.CompetitionName),
                            medal.Year,
                            Field: medal.Field == null ? string.Empty : ChampionImportWorkbook.NormalizeName(medal.Field)));
                        createdMedals++;
                    }
                }

                if (medalValidationError != null)
                {
                    skippedInvalid++;
                    errors.Add(new ChampionImportRowError(row.RowNumber, medalValidationError));
                }
            }

            return Ok(new ChampionImportResultDto(
                createdChampions,
                existingChampionRows,
                createdMedals,
                skippedExistingMedals,
                skippedInvalid,
                skippedOutOfScope,
                errors));
        }

        private bool CanImportChampionAtScope(Guid? provinceId, Guid? cityId)
        {
            var scopeLevel = User.ScopeLevel();
            if (scopeLevel == "Mastery")
                return true;
            if (scopeLevel == "Province")
                return provinceId.HasValue && cityId.HasValue &&
                       User.ScopeProvinceId() == provinceId;
            if (scopeLevel == "City")
                return cityId.HasValue && User.ScopeCityId() == cityId;
            return false;
        }

        // Helper methods for permission checks
        private bool IsAdminOrManager() => User.Role() is "Admin" or "Manager" or "SportsManager";
        private bool IsSportsOfficer() => User.Role() == "SportsOfficer";

        private bool HasChampionPermission() => User.Role() is "Admin" or "Manager" or "SportsManager" or "SportsOfficer";
        private bool HasChampionDeletePermission() => User.Role() is "Admin" or "Manager" or "SportsManager";

        private bool CanAccessChampion(Champion champion)
        {
            var role = User.Role();
            var myScopeLevel = User.ScopeLevel();

            // Admin, Manager, SportsManager have full access based on scope
            if (role is "Admin" or "Manager" or "SportsManager")
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    // Champion can be in province or city within province
                    return champion.Federation.ProvinceId == myProvinceId ||
                           (champion.City != null && champion.City.ProvinceId == myProvinceId);
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    // Champion can be in this city
                    return champion.CityId == myCityId;
                }
            }

            // SportsOfficer has same permissions as above
            if (role == "SportsOfficer")
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    return champion.Federation.ProvinceId == myProvinceId ||
                           (champion.City != null && champion.City.ProvinceId == myProvinceId);
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    return champion.CityId == myCityId;
                }
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid? federationId, Guid? provinceId, Guid? cityId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .AsQueryable();

            if (federationId.HasValue)
                qs = qs.Where(c => c.FederationId == federationId.Value);

            if (cityId.HasValue) qs = qs.Where(c => c.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(c => c.City != null && c.City.ProvinceId == provinceId.Value);

            // Apply scope filtering based on role
            var role = User.Role();
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();

            if (role is "Admin" or "Manager" or "SportsManager" or "SportsOfficer")
            {
                if (myScopeLevel == "Province")
                {
                    qs = qs.Where(c =>
                        (c.Federation.Level == FederationLevel.Provincial && c.Federation.ProvinceId == myProvinceId) ||
                        (c.City != null && c.City.ProvinceId == myProvinceId));
                }
                else if (myScopeLevel == "City")
                {
                    qs = qs.Where(c => c.CityId == myCityId);
                }
                // Mastery has no filter (can see all)
            }

            var count = await qs.CountAsync();
            var items = await qs
                .OrderBy(c => c.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ChampionListOutDto(
                    c.Id, c.FullName, c.BirthDate, c.Gender.ToString(),
                    c.FederationId, c.Federation.Name!, // National federations always have a Name
                    c.CityId, c.City != null ? c.City.Name : null,
                    c.Medals.Sum(m => m.Count))) // sums actual medal Count, not row count
                .ToListAsync();

            return Ok(new { items, count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه مشاهده این قهرمان را ندارید");

            return Ok(new ChampionDetailOutDto(
                champion.Id, champion.FullName, champion.BirthDate, champion.Gender.ToString(),
                champion.FederationId, champion.Federation.Name!,
                champion.CityId, champion.City?.ProvinceId, champion.City?.Name, champion.City?.Province?.Name,
                champion.PhotoPath, champion.Medals
                    .OrderByDescending(m => m.Year)
                    .Select(m => new MedalOutDto(
                        m.Id, m.MedalType.ToString(), m.AgeGroup.ToString(), m.Level.ToString(),
                        m.CompetitionName, m.Year, m.Count, m.Field))
                    .ToList()));
        }

        [HttpPost]
        public async Task<IActionResult> Create(ChampionCreateDto dto)
        {
            if (!HasChampionPermission())
                return Forbid("شما اجازه ایجاد قهرمان را ندارید");

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده باید یک فدراسیون کشوری باشد" });

            // Check if user has permission for this federation's scope
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();

            if (myScopeLevel == "Province" && federation.ProvinceId != myProvinceId)
                return Forbid("شما اجازه ایجاد قهرمان برای این فدراسیون را ندارید");

            if (dto.CityId.HasValue)
            {
                try { await _scope.CheckCityAllowedAsync(User, dto.CityId.Value); }
                catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
                catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }
            }

            var champion = new Champion
            {
                FullName = dto.FullName,
                BirthDate = dto.BirthDate.HasValue ? TehranDateTime.NormalizeUtc(dto.BirthDate.Value) : null,
                Gender = genderEnum,
                FederationId = dto.FederationId,
                CityId = dto.CityId,
                PhotoPath = dto.PhotoPath
            };

            _db.Champions.Add(champion);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = champion.Id }, new { id = champion.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, ChampionUpdateDto dto)
        {
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه ویرایش این قهرمان را ندارید");

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فadorasیون انتخاب‌شده باید یک فadorasیون کشوری باشد" });

            // Verify user can access the new federation scope
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();

            if (myScopeLevel == "Province" && federation.ProvinceId != myProvinceId)
                return Forbid("شما اجازه ویرایش قهرمان برای این فadorasیون را ندارید");

            if (dto.CityId.HasValue)
            {
                try { await _scope.CheckCityAllowedAsync(User, dto.CityId.Value); }
                catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
                catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }
            }

            champion.FullName = dto.FullName;
            champion.BirthDate = dto.BirthDate.HasValue ? TehranDateTime.NormalizeUtc(dto.BirthDate.Value) : null;
            champion.Gender = genderEnum;
            champion.FederationId = dto.FederationId;
            champion.CityId = dto.CityId;
            champion.PhotoPath = dto.PhotoPath;
            champion.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { id = champion.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه حذف این قهرمان را ندارید");

            if (!HasChampionDeletePermission())
                return Forbid("شما اجازه حذف قهرمان را ندارید");

            _db.Champions.Remove(champion); // cascades to Medals (default EF behavior, required FK)
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("{championId}/medals")]
        public async Task<IActionResult> AddMedal(Guid championId, MedalCreateDto dto)
        {
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.Id == championId);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه افزودن مدال به این قهرمان را ندارید");

            if (!Enum.TryParse<MedalType>(dto.MedalType, out var medalTypeEnum))
                return BadRequest(new { detail = "نوع مدال نامعتبر است" });
            if (!Enum.TryParse<AgeGroup>(dto.AgeGroup, out var ageGroupEnum))
                return BadRequest(new { detail = "گروه سنی نامعتبر است" });
            if (!Enum.TryParse<TournamentLevel>(dto.Level, out var levelEnum))
                return BadRequest(new { detail = "سطح مسابقه نامعتبر است" });
            if (dto.Count < 1)
                return BadRequest(new { detail = "تعداد مدال باید حداقل ۱ باشد" });

            var medal = new Medal
            {
                ChampionId = championId,
                MedalType = medalTypeEnum,
                AgeGroup = ageGroupEnum,
                Level = levelEnum,
                CompetitionName = dto.CompetitionName,
                Year = dto.Year,
                Count = dto.Count,
                Field = dto.Field
            };

            _db.Medals.Add(medal);
            await _db.SaveChangesAsync();

            return Ok(new MedalOutDto(medal.Id, medal.MedalType.ToString(), medal.AgeGroup.ToString(),
                medal.Level.ToString(), medal.CompetitionName, medal.Year, medal.Count, medal.Field));
        }

        [HttpDelete("{championId}/medals/{medalId}")]
        public async Task<IActionResult> DeleteMedal(Guid championId, Guid medalId)
        {
            var medal = await _db.Medals.FirstOrDefaultAsync(m => m.Id == medalId && m.ChampionId == championId);
            if (medal == null)
                return NotFound(new { detail = "مدال یافت نشد" });

            // Get champion to check access
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .FirstOrDefaultAsync(c => c.Id == championId);
            if (champion == null || !CanAccessChampion(champion))
                return Forbid("شما اجازه حذف این مدال را ندارید");

            if (!HasChampionDeletePermission())
                return Forbid("شما اجازه حذف مدال را ندارید");

            _db.Medals.Remove(medal);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
