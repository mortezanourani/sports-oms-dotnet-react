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
    [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
        }

        // Authorization logic:
        // Admins also have scope limitations:
        // - Mastery Admin can edit all Admin users
        // - Province Admin can only edit Province Admin (in their province) and City Admin (in their province)
        // - City Admin can only edit City Admin (in their city)
        //
        // Manager, HumanResourceManager, HumanResourceOfficer can manage users in:
        // - Their own scope
        // - All lower-level scopes (Province users for Mastery, City users for Mastery/Province)
        //
        // Role-based restrictions:
        // - Manager, HumanResourceManager cannot edit Admin users
        // - HumanResourceManager cannot edit Manager users
        // - HumanResourceOfficer cannot edit HumanResourceManager, Manager, or Admin users
        // - HumanResourceOfficer cannot deactivate users
        private bool CanManageTargetScope(string targetScopeLevel, Guid? targetProvinceId, Guid? targetCityId, string? targetRole)
        {
            var myRole = User.Role();
            var myScope = User.ScopeLevel();

            // Admin scope restrictions
            if (myRole == "Admin")
            {
                if (myScope == "Mastery")
                {
                    // Mastery Admin can manage all Admin users (any scope)
                    return true;
                }
                else if (myScope == "Province")
                {
                    // Province Admin can only manage Province Admin and City Admin in their province
                    if (targetScopeLevel == "Mastery") return false;
                    if (targetScopeLevel == "Province" && targetProvinceId != User.ScopeProvinceId()) return false;
                    if (targetScopeLevel == "City" && targetProvinceId != User.ScopeProvinceId()) return false;
                    return true;
                }
                else
                {
                    // City Admin can only manage City Admin in their city
                    if (targetScopeLevel != "City") return false;
                    if (targetCityId != User.ScopeCityId()) return false;
                    return true;
                }
            }

            // Determine if target is in allowed scope hierarchy
            bool isInAllowedScope;
            if (myScope == "Mastery")
            {
                // Mastery scope can manage Mastery, Province, and City users
                isInAllowedScope = targetScopeLevel is "Mastery" or "Province" or "City";
            }
            else if (myScope == "Province")
            {
                // Province scope can manage Province and City users (in their province)
                isInAllowedScope = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                   (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
            }
            else
            {
                // City scope can only manage City users in their city
                isInAllowedScope = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
            }

            if (!isInAllowedScope)
                return false;

            // Role-based restrictions
            if (myRole == "Manager" || myRole == "HumanResourceManager" || myRole == "HumanResourceOfficer")
            {
                // Cannot manage Admin users
                if (targetRole == "Admin")
                    return false;

                // HumanResourceManager cannot manage Manager users
                if (myRole == "HumanResourceManager" && targetRole == "Manager")
                    return false;

                // HumanResourceOfficer cannot manage HumanResourceManager, Manager, or Admin users
                if (myRole == "HumanResourceOfficer" && targetRole is "HumanResourceManager" or "Manager" or "Admin")
                    return false;

                // HumanResourceOfficer can only manage HumanResourceOfficer users if at same or lower scope
                if (myRole == "HumanResourceOfficer" && targetRole == "HumanResourceOfficer")
                {
                    if (myScope == "Mastery")
                    {
                        // Mastery HumanResourceOfficer can manage any HumanResourceOfficer
                        return true;
                    }
                    else if (myScope == "Province")
                    {
                        // Province HumanResourceOfficer can manage HumanResourceOfficer in their province
                        return targetProvinceId == User.ScopeProvinceId();
                    }
                    else
                    {
                        // City HumanResourceOfficer can only manage HumanResourceOfficer in their city
                        return targetCityId == User.ScopeCityId();
                    }
                }
            }

            return true;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // Helper methods for role-based permissions
        private bool HasUserManagementRole() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to create users
        private bool CanCreateUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to edit users
        private bool CanEditUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to deactivate users
        // HumanResourceOfficer is explicitly excluded
        private bool CanDeactivateUsers() =>
            User.Role() is "Admin" or "Manager" or "HumanResourceManager";

        // GET /api/users?scopeLevel=&provinceId=&cityId=
        [HttpGet]
        public async Task<IActionResult> List(string? scopeLevel, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Users.Include(u => u.ScopeProvince).Include(u => u.ScopeCity).AsQueryable();

            if (!string.IsNullOrEmpty(scopeLevel))
            {
                if (!Enum.TryParse<ScopeLevel>(scopeLevel, out var scopeLevelEnum))
                    return BadRequest(new { detail = "سطح دسترسی نامعتبر است" });
                qs = qs.Where(u => u.ScopeLevel == scopeLevelEnum);
            }

            if (cityId.HasValue) qs = qs.Where(u => u.ScopeCityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(u => u.ScopeProvinceId == provinceId.Value);

            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();

            // Apply scope-based filtering
            if (myScope == "Mastery")
            {
                // Mastery scope can see all users
            }
            else if (myScope == "Province")
            {
                // Province scope can see Province and City users in their province
                qs = qs.Where(u => 
                    (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                    (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
            }
            else if (myScope == "City")
            {
                // City scope can only see City users in their city
                qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
            }

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
            if (u == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageTargetScope(u.ScopeLevel.ToString(), u.ScopeProvinceId, u.ScopeCityId, u.Role.ToString()))
                return Forbid("شما اجازه مشاهده این کاربر را ندارید");

            return Ok(new UserOutDto(
                u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
                u.ScopeProvinceId, u.ScopeProvince?.Name, u.ScopeCityId, u.ScopeCity?.Name));
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateDto dto)
        {
            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            if (!CanCreateUsers())
                return Forbid("شما اجازه ایجاد کاربر ندارید");

            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId, dto.Role))
                return Forbid("شما اجازه ایجاد کاربر در این سطح دسترسی و نقش را ندارید");

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
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            // Checked against the user's CURRENT scope and role first - a caller
            // must already be allowed to manage this person before touching them at all.
            if (!CanEditUsers())
                return Forbid("شما اجازه ویرایش کاربران ندارید");

            if (!CanManageTargetScope(user.ScopeLevel.ToString(), user.ScopeProvinceId, user.ScopeCityId, user.Role.ToString()))
                return Forbid("شما اجازه ویرایش این کاربر را ندارید");

            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            // ALSO checked against the proposed NEW scope - otherwise a
            // Province admin could edit someone already within their
            // jurisdiction and "promote" them to National scope, escaping
            // the province admin's own authority entirely.
            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId, dto.Role))
                return Forbid("شما اجازه انتقال این کاربر به این سطح دسترسی را ندارید");

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
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            // HumanResourceOfficer cannot deactivate users at all
            if (!CanDeactivateUsers())
                return Forbid("شما اجازه غیرفعال‌سازی کاربران ندارید");

            if (!CanManageTargetScope(user.ScopeLevel.ToString(), user.ScopeProvinceId, user.ScopeCityId, user.Role.ToString()))
                return Forbid("شما اجازه غیرفعال‌سازی این کاربر را ندارید");

            await _userManager.SetLockoutEnabledAsync(user, true);
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);

            return Ok(new { success = true });
        }

        // Federation position assignment intentionally stays Admin-only
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
                if (provinceId == null) return "برای دسترسی استانی، انتخاب استان الزامی است";
                if (!await _db.Provinces.AnyAsync(p => p.Id == provinceId)) return "استان انتخاب‌شده معتبر نیست";
                return null;
            }

            if (level == ScopeLevel.City)
            {
                if (cityId == null) return "برای دسترسی شهری، انتخاب شهر الزامی است";
                var city = await _db.Cities.FindAsync(cityId);
                if (city == null) return "شهر انتخاب‌شده معتبر نیست";
                if (provinceId != null && city.ProvinceId != provinceId)
                    return "استان و شهر انتخاب‌شده با یکدیگر همخوانی ندارند";
                return null;
            }

            return null;
        }
    }
}
