using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;

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

        // GET /api/athlete-counts
        // Supports filtering by federation, year, and gender as separate
        // optional query params, on top of the mandatory scope filter — this
        // combination is exactly what a statistics page needs: "show me
        // female athlete counts for the Wrestling federation in 2025."
        [HttpGet]
        public async Task<IActionResult> List(
            Guid? federationId, int? year, string? gender, int page = 1, int pageSize = 20)
        {
            var querySet = _db.AthleteCounts
                .Include(a => a.Federation)
                .Include(a => a.City).ThenInclude(c => c.Province)
                .AsQueryable();

            if (federationId.HasValue) 
                querySet = querySet.Where(a => a.FederationId == federationId.Value);
            if (year.HasValue)
                querySet = querySet.Where(a => a.Year == year.Value);

            if (!string.IsNullOrEmpty(gender))
            {
                if (!Enum.TryParse<Gender>(gender, out var genderEnum))
                    return BadRequest(new { detail = "جنسیت نامعتبر است" });

                querySet = querySet.Where(a => a.Gender == genderEnum);
            }

            querySet = _scope.FilterAthleteCounts(querySet, User);

            var count = await querySet.CountAsync();
            var items = await querySet
                .OrderByDescending(a => a.Year).ThenByDescending(a => a.Month)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(a => new AthleteCountOutDto(
                    a.Id, a.FederationId, a.Federation.Name,
                    a.CityId, a.City.Name, a.City.Province.Name,
                    a.Year, a.Month, a.Gender.ToString(), a.Count))
                .ToListAsync();

            return Ok(new { items, count });
        }

        // GET /api/athlete-counts/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var a = await _db.AthleteCounts
                .Include(x => x.Federation)
                .Include(x => x.City).ThenInclude(c => c.Province)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (a == null) return NotFound(new { detail = "رکورد یافت نشد" });

            return Ok(new AthleteCountOutDto(
                a.Id, a.FederationId, a.Federation.Name,
                a.CityId, a.City.Name, a.City.Province.Name,
                a.Year, a.Month, a.Gender.ToString(), a.Count));
        }

        // POST /api/athlete-counts
        // Restricted to Admin, Manager, InsuranceOfficer — matches "Insurance
        // officer: can add/update/delete athletes counts or statistics" from
        // the role table.
        [HttpPost]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Create(AthleteCountCreateDto dto)
        {
            if (dto.Month < 1 || dto.Month > 12)
                return BadRequest(new { detail = "ماه باید بین ۱ تا ۱۲ باشد" });

            if (dto.Count < 0)
                return BadRequest(new { detail = "تعداد نمی‌تواند منفی باشد" });

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federationExists = await _db.Federations.AnyAsync(f => f.Id == dto.FederationId);
            if (!federationExists)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده معتبر نیست" });

            try { await _scope.CheckCityAllowedAsync(User, dto.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
            catch (KeyNotFoundException)
            { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }

            // Enforce the "one row per federation+city+year+month+gender"
            // rule at the application level too, not just relying on the
            // database's unique index — this lets us return a clear Persian
            // message pointing at the existing record, instead of the caller
            // getting a raw database constraint violation with no useful
            // explanation of what to do instead (i.e. "go edit that record").
            var existing = await _db.AthleteCounts.FirstOrDefaultAsync(a =>
                a.FederationId == dto.FederationId && a.CityId == dto.CityId &&
                a.Year == dto.Year && a.Month == dto.Month && a.Gender == genderEnum);

            if (existing != null)
                return Conflict(new
                {
                    detail = "آماری برای این فدراسیون/شهر/سال/ماه/جنسیت قبلاً ثبت شده است. لطفاً رکورد موجود را ویرایش کنید.",
                    existingId = existing.Id
                });

            var athleteCount = new AthleteCount
            {
                FederationId = dto.FederationId,
                CityId = dto.CityId,
                Year = dto.Year,
                Month = dto.Month,
                Gender = genderEnum,
                Count = dto.Count
            };

            _db.AthleteCounts.Add(athleteCount);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = athleteCount.Id }, new { id = athleteCount.Id });
        }

        // PUT /api/athlete-counts/{id}
        // Only accepts a new Count value, per the design decision in Step 2 —
        // the record's identity fields are immutable after creation.
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Update(Guid id, AthleteCountUpdateDto dto)
        {
            var athleteCount = await _db.AthleteCounts.FindAsync(id);
            if (athleteCount == null)
                return NotFound(new { detail = "رکورد یافت نشد" });

            if (dto.Count < 0)
                return BadRequest(new { detail = "تعداد نمی‌تواند منفی باشد" });

            // Scope check uses the record's EXISTING city, not a submitted
            // one — since CityId can't be changed via this endpoint, this
            // confirms the caller is actually allowed to touch this
            // particular record at all, not just that some city is valid.
            try { await _scope.CheckCityAllowedAsync(User, athleteCount.CityId); }
            catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }

            athleteCount.Count = dto.Count;
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        // DELETE /api/athlete-counts/{id}
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager,InsuranceOfficer")]
        public async Task<IActionResult> Delete(Guid id)
        {
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
