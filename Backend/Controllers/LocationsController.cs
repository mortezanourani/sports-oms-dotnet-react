using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DocumentFormat.OpenXml.Packaging;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/locations")]
    [Authorize]
    public class LocationsController : ControllerBase
    {
        private const string MasteryOrProvinceAdminRoles = "Admin";
        private readonly ApplicationDbContext _db;

        public LocationsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet("import/template")]
        [Authorize(Roles = MasteryOrProvinceAdminRoles)]
        public IActionResult DownloadImportTemplate()
        {
            if (IsCityScope())
                return Forbid("شما اجازه مدیریت مکان‌ها را ندارید");

            return File(
                LocationImportWorkbook.CreateTemplate(),
                LocationImportWorkbook.ContentType,
                "locations-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = MasteryOrProvinceAdminRoles)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (IsCityScope())
                return Forbid("شما اجازه مدیریت مکان‌ها را ندارید");

            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<LocationImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = LocationImportWorkbook.Read(stream);
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

            var provinceEntities = await _db.Provinces.ToListAsync();
            var provincesByName = provinceEntities
                .GroupBy(province => LocationImportWorkbook.NormalizeName(province.Name), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase);
            var cityKeys = (await _db.Cities
                    .Select(city => new { city.ProvinceId, city.Name })
                    .ToListAsync())
                .Select(city => (city.ProvinceId, Name: LocationImportWorkbook.NormalizeName(city.Name)))
                .ToHashSet();
            var createdProvinces = 0;
            var createdCities = 0;
            var skippedExisting = 0;
            var skippedMissingProvince = 0;
            var skippedInvalidProvince = 0;
            var errors = new List<LocationImportRowError>();

            // Import province rows first so a city row can refer to a province
            // even when it appears later in the workbook.
            foreach (var row in rows.Where(row => row.CityName == null))
            {
                if (string.IsNullOrWhiteSpace(row.ProvinceName))
                {
                    skippedMissingProvince++;
                    continue;
                }

                var provinceName = row.ProvinceName.Trim();
                var key = LocationImportWorkbook.NormalizeName(provinceName);
                if (provincesByName.ContainsKey(key))
                {
                    skippedExisting++;
                    continue;
                }

                if (IsProvinceScope())
                {
                    errors.Add(new LocationImportRowError(row.RowNumber, "در سطح استانی امکان ایجاد استان جدید وجود ندارد."));
                    continue;
                }

                var province = new Province { Name = provinceName };
                _db.Provinces.Add(province);
                try
                {
                    await _db.SaveChangesAsync();
                    provincesByName.Add(key, province);
                    createdProvinces++;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(province).State = EntityState.Detached;
                    var existingProvince = await _db.Provinces
                        .FirstOrDefaultAsync(item => item.Name == provinceName);
                    if (existingProvince != null)
                    {
                        provincesByName[key] = existingProvince;
                        skippedExisting++;
                    }
                    else
                    {
                        errors.Add(new LocationImportRowError(row.RowNumber, "ذخیره استان با خطا مواجه شد."));
                    }
                }
            }

            foreach (var row in rows.Where(row => row.CityName != null))
            {
                if (string.IsNullOrWhiteSpace(row.ProvinceName))
                {
                    skippedMissingProvince++;
                    continue;
                }

                var provinceKey = LocationImportWorkbook.NormalizeName(row.ProvinceName);
                if (!provincesByName.TryGetValue(provinceKey, out var province))
                {
                    skippedInvalidProvince++;
                    continue;
                }

                if (IsProvinceScope() && province.Id != User.ScopeProvinceId())
                {
                    errors.Add(new LocationImportRowError(row.RowNumber, "شما اجازه ایجاد شهر در این استان را ندارید."));
                    continue;
                }

                var cityName = row.CityName!.Trim();
                var cityKey = LocationImportWorkbook.NormalizeName(cityName);
                if (cityKeys.Contains((province.Id, cityKey)))
                {
                    skippedExisting++;
                    continue;
                }

                var city = new City { ProvinceId = province.Id, Name = cityName };
                _db.Cities.Add(city);
                try
                {
                    await _db.SaveChangesAsync();
                    cityKeys.Add((province.Id, cityKey));
                    createdCities++;
                }
                catch (DbUpdateException)
                {
                    _db.Entry(city).State = EntityState.Detached;
                    var duplicate = await _db.Cities.AnyAsync(item => item.ProvinceId == province.Id &&
                        item.Name == cityName);
                    if (duplicate)
                        skippedExisting++;
                    else
                        errors.Add(new LocationImportRowError(row.RowNumber, "ذخیره شهرستان با خطا مواجه شد."));
                }
            }

            return Ok(new LocationImportResultDto(
                createdProvinces,
                createdCities,
                skippedExisting,
                skippedMissingProvince,
                skippedInvalidProvince,
                errors.Count,
                errors));
        }

        // Helper methods
        private bool IsAdmin() => User.Role() == "Admin";
        private bool IsMasteryScope() => User.ScopeLevel() == "Mastery";
        private bool IsProvinceScope() => User.ScopeLevel() == "Province";
        private bool IsCityScope() => User.ScopeLevel() == "City";

        private Guid? MyProvinceId() => IsProvinceScope() || IsCityScope() ? User.ScopeProvinceId() : null;
        private Guid? MyCityId() => IsCityScope() ? User.ScopeCityId() : null;

        // GET /api/locations/provinces
        [HttpGet("provinces")]
        public async Task<IActionResult> ListProvinces()
        {
            var query = _db.Provinces.AsQueryable();

            // Scope filtering based on role
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();

            if (myScope == "Province")
            {
                // Province scope: can only see their own province
                query = query.Where(p => p.Id == myProvinceId);
            }
            else if (myScope == "City")
            {
                // City scope: can only see their own province
                query = query.Where(p => p.Id == myProvinceId);
            }

            var provinces = await query
                .OrderBy(p => p.Name)
                .Select(p => new ProvinceOutDto(p.Id, p.Name))
                .ToListAsync();

            return Ok(provinces);
        }

        // POST /api/locations/provinces - Admin only, Mastery scope only
        [HttpPost("provinces")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateProvince(ProvinceCreateDto dto)
        {
            // Only Mastery Admin can create provinces
            if (!IsMasteryScope())
                return Forbid("شما اجازه ایجاد استان ندارید");

            var exists = await _db.Provinces.AnyAsync(p => p.Name == dto.Name);
            if (exists)
                return Conflict(new { detail = "استانی با این نام قبلاً ثبت شده است" });

            var province = new Province { Name = dto.Name };
            _db.Provinces.Add(province);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(ListProvinces), new ProvinceOutDto(province.Id, province.Name));
        }

        // PUT /api/locations/provinces/{id} - Admin only, Mastery scope only
        [HttpPut("provinces/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateProvince(Guid id, ProvinceCreateDto dto)
        {
            // Only Mastery Admin can update provinces
            if (!IsMasteryScope())
                return Forbid("شما اجازه ویرایش استان‌ها را ندارید");

            var province = await _db.Provinces.FindAsync(id);
            if (province == null)
                return NotFound(new { detail = "استان یافت نشد" });

            province.Name = dto.Name;
            await _db.SaveChangesAsync();

            return Ok(new ProvinceOutDto(province.Id, province.Name));
        }

        // DELETE /api/locations/provinces/{id} - Admin only, Mastery scope only
        [HttpDelete("provinces/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteProvince(Guid id)
        {
            // Only Mastery Admin can delete provinces
            if (!IsMasteryScope())
                return Forbid("شما اجازه حذف استان‌ها را ندارید");

            var province = await _db.Provinces.FindAsync(id);
            if (province == null)
                return NotFound(new { detail = "استان یافت نشد" });

            try
            {
                _db.Provinces.Remove(province);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این استان دارای شهرهای ثبت‌شده است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }

        // GET /api/locations/cities?provinceId=...
        [HttpGet("cities")]
        public async Task<IActionResult> ListCities(Guid? provinceId)
        {
            var query = _db.Cities.Include(c => c.Province).AsQueryable();

            // Scope filtering based on role
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();

            if (myScope == "Province")
            {
                // Province scope: can see all cities in their province
                query = query.Where(c => c.ProvinceId == myProvinceId);
            }
            else if (myScope == "City")
            {
                // City scope: can see only their own city
                query = query.Where(c => c.Id == myCityId);
            }

            if (provinceId.HasValue)
                query = query.Where(c => c.ProvinceId == provinceId.Value);

            var cities = await query
                .OrderBy(c => c.Name)
                .Select(c => new CityOutDto(c.Id, c.Name, c.ProvinceId, c.Province.Name))
                .ToListAsync();

            return Ok(cities);
        }

        // POST /api/locations/cities - Admin only, Province Admin + Mastery Admin
        [HttpPost("cities")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> CreateCity(CityCreateDto dto)
        {
            var province = await _db.Provinces.FindAsync(dto.ProvinceId);
            if (province == null)
                return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });

            // Only Province Admin or Mastery Admin can create cities
            // City Admin cannot create cities
            if (IsCityScope())
                return Forbid("شما اجازه ایجاد شهر ندارید");

            if (IsProvinceScope() && dto.ProvinceId != User.ScopeProvinceId())
                return Forbid("شما اجازه ایجاد شهر در این استان را ندارید");

            var exists = await _db.Cities
                .AnyAsync(c => c.ProvinceId == dto.ProvinceId && c.Name == dto.Name);
            if (exists)
                return Conflict(new { detail = "این شهر قبلاً در این استان ثبت شده است" });

            var city = new City { ProvinceId = dto.ProvinceId, Name = dto.Name };
            _db.Cities.Add(city);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(ListCities),
                new CityOutDto(city.Id, city.Name, city.ProvinceId, province.Name));
        }

        // PUT /api/locations/cities/{id} - Admin only, Province Admin + Mastery Admin
        [HttpPut("cities/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCity(Guid id, CityCreateDto dto)
        {
            var city = await _db.Cities.FindAsync(id);
            if (city == null)
                return NotFound(new { detail = "شهر یافت نشد" });

            var province = await _db.Provinces.FindAsync(dto.ProvinceId);
            if (province == null)
                return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });

            // Only Province Admin or Mastery Admin can update cities
            // City Admin cannot update cities
            if (IsCityScope())
                return Forbid("شما اجازه ویرایش شهرها را ندارید");

            // Province Admin can only update cities in their province
            if (IsProvinceScope())
            {
                var myProvinceId = User.ScopeProvinceId();
                if (city.ProvinceId != myProvinceId || province.Id != myProvinceId)
                    return Forbid("شما اجازه ویرایش این شهر را ندارید");
            }

            city.ProvinceId = dto.ProvinceId;
            city.Name = dto.Name;
            await _db.SaveChangesAsync();

            return Ok(new CityOutDto(city.Id, city.Name, city.ProvinceId, province.Name));
        }

        // DELETE /api/locations/cities/{id} - Admin only, Province Admin + Mastery Admin
        [HttpDelete("cities/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCity(Guid id)
        {
            var city = await _db.Cities.FindAsync(id);
            if (city == null)
                return NotFound(new { detail = "شهر یافت نشد" });

            // Only Province Admin or Mastery Admin can delete cities
            // City Admin cannot delete cities
            if (IsCityScope())
                return Forbid("شما اجازه حذف شهرها را ندارید");

            // Province Admin can only delete cities in their province
            if (IsProvinceScope())
            {
                var myProvinceId = User.ScopeProvinceId();
                if (city.ProvinceId != myProvinceId)
                    return Forbid("شما اجازه حذف این شهر را ندارید");
            }

            try
            {
                _db.Cities.Remove(city);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این شهر دارای اطلاعات وابسته است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }
    }
}
