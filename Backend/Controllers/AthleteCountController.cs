using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.Packaging;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Extensions;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/athlete-counts")]
    [Authorize]
    public class AthleteCountsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ScopeService _scope;

        public AthleteCountsController(ApplicationDbContext db, ScopeService scope)
        {
            _db = db;
            _scope = scope;
        }

        // Helper methods for scope validation
        private bool IsAdmin() => User.Role() == "Admin";
        private bool IsManager() => User.Role() == "Manager";
        private bool IsInsuranceOfficer() => User.Role() == "InsuranceOfficer";
        private bool IsSportsManager() => User.Role() == "SportsManager";
        private bool IsSportsOfficer() => User.Role() == "SportsOfficer";

        private bool HasFullAccess() => IsAdmin() || IsManager() || IsInsuranceOfficer();
        private bool CanRead() => HasFullAccess() || IsSportsManager() || IsSportsOfficer();
        private bool CanCreate() => HasFullAccess();
        private bool CanUpdate() => HasFullAccess();
        private bool CanDelete() => HasFullAccess();

        [HttpGet("import/template")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public IActionResult DownloadImportTemplate()
        {
            return File(
                AthleteCountImportWorkbook.CreateTemplate(),
                AthleteCountImportWorkbook.ContentType,
                "athlete-counts-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (!CanCreate())
                return Forbid("شما اجازه ایجاد آمار ورزشکاران را ندارید");

            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<AthleteCountImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = AthleteCountImportWorkbook.Read(stream);
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

            var federations = await _db.Federations
                .Where(federation => federation.Level == FederationLevel.National)
                .Select(federation => new { federation.Id, federation.Name })
                .ToListAsync();
            var provinces = await _db.Provinces
                .Select(province => new { province.Id, province.Name })
                .ToListAsync();
            var cities = await _db.Cities
                .Select(city => new { city.Id, city.Name, city.ProvinceId })
                .ToListAsync();
            var existingRecords = (await _db.AthleteCounts
                    .Select(record => new
                    {
                        record.FederationId,
                        record.CityId,
                        record.Year,
                        record.Month
                    })
                    .ToListAsync())
                .Select(record => (
                    FederationId: record.FederationId,
                    CityId: record.CityId,
                    Year: record.Year,
                    Month: record.Month))
                .ToHashSet();
            var addedRecords = new List<AthleteCount>();
            var created = 0;
            var skippedInvalid = 0;
            var skippedOutOfScope = 0;
            var skippedExisting = 0;
            var errors = new List<AthleteCountImportRowError>();

            foreach (var row in rows)
            {
                if (row.FederationName == null || row.ProvinceName == null || row.CityName == null ||
                    row.Year == null || row.Month == null || row.MenCount == null || row.WomenCount == null ||
                    !AthleteCountImportWorkbook.TryParseInteger(row.Year, out var year) || year <= 0 ||
                    !AthleteCountImportWorkbook.TryParseMonth(row.Month, out var month) ||
                    !AthleteCountImportWorkbook.TryParseInteger(row.MenCount, out var menCount) || menCount < 0 ||
                    !AthleteCountImportWorkbook.TryParseInteger(row.WomenCount, out var womenCount) || womenCount < 0)
                {
                    skippedInvalid++;
                    errors.Add(new AthleteCountImportRowError(
                        row.RowNumber, "همه ستون‌ها باید مقدار معتبر داشته باشند؛ سال مثبت، ماه بین ۱ تا ۱۲ و تعدادها نامنفی باشند."));
                    continue;
                }

                var federationMatches = federations.Where(federation =>
                    federation.Name != null &&
                    AthleteCountImportWorkbook.NormalizeName(federation.Name) ==
                    AthleteCountImportWorkbook.NormalizeName(row.FederationName)).ToList();
                var provinceMatches = provinces.Where(province =>
                    AthleteCountImportWorkbook.NormalizeName(province.Name) ==
                    AthleteCountImportWorkbook.NormalizeName(row.ProvinceName)).ToList();

                if (federationMatches.Count != 1 || provinceMatches.Count != 1)
                {
                    skippedInvalid++;
                    errors.Add(new AthleteCountImportRowError(
                        row.RowNumber,
                        federationMatches.Count != 1
                            ? "نام فدراسیون کشوری معتبر نیست یا تکراری است."
                            : "نام استان معتبر نیست یا تکراری است."));
                    continue;
                }

                var cityMatches = cities.Where(city =>
                    city.ProvinceId == provinceMatches[0].Id &&
                    AthleteCountImportWorkbook.NormalizeName(city.Name) ==
                    AthleteCountImportWorkbook.NormalizeName(row.CityName)).ToList();
                if (cityMatches.Count != 1)
                {
                    skippedInvalid++;
                    errors.Add(new AthleteCountImportRowError(
                        row.RowNumber, "شهرستان با استان انتخاب‌شده مطابقت ندارد یا معتبر نیست."));
                    continue;
                }

                try
                {
                    await _scope.CheckCityAllowedAsync(User, cityMatches[0].Id);
                }
                catch (UnauthorizedAccessException)
                {
                    skippedOutOfScope++;
                    continue;
                }
                catch (KeyNotFoundException)
                {
                    skippedInvalid++;
                    errors.Add(new AthleteCountImportRowError(row.RowNumber, "شهرستان معتبر نیست."));
                    continue;
                }

                var key = (
                    FederationId: federationMatches[0].Id,
                    CityId: cityMatches[0].Id,
                    Year: year,
                    Month: month);
                if (!existingRecords.Add(key))
                {
                    skippedExisting++;
                    continue;
                }

                addedRecords.Add(new AthleteCount
                {
                    FederationId = key.FederationId,
                    CityId = cityMatches[0].Id,
                    Year = year,
                    Month = month,
                    MaleCount = menCount,
                    FemaleCount = womenCount
                });
                created++;
            }

            if (addedRecords.Count > 0)
            {
                _db.AthleteCounts.AddRange(addedRecords);
                await _db.SaveChangesAsync();
            }

            return Ok(new AthleteCountImportResultDto(
                created, skippedInvalid, skippedOutOfScope, skippedExisting, errors));
        }

        // Scope validation helper
        private bool IsInAllowedScope(string targetScopeLevel, Guid? targetProvinceId, Guid? targetCityId)
        {
            var myScope = User.ScopeLevel();

            if (myScope == "Mastery")
            {
                return true;
            }
            else if (myScope == "Province")
            {
                if (!targetProvinceId.HasValue) return false;
                return targetScopeLevel is "Province" or "City" && targetProvinceId == User.ScopeProvinceId();
            }
            else if (myScope == "City")
            {
                return targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> List(
            Guid? federationId, int? year, Guid? provinceId, Guid? cityId, int page = 1, int pageSize = 20)
        {
            if (!CanRead())
                return Forbid("شما اجازه مشاهده آمار ورزشکاران را ندارید");

            var qs = _db.AthleteCounts
                .Include(a => a.Federation)
                .Include(a => a.City).ThenInclude(c => c.Province)
                .AsQueryable();

            if (federationId.HasValue) qs = qs.Where(a => a.FederationId == federationId.Value);
            if (year.HasValue) qs = qs.Where(a => a.Year == year.Value);

            if (cityId.HasValue) qs = qs.Where(a => a.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(a => a.City.ProvinceId == provinceId.Value);
            
            // Apply scope filtering based on role
            if (HasFullAccess())
            {
                // Admin, Manager, InsuranceOfficer: use their scope service
                qs = _scope.FilterAthleteCounts(qs, User);
            }
            else
            {
                // SportsManager, SportsOfficer: read-only with scope restriction
                var myScope = User.ScopeLevel();
                var myProvinceId = User.ScopeProvinceId();
                var myCityId = User.ScopeCityId();

                if (myScope == "Mastery")
                {
                    // Can see all (no filter needed)
                }
                else if (myScope == "Province")
                {
                    qs = qs.Where(a => a.City.ProvinceId == myProvinceId);
                }
                else if (myScope == "City")
                {
                    qs = qs.Where(a => a.CityId == myCityId);
                }
            }

            var count = await qs.CountAsync();
            var items = await qs
                .OrderByDescending(a => a.Year).ThenByDescending(a => a.Month)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AthleteCountOutDto(
                    a.Id, a.FederationId, a.Federation.Name!,
                    a.CityId, a.City.Name, a.City.Province.Name,
                    a.Year, a.Month, a.MaleCount, a.FemaleCount))
                .ToListAsync();

            return Ok(new { items, count });
        }

        [HttpGet("summary")]
        public async Task<IActionResult> Summary(
            Guid? provinceId, Guid? cityId, int? year = null, int? month = null)
        {
            if (!CanRead())
                return Forbid("شما اجازه مشاهده آمار ورزشکاران را ندارید");

            if (month.HasValue && (!year.HasValue || month is < 1 or > 12))
                return BadRequest(new { detail = "برای فیلتر ماه، سال معتبر و ماه بین ۱ تا ۱۲ الزامی است" });

            var records = ApplySummaryScope(
                _db.AthleteCounts.AsQueryable(), provinceId, cityId);
            records = ApplySummaryPeriod(records, year, month);

            var totals = await records
                .GroupBy(record => record.FederationId)
                .Select(group => new
                {
                    FederationId = group.Key,
                    MaleCount = group.Sum(record => record.MaleCount),
                    FemaleCount = group.Sum(record => record.FemaleCount)
                })
                .ToListAsync();
            var totalsByFederation = totals.ToDictionary(total => total.FederationId);
            var recordIds = new Dictionary<Guid, Guid>();
            if (year.HasValue && month.HasValue && cityId.HasValue)
            {
                var matchedRecordIds = await records
                    .Select(record => new { record.FederationId, record.Id })
                    .ToListAsync();
                recordIds = matchedRecordIds.ToDictionary(record => record.FederationId, record => record.Id);
            }

            var federations = await _db.Federations
                .Where(federation => federation.Level == FederationLevel.National)
                .OrderBy(federation => federation.Name)
                .Select(federation => new { federation.Id, FederationName = federation.Name ?? "" })
                .ToListAsync();

            return Ok(federations.Select(federation =>
            {
                totalsByFederation.TryGetValue(federation.Id, out var total);
                var maleCount = total?.MaleCount ?? 0;
                var femaleCount = total?.FemaleCount ?? 0;
                recordIds.TryGetValue(federation.Id, out var recordId);
                return new AthleteCountFederationSummaryDto(
                    federation.Id, federation.FederationName,
                    recordId == Guid.Empty ? null : recordId,
                    maleCount, femaleCount, maleCount + femaleCount);
            }));
        }

        [HttpGet("summary/filters")]
        public async Task<IActionResult> SummaryFilters(Guid? provinceId, Guid? cityId, int? year)
        {
            if (!CanRead())
                return Forbid("شما اجازه مشاهده آمار ورزشکاران را ندارید");

            var records = ApplySummaryScope(
                _db.AthleteCounts.AsQueryable(), provinceId, cityId);
            var years = await records
                .Select(record => record.Year)
                .Distinct()
                .OrderByDescending(value => value)
                .ToArrayAsync();
            var months = year.HasValue
                ? await records
                    .Where(record => record.Year == year.Value)
                    .Select(record => record.Month)
                    .Distinct()
                    .OrderBy(value => value)
                    .ToArrayAsync()
                : [];

            return Ok(new AthleteCountSummaryFiltersDto(years, months));
        }

        private IQueryable<AthleteCount> ApplySummaryScope(
            IQueryable<AthleteCount> records, Guid? provinceId, Guid? cityId)
        {
            if (provinceId.HasValue)
                records = records.Where(record => record.City.ProvinceId == provinceId.Value);
            if (cityId.HasValue)
                records = records.Where(record => record.CityId == cityId.Value);

            if (HasFullAccess())
                return _scope.FilterAthleteCounts(records, User);

            var myScope = User.ScopeLevel();
            if (myScope == "Province")
                return records.Where(record => record.City.ProvinceId == User.ScopeProvinceId());
            if (myScope == "City")
                return records.Where(record => record.CityId == User.ScopeCityId());
            return myScope == "Mastery" ? records : records.Where(_ => false);
        }

        private static IQueryable<AthleteCount> ApplySummaryPeriod(
            IQueryable<AthleteCount> records, int? year, int? month)
        {
            if (year.HasValue)
            {
                records = records.Where(record => record.Year == year.Value);
                if (month.HasValue)
                    records = records.Where(record => record.Month == month.Value);
                return records;
            }

            var tehranToday = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"));
            var persianCalendar = new System.Globalization.PersianCalendar();
            var currentMonthIndex = persianCalendar.GetYear(tehranToday) * 12 +
                persianCalendar.GetMonth(tehranToday);
            var firstMonthIndex = currentMonthIndex - 11;
            return records.Where(record =>
                record.Year * 12 + record.Month >= firstMonthIndex &&
                record.Year * 12 + record.Month <= currentMonthIndex);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            if (!CanRead())
                return Forbid("شما اجازه مشاهده آمار ورزشکاران را ندارید");

            var a = await _db.AthleteCounts
                .Include(x => x.Federation)
                .Include(x => x.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound(new { detail = "رکورد یافت نشد" });

            if (!IsInAllowedScope("City", a.City.ProvinceId, a.CityId))
                return Forbid("شما اجازه مشاهده این رکورد را ندارید");

            return Ok(new AthleteCountOutDto(
                a.Id, a.FederationId, a.Federation.Name!,
                a.CityId, a.City.Name, a.City.Province.Name,
                a.Year, a.Month, a.MaleCount, a.FemaleCount));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Create(AthleteCountCreateDto dto)
        {
            if (!CanCreate())
                return Forbid("شما اجازه ایجاد آمار ورزشکاران را ندارید");

            if (dto.Month < 1 || dto.Month > 12)
                return BadRequest(new { detail = "ماه باید بین ۱ تا ۱۲ باشد" });
            if (dto.MaleCount < 0 || dto.FemaleCount < 0)
                return BadRequest(new { detail = "تعداد ورزشکاران نمی‌تواند منفی باشد" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده باید یک فدراسیون کشوری باشد" });

            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }

            var existing = await _db.AthleteCounts.FirstOrDefaultAsync(a =>
                a.FederationId == dto.FederationId && a.CityId == dto.CityId &&
                a.Year == dto.Year && a.Month == dto.Month);

            if (existing != null)
                return Conflict(new
                {
                    detail = "آماری برای این فدراسیون/شهر/سال/ماه قبلاً ثبت شده است. لطفاً رکورد موجود را ویرایش کنید.",
                    existingId = existing.Id
                });

            var athleteCount = new AthleteCount
            {
                FederationId = dto.FederationId,
                CityId = dto.CityId,
                Year = dto.Year,
                Month = dto.Month,
                MaleCount = dto.MaleCount,
                FemaleCount = dto.FemaleCount
            };

            _db.AthleteCounts.Add(athleteCount);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = athleteCount.Id }, new { id = athleteCount.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Update(Guid id, AthleteCountUpdateDto dto)
        {
            if (!CanUpdate())
                return Forbid("شما اجازه ویرایش آمار ورزشکاران را ندارید");

            var athleteCount = await _db.AthleteCounts.FindAsync(id);
            if (athleteCount == null)
                return NotFound(new { detail = "رکورد یافت نشد" });

            if (dto.MaleCount < 0 || dto.FemaleCount < 0)
                return BadRequest(new { detail = "تعداد ورزشکاران نمی‌تواند منفی باشد" });

            try { await _scope.CheckCityAllowedAsync(User, athleteCount.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

            athleteCount.MaleCount = dto.MaleCount;
            athleteCount.FemaleCount = dto.FemaleCount;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!CanDelete())
                return Forbid("شما اجازه حذف آمار ورزشکاران را ندارید");

            var athleteCount = await _db.AthleteCounts.FindAsync(id);
            if (athleteCount == null)
                return NotFound(new { detail = "رکورد یافت نشد" });

            try { await _scope.CheckCityAllowedAsync(User, athleteCount.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

            _db.AthleteCounts.Remove(athleteCount);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
