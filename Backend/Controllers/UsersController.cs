using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // GET /api/users
        // Admin and Manager can both view the user list (Manager has visibility
        // into "all sections" per the role table — it just can't create/assign).
        [HttpGet]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> List()
        {
            var users = await _db.Users
            .Include(u => u.ScopeProvince)
            .Include(u => u.ScopeCity)
            .OrderBy(u => u.UserName)
            .Select(u => new UserOutDto(
            u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
            u.ScopeProvinceId, u.ScopeProvince != null ? u.ScopeProvince.Name : null,
            u.ScopeCityId, u.ScopeCity != null ? u.ScopeCity.Name : null))
            .ToListAsync();

            return Ok(users);
        }

        // GET /api/users/{id}
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Get(Guid id)
        {
            var u = await _db.Users
            .Include(x => x.ScopeProvince)
            .Include(x => x.ScopeCity)
            .FirstOrDefaultAsync(x => x.Id == id);

            if (u == null) return NotFound(new { detail = "کاربر یافت نشد" });

            return Ok(new UserOutDto(
            u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
            u.ScopeProvinceId, u.ScopeProvince?.Name, u.ScopeCityId, u.ScopeCity?.Name));
        }

        // POST /api/users
        // Admin only — this is explicitly called out in the role table
        // ("Manager ... can't create users"), so this is the one place in the
        // whole API where the role restriction is a single fixed value, not a list.
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(UserCreateDto dto)
        {
            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });

            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null)
                return BadRequest(new { detail = scopeError });

            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = $"{dto.Username}@local", // Identity requires an email; a placeholder domain is fine
                                                 // since this app authenticates by username, not email
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Role = roleEnum,
                ScopeLevel = scopeLevelEnum,
                ScopeProvinceId = scopeLevelEnum == ScopeLevel.National ? null : dto.ScopeProvinceId,
                ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                // Identity returns its own list of password/username validation errors
                // (too short, no digit, username taken, etc.) — surface them directly
                // rather than a generic failure message, since they're already
                // specific and actionable for whoever is filling out the form.
                var errors = string.Join(" / ", result.Errors.Select(e => e.Description));
                return BadRequest(new { detail = errors });
            }

            return CreatedAtAction(nameof(Get), new { id = user.Id },
            new UserOutDto(user.Id, user.UserName!, user.FirstName ?? "", user.LastName ?? "", user.Role.ToString(),
            user.ScopeLevel.ToString(), user.ScopeProvinceId, null, user.ScopeCityId, null));
        }

        // PUT /api/users/{id}
        // Also Admin only — updating someone's role or geographic scope is just
        // as sensitive as creating the account in the first place.
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, UserUpdateDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });

            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null)
                return BadRequest(new { detail = scopeError });

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Role = roleEnum;
            user.ScopeLevel = scopeLevelEnum;
            user.ScopeProvinceId = scopeLevelEnum == ScopeLevel.National ? null : dto.ScopeProvinceId;
            user.ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        // DELETE /api/users/{id}
        // Uses Identity's lockout mechanism instead of actually deleting the row.
        // Reasoning: this user may be referenced as the `sender` or a `recipient`
        // of existing Messages, or as the historical holder of a FederationPosition.
        // A hard delete would either fail (if any relation uses Restrict) or
        // silently erase "who sent this report" from historical records — neither
        // is acceptable for what's meant to be an audit-relevant system. Locking
        // the account out achieves the same practical goal ("this person can no
        // longer log in or act") without destroying history.
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            return Ok(new { success = true });
        }

        // POST /api/users/{id}/federation-position
        // Assigns a user as a federation's president/miss/vice. Admin only,
        // per the role table's explicit "Manager ... can't assign any
        // presidents/vices" restriction.
        //
        // Before inserting the new position row, this closes out whoever
        // currently holds that exact position (same federation, same position
        // type, EndDate still null) by setting their EndDate to the new
        // person's start date. This is what preserves full history — the old
        // holder's row stays in the table forever as a record of "who held
        // this position and for how long," rather than being overwritten.
        [HttpPost("{id}/federation-position")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignFederationPosition(Guid id, FederationPositionAssignDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null) return BadRequest(new { detail = "فدراسیون انتخاب‌شده معتبر نیست" });

            if (!Enum.TryParse<FederationPositionType>(dto.Position, out var positionEnum))
                return BadRequest(new { detail = "سمت انتخاب‌شده معتبر نیست" });

            var currentHolder = await _db.FederationPositions
            .FirstOrDefaultAsync(p => p.FederationId == dto.FederationId
            && p.Position == positionEnum
            && p.EndDate == null);

            if (currentHolder != null)
            {
                currentHolder.EndDate = dto.StartDate;
            }

            var newPosition = new FederationPosition
            {
                UserId = id,
                FederationId = dto.FederationId,
                Position = positionEnum,
                StartDate = dto.StartDate,
                EndDate = null
            };

            _db.FederationPositions.Add(newPosition);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, positionId = newPosition.Id });
        }

        // GET /api/users/{id}/federation-positions
        // Full history (past and current) for one specific user — open to
        // Admin/Manager since it's read-only reference/audit information.
        [HttpGet("{id}/federation-positions")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> GetFederationPositions(Guid id)
        {
            var positions = await _db.FederationPositions
            .Include(p => p.Federation)
            .Include(p => p.User)
            .Where(p => p.UserId == id)
            .OrderByDescending(p => p.StartDate)
            .Select(p => new FederationPositionOutDto(
            p.Id, p.Position.ToString(), p.StartDate, p.EndDate,
            p.UserId, p.User.FirstName ?? string.Empty, p.User.LastName ?? p.User.UserName!,
            p.FederationId, p.Federation.Name))
            .ToListAsync();

            return Ok(positions);
        }

        // Shared validation logic for Create and Update, so the "does this
        // scope combination make sense" rule only exists in one place.
        private async Task<string?> ValidateScopeAsync(ScopeLevel level, Guid? provinceId, Guid? cityId)
        {
            if (level == ScopeLevel.National)
            {
                // National scope shouldn't have a province/city attached at all —
                // not an error, just something the controller silently clears
                // when saving (see ScopeProvinceId/ScopeCityId assignment above),
                // so no validation needed here.
                return null;
            }

            if (level == ScopeLevel.Province)
            {
                if (provinceId == null)
                    return "برای دسترسی استانی، انتخاب استان الزامی است";

                var provinceExists = await _db.Provinces.AnyAsync(p => p.Id == provinceId);
                if (!provinceExists)
                    return "استان انتخاب‌شده معتبر نیست";

                return null;
            }

            if (level == ScopeLevel.City)
            {
                if (cityId == null)
                    return "برای دسترسی شهری، انتخاب شهر الزامی است";

                var city = await _db.Cities.FindAsync(cityId);
                if (city == null)
                    return "شهر انتخاب‌شده معتبر نیست";

                // Cross-check: if a provinceId was also submitted alongside the
                // city, it must actually match that city's real province —
                // otherwise the frontend could accidentally submit a mismatched
                // pair (picked a city, then province dropdown wasn't updated).
                if (provinceId != null && city.ProvinceId != provinceId)
                    return "استان و شهر انتخاب‌شده با یکدیگر همخوانی ندارند";

                return null;
            }

            return null;
        }
    }
}
