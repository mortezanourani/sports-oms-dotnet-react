using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/facilities")]
    [Authorize]
    public class FacilitiesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ScopeService _scope;

        public FacilitiesController(ApplicationDbContext db, ScopeService scope)
        {
            _db = db;
            _scope = scope;
        }

        [HttpGet]
        public async Task<IActionResult> List(string? facilityType, Guid? provinceId, Guid? cityId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Facilities
                .Include(f => f.City)
                    .ThenInclude(c => c.Province)
                .AsQueryable();
            
            if (!string.IsNullOrEmpty(facilityType))
            {
                if (!Enum.TryParse<FacilityType>(facilityType, out var facilityTypeEnum))
                    return BadRequest(new { detail = "نوع مکان نامعتبر است" });

                qs = qs.Where(f => f.FacilityType == facilityTypeEnum);
            }

            if (cityId.HasValue) qs = qs.Where(f => f.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(f => f.City.ProvinceId == provinceId.Value);
            
            qs = _scope.FilterFacilities(qs, User);

            var count = await qs.CountAsync();
            var items = await qs.Skip((page - 1) * pageSize).Take(pageSize)
            .Select(f => new FacilityOutDto(
                    f.Id, f.FacilityType.ToString(), f.Name, f.Address, f.SportType,
                    f.OwnerName, f.OwnerContact, f.CityId, f.City.ProvinceId, f.City.Name, f.City.Province.Name, f.CreatedAt))
            .ToListAsync();

            return Ok(new { items, count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var f = await _db.Facilities.Include(x => x.City).ThenInclude(c => c.Province)
            .FirstOrDefaultAsync(x => x.Id == id);
            if (f == null) return NotFound();

            return Ok(new FacilityOutDto(f.Id, f.FacilityType.ToString(), f.Name, f.Address, f.SportType,
            f.OwnerName, f.OwnerContact, f.CityId, f.City.ProvinceId, f.City.Name, f.City.Province.Name, f.CreatedAt));
        }

        [HttpPost]
        [Authorize(Roles = "Admin,Manager,M88Officer,M5Officer")]
        public async Task<IActionResult> Create(FacilityCreateDto dto)
        {
            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر یافت نشد" }); }

            var role = User.FindFirst("role")?.Value;
            if (role == "M88Officer" && dto.FacilityType != "Msy")
                return StatusCode(403, new { detail = "شما فقط مجاز به ثبت اماکن msy هستید" });
            if (role == "M5Officer" && dto.FacilityType is not ("Private" or "Gov"))
                return StatusCode(403, new { detail = "شما فقط مجاز به ثبت اماکن خصوصی/دولتی هستید" });

            if (!Enum.TryParse<FacilityType>(dto.FacilityType, out var facilityTypeEnum))
                return BadRequest(new { detail = "نوع مکان نامعتبر است" });

            var facility = new Facility
            {
                FacilityType = facilityTypeEnum,
                Name = dto.Name,
                Address = dto.Address,
                SportType = dto.SportType,
                OwnerName = dto.OwnerName,
                OwnerContact = dto.OwnerContact,
                CityId = dto.CityId
            };
            _db.Facilities.Add(facility);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = facility.Id }, new { id = facility.Id });
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager,M88Officer,M5Officer")]
        public async Task<IActionResult> Update(Guid id, FacilityUpdateDto dto)
        {
            var facility = await _db.Facilities.FindAsync(id);
            if (facility == null) return NotFound();

            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

            if (!Enum.TryParse<FacilityType>(dto.FacilityType, out var facilityTypeEnum))
                return BadRequest(new { detail = "نوع مکان نامعتبر است" });

            facility.FacilityType = facilityTypeEnum;
            facility.Name = dto.Name;
            facility.Address = dto.Address;
            facility.SportType = dto.SportType;
            facility.OwnerName = dto.OwnerName;
            facility.OwnerContact = dto.OwnerContact;
            facility.CityId = dto.CityId;
            facility.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { id = facility.Id });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var facility = await _db.Facilities.FindAsync(id);
            if (facility == null) return NotFound();
            _db.Facilities.Remove(facility);
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }
    }
}