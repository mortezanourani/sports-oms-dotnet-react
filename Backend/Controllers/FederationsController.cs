using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/federations")]
    [Authorize]
    public class FederationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public FederationsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/federations
        // Open to any authenticated user, no role restriction. Federations are
        // referenced everywhere (Champions, AthleteCount, User federation
        // positions), so every role needs to be able to read this list to
        // populate dropdowns — restricting read access here would break forms
        // for roles that have nothing to do with managing federations directly.
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var federations = await _db.Federations
            .OrderBy(f => f.Name)
            .Select(f => new FederationOutDto(f.Id, f.Name, f.SportType))
            .ToListAsync();

            return Ok(federations);
        }

        // GET /api/federations/{id}
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null)
                return NotFound(new { detail = "فدراسیون یافت نشد" });

            return Ok(new FederationOutDto(federation.Id, federation.Name, federation.SportType));
        }

        // POST /api/federations
        // Restricted to Admin/Manager, matching the role table's statement that
        // only these two roles touch federation structure itself — the actual
        // day-to-day sport-specific data (champions, medals, athlete counts)
        // belongs to the specialized officer roles, but the federation *record*
        // itself is closer to reference/organizational data, same category as
        // Province/City.
        [HttpPost]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Create(FederationCreateDto dto)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Name == dto.Name);
            if (exists)
                return Conflict(new { detail = "فدراسیونی با این نام قبلاً ثبت شده است" });

            var federation = new Federation { Name = dto.Name, SportType = dto.SportType };
            _db.Federations.Add(federation);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = federation.Id },
            new FederationOutDto(federation.Id, federation.Name, federation.SportType));
        }

        // PUT /api/federations/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Update(Guid id, FederationUpdateDto dto)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null)
                return NotFound(new { detail = "فدراسیون یافت نشد" });

            federation.Name = dto.Name;
            federation.SportType = dto.SportType;
            await _db.SaveChangesAsync();

            return Ok(new FederationOutDto(federation.Id, federation.Name, federation.SportType));
        }

        // DELETE /api/federations/{id}
        // Champion.FederationId uses DeleteBehavior.Restrict, so the database
        // itself will refuse this delete if any Champion still references the
        // federation. AthleteCount and FederationPosition also carry a
        // FederationId, and although those two weren't explicitly given a
        // Restrict rule in ApplicationDbContext, EF Core's *default* delete
        // behavior for a required (non-nullable) foreign key is Cascade —
        // meaning without an explicit override, deleting a Federation would
        // silently delete every AthleteCount and FederationPosition row tied
        // to it too. That's almost certainly not what you want for
        // organizational/historical data, so this needs a fix before it ships.
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null)
                return NotFound(new { detail = "فدراسیون یافت نشد" });

            try
            {
                _db.Federations.Remove(federation);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این فدراسیون دارای اطلاعات وابسته است و قابل حذف نیست" });

            }

            return Ok(new { success = true });
        }

        // GET /api/federations/{id}/positions
        // Full leadership history for one federation — every past and current
        // President/Miss/Vice, reusing the same FederationPositionOutDto shape
        // UsersController already returns, just queried from the opposite
        // direction (by FederationId instead of UserId).
        [HttpGet("{id}/positions")]
        public async Task<IActionResult> GetPositions(Guid id)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Id == id);
            if (!exists) return NotFound(new { detail = "فدراسیون یافت نشد" });

            var positions = await _db.FederationPositions
                .Include(p => p.User)
                .Include(p => p.Federation)
                .Where(p => p.FederationId == id)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new FederationPositionOutDto(
                    p.Id, p.Position.ToString(), p.StartDate, p.EndDate,
                    p.UserId, p.User.FirstName!, p.User.LastName!,
                    p.FederationId, p.Federation.Name))
                .ToListAsync();

            return Ok(positions);
        }
    }
}
