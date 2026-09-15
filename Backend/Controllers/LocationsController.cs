using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/locations")]
    [Authorize]
    public class LocationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public LocationsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/locations/provinces
        // Open to any authenticated user — every role needs to read this list
        // to populate dropdowns, so no extra [Authorize(Roles=...)] restriction here.
        [HttpGet("provinces")]
        public async Task<IActionResult> ListProvinces()
        {
            var provinces = await _db.Provinces
            .OrderBy(p => p.Name)
            .Select(p => new ProvinceOutDto(p.Id, p.Name))
            .ToListAsync();

            return Ok(provinces);
        }

        // POST /api/locations/provinces
        // Restricted: reference data should only be edited by Admin or Manager,
        // per the role table — regular officers only consume this data, never edit it.
        [HttpPost("provinces")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> CreateProvince(ProvinceCreateDto dto)
        {
            // Guard against duplicate province names before hitting the database,
            // giving a clear Persian error instead of a raw SQL constraint failure.
            var exists = await _db.Provinces.AnyAsync(p => p.Name == dto.Name);
            if (exists)
                return Conflict(new { detail = "استانی با این نام قبلاً ثبت شده است" });

            var province = new Province { Name = dto.Name };
            _db.Provinces.Add(province);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(ListProvinces), new ProvinceOutDto(province.Id, province.Name));
        }

        // PUT /api/locations/provinces/{id}
        [HttpPut("provinces/{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UpdateProvince(Guid id, ProvinceCreateDto dto)
        {
            var province = await _db.Provinces.FindAsync(id);
            if (province == null)
                return NotFound(new { detail = "استان یافت نشد" });

            province.Name = dto.Name;
            await _db.SaveChangesAsync();

            return Ok(new ProvinceOutDto(province.Id, province.Name));
        }

        // DELETE /api/locations/provinces/{id}
        // Note: this will fail at the database level if any City still references
        // this province, because ApplicationDbContext configures that relationship
        // with DeleteBehavior.Restrict. That's intentional — it stops an Admin from
        // accidentally deleting a province that still has cities (and therefore
        // facilities, athlete counts, etc.) attached to it.
        [HttpDelete("provinces/{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteProvince(Guid id)
        {
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
                // The database rejected the delete because of the Restrict rule —
                // translate that into a clear message instead of a raw 500 error.
                return Conflict(new { detail = "این استان دارای شهرهای ثبت‌شده است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }

        // GET /api/locations/cities?provinceId=...
        // provinceId is optional — omitting it returns every city in the country,
        // which is useful for an admin-level "all cities" list view, while passing
        // it filters to just that province's cities, which is what a cascading
        // Province -> City dropdown on the frontend will call.
        [HttpGet("cities")]
        public async Task<IActionResult> ListCities(Guid? provinceId)
        {
            var query = _db.Cities.Include(c => c.Province).AsQueryable();

            if (provinceId.HasValue)
                query = query.Where(c => c.ProvinceId == provinceId.Value);

            var cities = await query
            .OrderBy(c => c.Name)
            .Select(c => new CityOutDto(c.Id, c.Name, c.ProvinceId, c.Province.Name))
            .ToListAsync();

            return Ok(cities);
        }

        // POST /api/locations/cities
        [HttpPost("cities")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> CreateCity(CityCreateDto dto)
        {
            var province = await _db.Provinces.FindAsync(dto.ProvinceId);
            if (province == null)
                return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });

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

        // PUT /api/locations/cities/{id}
        [HttpPut("cities/{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> UpdateCity(Guid id, CityCreateDto dto)
        {
            var city = await _db.Cities.FindAsync(id);
            if (city == null)
                return NotFound(new { detail = "شهر یافت نشد" });

            var province = await _db.Provinces.FindAsync(dto.ProvinceId);
            if (province == null)
                return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });

            city.ProvinceId = dto.ProvinceId;
            city.Name = dto.Name;
            await _db.SaveChangesAsync();

            return Ok(new CityOutDto(city.Id, city.Name, city.ProvinceId, province.Name));
        }

        // DELETE /api/locations/cities/{id}
        // Same Restrict-based protection as DeleteProvince: if any Facility or
        // AthleteCount references this city, the delete fails at the database
        // level and we translate that into a readable message.
        [HttpDelete("cities/{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> DeleteCity(Guid id)
        {
            var city = await _db.Cities.FindAsync(id);
            if (city == null)
                return NotFound(new { detail = "شهر یافت نشد" });

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
