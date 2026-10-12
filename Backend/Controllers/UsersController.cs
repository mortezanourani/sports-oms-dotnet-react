using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/users")]
    [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer,SportsManager,SportsOfficer")]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FederationPositionService _federationPositionService;

        public UsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            FederationPositionService federationPositionService)
        {
            _db = db;
            _userManager = userManager;
            _federationPositionService = federationPositionService;
        }

        // Authorization logic:
        // Admins also have scope limitations:
        // - Mastery Admin can edit all Admin users
        // - Province Admin can only edit Province Admin (in their province) and City Admin (in their province)
        // - City Admin can only edit City Admin (in their city)
        // - Manager, HumanResourceManager, HumanResourceOfficer can manage users in their scope + lower scopes
        // 
        // FederationRep accounts are managed through Federations API only
        // This controller NEVER handles FederationRep role users
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

            // Manager scope restrictions
            if (myRole == "Manager")
            {
                if (myScope == "Mastery")
                {
                    return true;
                }
                else if (myScope == "Province")
                {
                    return targetProvinceId == User.ScopeProvinceId();
                }
                else
                {
                    return targetCityId == User.ScopeCityId();
                }
            }

            // SportsManager and SportsOfficer scope restrictions
            if (myRole is "SportsManager" or "SportsOfficer")
            {
                bool isInAllowedScope;
                if (myScope == "Mastery")
                {
                    isInAllowedScope = targetScopeLevel is "Mastery" or "Province" or "City";
                }
                else if (myScope == "Province")
                {
                    isInAllowedScope = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                      (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
                }
                else
                {
                    isInAllowedScope = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
                }
                
                if (!isInAllowedScope) return false;
                return true;
            }

            // Role-based restrictions for HumanResourceManager and HumanResourceOfficer
            if (myRole == "HumanResourceManager" || myRole == "HumanResourceOfficer")
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

                // Check scope
                if (myScope == "Mastery")
                {
                    return true;
                }
                else if (myScope == "Province")
                {
                    return targetProvinceId == User.ScopeProvinceId();
                }
                else
                {
                    return targetCityId == User.ScopeCityId();
                }
            }

            return false;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // Helper methods for role-based permissions
        private bool HasUserManagementRole() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to create users
        // Does NOT include FederationRep - that's handled in FederationsController
        private bool CanCreateUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to edit users
        private bool CanEditUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to deactivate users
        private bool CanDeactivateUsers() =>
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "SportsManager";

        // GET /api/users?scopeLevel=&provinceId=&cityId=
        [HttpGet]
        public async Task<IActionResult> List(string? scopeLevel, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Users.Include(u => u.ScopeProvince).Include(u => u.ScopeCity).AsQueryable();

            // NEVER include FederationRep role users in this controller
            // This controller manages regular users only
            qs = qs.Where(u => u.Role != UserRole.FederationRep);

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
            var myRole = User.Role();

            // Apply scope-based filtering
            if (myRole is "SportsManager" or "SportsOfficer")
            {
                // SportsManager and SportsOfficer can see users in their scope
                if (myScope == "Mastery")
                {
                    // Mastery can see all users (except FederationRep)
                }
                else if (myScope == "Province")
                {
                    qs = qs.Where(u => 
                        (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                        (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
                }
                else if (myScope == "City")
                {
                    qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
                }
            }
            else if (myRole is "HumanResourceManager" or "HumanResourceOfficer")
            {
                // HumanResourceManager and HumanResourceOfficer have scope restrictions
                if (myScope == "Mastery")
                {
                    // Mastery can see all users (except FederationRep)
                }
                else if (myScope == "Province")
                {
                    qs = qs.Where(u => 
                        (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                        (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
                }
                else if (myScope == "City")
                {
                    qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
                }
            }
            else if (myScope == "Mastery")
            {
                // Mastery scope can see all users (except FederationRep)
            }
            else if (myScope == "Province")
            {
                qs = qs.Where(u => 
                    (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                    (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
            }
            else if (myScope == "City")
            {
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
            
            // Never return FederationRep role users through this controller
            if (u == null || u.Role == UserRole.FederationRep)
                return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageTargetScope(u.ScopeLevel.ToString(), u.ScopeProvinceId, u.ScopeCityId, u.Role.ToString()))
                return Forbid("شما اجازه مشاهده این کاربر را ندارید");

            return Ok(new UserOutDto(
                u.Id, u.UserName!, u.FirstName ?? "", u.LastName ?? "", u.Role.ToString(), u.ScopeLevel.ToString(),
                u.ScopeProvinceId, u.ScopeProvince?.Name, u.ScopeCityId, u.ScopeCity?.Name));
        }

        [HttpPost]
        public async Task<IActionResult> Create(UserCreateDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { detail = "نام کاربری الزامی است" });

            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum) || !Enum.IsDefined(roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });
            
            // FederationRep accounts are managed through Federations API
            if (roleEnum == UserRole.FederationRep)
                return BadRequest(new { detail = "ایجاد کاربر با نقش نماینده فدراسیون از طریق API فurasیون‌ها انجام می‌شود" });
            
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum) ||
                !Enum.IsDefined(scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            if (!CanCreateUsers())
                return Forbid("شما اجازه ایجاد کاربر ندارید");

            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId, dto.Role))
                return Forbid("شما اجازه ایجاد کاربر در این سطح دسترسی و نقش را ندارید");

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null) return BadRequest(new { detail = scopeError });

            var username = dto.Username.Trim();
            var existingUser = await _userManager.FindByNameAsync(username);
            if (existingUser != null)
            {
                if (existingUser.Role != UserRole.FederationRep)
                    return Conflict(new { detail = "این نام کاربری قبلا برای یک نقش اصلی استفاده شده است" });

                existingUser.Role = roleEnum;
                existingUser.ScopeLevel = scopeLevelEnum;
                existingUser.ScopeProvinceId = await GetScopeProvinceIdAsync(
                    scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
                existingUser.ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null;

                var updateResult = await _userManager.UpdateAsync(existingUser);
                if (!updateResult.Succeeded)
                {
                    var errors = string.Join(" / ", updateResult.Errors.Select(e => e.Description));
                    return BadRequest(new { detail = errors });
                }

                return CreatedAtAction(nameof(Get), new { id = existingUser.Id },
                    new UserOutDto(existingUser.Id, existingUser.UserName!, existingUser.FirstName ?? "",
                        existingUser.LastName ?? "", existingUser.Role.ToString(), existingUser.ScopeLevel.ToString(),
                        existingUser.ScopeProvinceId, null, existingUser.ScopeCityId, null));
            }

            if (string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { detail = "برای ایجاد حساب جدید، کلمه عبور الزامی است" });

            var normalizedProvinceId = await GetScopeProvinceIdAsync(
                scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            var user = new ApplicationUser
            {
                UserName = username,
                Email = $"{username}@local",
                FirstName = dto.FirstName,
                LastName = dto.LastName,
                Role = roleEnum,
                ScopeLevel = scopeLevelEnum,
                ScopeProvinceId = normalizedProvinceId,
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

            // Never update FederationRep role users through this controller
            if (user.Role == UserRole.FederationRep)
                return BadRequest(new { detail = "ویرایش کاربر با نقش نماینده فurasیون از طریق API فurasیون‌ها انجام می‌شود" });

            // Checked against the user's CURRENT scope and role first
            if (!CanEditUsers())
                return Forbid("شما اجازه ویرایش کاربران ندارید");

            if (!CanManageTargetScope(user.ScopeLevel.ToString(), user.ScopeProvinceId, user.ScopeCityId, user.Role.ToString()))
                return Forbid("شما اجازه ویرایش این کاربر را ندارید");

            if (!Enum.TryParse<UserRole>(dto.Role, out var roleEnum) || !Enum.IsDefined(roleEnum))
                return BadRequest(new { detail = "نقش انتخاب‌شده معتبر نیست" });
            
            // FederationRep accounts are managed through Federations API
            if (roleEnum == UserRole.FederationRep)
                return BadRequest(new { detail = "تغییر نقش به نماینده فurasیون از طریق API فurasیون‌ها انجام می‌شود" });
            
            if (!Enum.TryParse<ScopeLevel>(dto.ScopeLevel, out var scopeLevelEnum) ||
                !Enum.IsDefined(scopeLevelEnum))
                return BadRequest(new { detail = "سطح دسترسی جغرافیایی معتبر نیست" });

            // ALSO checked against the proposed NEW scope
            if (!CanManageTargetScope(dto.ScopeLevel, dto.ScopeProvinceId, dto.ScopeCityId, dto.Role))
                return Forbid("شما اجازه انتقال این کاربر به این سطح دسترسی را ندارید");

            var scopeError = await ValidateScopeAsync(scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            if (scopeError != null) return BadRequest(new { detail = scopeError });

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;
            user.Role = roleEnum;
            user.ScopeLevel = scopeLevelEnum;
            user.ScopeProvinceId = await GetScopeProvinceIdAsync(
                scopeLevelEnum, dto.ScopeProvinceId, dto.ScopeCityId);
            user.ScopeCityId = scopeLevelEnum == ScopeLevel.City ? dto.ScopeCityId : null;

            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            // Never deactivate FederationRep role users through this controller
            if (user.Role == UserRole.FederationRep)
                return BadRequest(new { detail = "غیرفعال‌سازی کاربر با نقش نماینده فurasیون از طریق API فurasیون‌ها انجام می‌شود" });

            // HumanResourceOfficer and SportsOfficer cannot deactivate users at all
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
            if (federation == null) return BadRequest(new { detail = "فurasیون انتخاب‌شده معتبر نیست" });

            if (!Enum.TryParse<FederationPositionType>(dto.Position, out var positionEnum) ||
                !Enum.IsDefined(positionEnum))
                return BadRequest(new { detail = "سمت انتخاب‌شده معتبر نیست" });

            var (newPosition, error) = await _federationPositionService.AssignAsync(
                user, federation, positionEnum, dto.StartDate, dto.EndDate);
            if (error != null) return Conflict(new { detail = error });

            return Ok(new { success = true, positionId = newPosition!.Id });
        }

        [HttpGet("{id}/federation-positions")]
        public async Task<IActionResult> GetFederationPositions(Guid id)
        {
            var positions = await _db.FederationPositions
                .Include(p => p.Federation).Include(p => p.User)
                .Where(p => p.UserId == id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return Ok(positions.Select(p => new FederationPositionOutDto(
                p.Id, p.Position.ToString(), p.StartDate,
                p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate),
                p.UserId, p.User.FirstName ?? "", p.User.LastName ?? "",
                p.FederationId, p.Federation.Name ?? "")));
        }

        private async Task<string?> ValidateScopeAsync(ScopeLevel level, Guid? provinceId, Guid? cityId)
        {
            if (level == ScopeLevel.Mastery)
                return provinceId.HasValue || cityId.HasValue
                    ? "دسترسی ملی نباید استان یا شهر داشته باشد"
                    : null;

            if (level == ScopeLevel.Province)
            {
                if (provinceId == null) return "برای دسترسی استانی، انتخاب استان الزامی است";
                if (cityId.HasValue) return "دسترسی استانی نباید شهر داشته باشد";
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

        private async Task<Guid?> GetScopeProvinceIdAsync(
            ScopeLevel level,
            Guid? provinceId,
            Guid? cityId)
        {
            if (level == ScopeLevel.Province) return provinceId;
            if (level == ScopeLevel.City && cityId.HasValue)
                return await _db.Cities.Where(c => c.Id == cityId.Value)
                    .Select(c => (Guid?)c.ProvinceId)
                    .FirstOrDefaultAsync();
            return null;
        }

    }
}
