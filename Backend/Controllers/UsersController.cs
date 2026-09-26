using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin,Manager")] // role gate stays the same; scope gate is method-body logic below
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // Admin and Manager are now fully symmetric for user management —
        // the only remaining difference between them lives elsewhere
        // (federation position assignment stays Admin-only, unchanged
        // below). This single method is the real authorization boundary
        // for create/edit/deactivate, replacing the old flat "Admin only"
        // rule with a scope-aware one.
        private bool CanManageTargetScope(string targetScopeLevel, Guid? targetProvinceId, Guid? targetCityId)
        {
            var myScope = User.ScopeLevel();

            if (myScope == "Mastery") return true;

            if (myScope == "Province")
            {
                if (targetScopeLevel == "Mastery") return false;
                // Both Province- and City-scoped target users carry their
                // own ScopeProvinceId directly (set at creation time), so
                // no extra City lookup is needed here to find "which
                // province does this city belong to" — it's already on
                // the record.
                return targetProvinceId == User.ScopeProvinceId();
            }

            if (myScope == "City")
            {
                return targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
            }

            return false;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // GET /api/users?scopeLevel=&provinceId=&cityId=
        // scopeLevel/provinceId/cityId here drive the three dashboard tabs
        // (Mastery/Province/City). Independent of those params, every
        // result is ALSO filtered to what the caller's own scope permits —
        // the tab filters are a UI convenience, the scope filter below is
        // the actual security boundary, applied regardless of what the
        // client asks for.
        [HttpGet]
        public async Task<IActionResult> List(string? scopeLevel, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Users.Include(u => u.ScopeProvince).Include(u => u.ScopeCity).AsQueryable();

            if (!string.IsNullOrEmpty(scopeLevel))
            {
                if (!Enum.TryParse<ScopeLevel>(scopeLevel, out var scopeLevelEnum))
                    return BadRequest(new { detail = "??? ?????? ??????? ???" });
                qs = qs.Where(u => u.ScopeLevel == scopeLevelEnum);
            }

            if (cityId.HasValue) qs = qs.Where(u => u.ScopeCityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(u => u.ScopeProvinceId == provinceId.Value);

            var myScope = User.ScopeLevel();
            if (myScope == "Province")
            {
                var myProvinceId = User.ScopeProvinceId();
                qs = qs.Where(u => u.ScopeLevel != ScopeLevel.Mastery && u.ScopeProvinceId == myProvinceId);
            }
            else if (myScope == "City")
            {
                var myCityId = User.ScopeCityId();
                qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
            }
            // Mastery scope: no additional restriction beyond the explicit query params above.

            var users = await qs
                .OrderBy(u => u.UserName)
                .Select(u => new UserOutDto(
                    u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
                    u.ScopeProvinceId, u.ScopeProvince != null ? u.ScopeProvince.Name : null,
                    u.ScopeCityId, u.ScopeCity != null ? u.ScopeCity.Name : null))
                .ToListAsync();

            return Ok(users);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var u = await _db.Users.Include(x => x.ScopeProvince).Include(x => x.ScopeCity)
                .FirstOrDefaultAsync(x => x.Id == id);
            if (u == null) return NotFound(new { detail = "????? ???? ???" });

            if (!CanManageTargetScope(u.ScopeLevel.ToString(), u.ScopeProvinceId, u.ScopeCityId))
                return Forbid("??? ????? ?????? ??? ????? ?? ??????");

            return Ok(new UserOutDto(
                u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
                u.ScopeProvinceId, u.ScopeProvince?.Name, u.ScopeCityId, u.ScopeCity?.Name));
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateDto dto)
        {
            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "??? ?????????? ????? ????" });
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "??? ?????? ????????? ????? ????" });

            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId))
                return Forbid("??? ????? ????? ????? ?? ??? ??? ?????? ?? ??????");

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null) return BadRequest(new { detail = scopeError });

            var user = new ApplicationUser
            {
                UserName = dto.Username,
                Email = $"{dto.Username}@local",
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Role = roleEnum,
                ScopeLevel = scopeLevelEnum,
                ScopeProvinceId = scopeLevelEnum == ScopeLevel.Mastery ? null : dto.ScopeProvinceId,
                ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(" / ", result.Errors.Select(e => e.Description));
                return BadRequest(new { detail = errors });
            }

            return CreatedAtAction(nameof(Get), new { id = user.Id },
                new UserOutDto(user.Id, user.UserName!, user.FirstName ?? "", user.LastName ?? "", user.Role.ToString(),
                    user.ScopeLevel.ToString(), user.ScopeProvinceId, null, user.ScopeCityId, null));
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, UserUpdateDto dto)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { detail = "????? ???? ???" });

            // Checked against the user's CURRENT scope first — a caller
            // must already be allowed to manage this person before
            // touching them at all.
            if (!CanManageTargetScope(user.ScopeLevel.ToString(), user.ScopeProvinceId, user.ScopeCityId))
                return Forbid("??? ????? ?????? ??? ????? ?? ??????");

            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "??? ?????????? ????? ????" });
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "??? ?????? ????????? ????? ????" });

            // ALSO checked against the proposed NEW scope — otherwise a
            // Province admin could edit someone already within their
            // jurisdiction and "promote" them to National scope, escaping
            // the province admin's own authority entirely.
            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId))
                return Forbid("??? ????? ?????? ??? ????? ?? ??? ??? ?????? ?? ??????");

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null) return BadRequest(new { detail = scopeError });

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Role = roleEnum;
            user.ScopeLevel = scopeLevelEnum;
            user.ScopeProvinceId = scopeLevelEnum == ScopeLevel.Mastery ? null : dto.ScopeProvinceId;
            user.ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound(new { detail = "????? ???? ???" });

            if (!CanManageTargetScope(user.ScopeLevel.ToString(), user.ScopeProvinceId, user.ScopeCityId))
                return Forbid("??? ????? ???????????? ??? ????? ?? ??????");

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            return Ok(new { success = true });
        }

        // Federation position assignment intentionally stays Admin-only,
        // with no scope-based expansion — this wasn't part of what you
        // asked to change, so I left it exactly as it was rather than
        // assuming it should follow the same new pattern. Worth revisiting
        // deliberately later if you want scoped Admins/Managers to assign
        // federation reps within their own jurisdiction too.
        [HttpPost("{id}/federation-position")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> AssignFederationPosition(Guid id, FederationPositionAssignDto dto)
        {
            // ... unchanged from before ...
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound(new { detail = "????? ???? ???" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null) return BadRequest(new { detail = "???????? ?????????? ????? ????" });

            if (!Enum.TryParse<FederationPositionType>(dto.Position, out var positionEnum))
                return BadRequest(new { detail = "??? ?????????? ????? ????" });

            var currentHolder = await _db.FederationPositions.FirstOrDefaultAsync(p =>
                p.FederationId == dto.FederationId && p.Position == positionEnum && p.EndDate == null);
            if (currentHolder != null) currentHolder.EndDate = dto.StartDate;

            var newPosition = new FederationPosition
            {
                UserId = id, FederationId = dto.FederationId, Position = positionEnum,
                StartDate = dto.StartDate, EndDate = null
            };
            _db.FederationPositions.Add(newPosition);
            await _db.SaveChangesAsync();

            return Ok(new { success = true, positionId = newPosition.Id });
        }

        [HttpGet("{id}/federation-positions")]
        public async Task<IActionResult> GetFederationPositions(Guid id)
        {
            var positions = await _db.FederationPositions
                .Include(p => p.Federation).Include(p => p.User)
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new FederationPositionOutDto(
                    p.Id, p.Position.ToString(), p.StartDate, p.EndDate,
                    p.UserId, p.User.FirstName!, p.User.LastName!,
                    p.FederationId, p.Federation.Name ?? ""))
                .ToListAsync();

            return Ok(positions);
        }

        private async Task<string?> ValidateScopeAsync(ScopeLevel level, Guid? provinceId, Guid? cityId)
        {
            if (level == ScopeLevel.Mastery) return null;

            if (level == ScopeLevel.Province)
            {
                if (provinceId == null) return "???? ?????? ??????? ?????? ????? ?????? ???";
                if (!await _db.Provinces.AnyAsync(p => p.Id == provinceId)) return "????? ?????????? ????? ????";
                return null;
            }

            if (level == ScopeLevel.City)
            {
                if (cityId == null) return "???? ?????? ????? ?????? ??? ?????? ???";
                var city = await _db.Cities.FindAsync(cityId);
                if (city == null) return "??? ?????????? ????? ????";
                if (provinceId != null && city.ProvinceId != provinceId)
                    return "????? ? ??? ?????????? ?? ?????? ??????? ??????";
                return null;
            }

            return null;
        }
    }
}
