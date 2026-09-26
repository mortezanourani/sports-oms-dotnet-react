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
    [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer,SportsManager,SportsOfficer")]
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
        // - Admin users can also manage FederationRep users in their own scope + lower scopes
        // - Manager users can also manage FederationRep users in their own scope + lower scopes
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
        //
        // SportsManager and SportsOfficer can ONLY create/edit users with FederationRep role
        // - They can create/edit FederationRep users in their own scope + lower scopes
        // - SportsManager can deactivate FederationRep users in their own scope + lower scopes
        // - SportsOfficer CANNOT deactivate any users
        // - SportsManager/SportsOfficer CANNOT edit Admin, Manager, HumanResourceManager, HumanResourceOfficer users
        private bool CanManageTargetScope(string targetScopeLevel, Guid? targetProvinceId, Guid? targetCityId, string? targetRole)
        {
            var myRole = User.Role();
            var myScope = User.ScopeLevel();

            // Admin scope restrictions
            if (myRole == "Admin")
            {
                // Admin can always manage FederationRep users in their scope + lower scopes
                if (targetRole == "FederationRep")
                {
                    bool adminIsInAllowedScope;
                    if (myScope == "Mastery")
                    {
                        adminIsInAllowedScope = targetScopeLevel is "Mastery" or "Province" or "City";
                    }
                    else if (myScope == "Province")
                    {
                        adminIsInAllowedScope = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                           (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
                    }
                    else
                    {
                        adminIsInAllowedScope = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
                    }
                    if (adminIsInAllowedScope) return true;
                }

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

            // Manager can manage FederationRep users in their scope + lower scopes
            if (myRole == "Manager")
            {
                if (targetRole == "FederationRep")
                {
                    bool managerIsInAllowedScope;
                    if (myScope == "Mastery")
                    {
                        managerIsInAllowedScope = targetScopeLevel is "Mastery" or "Province" or "City";
                    }
                    else if (myScope == "Province")
                    {
                        managerIsInAllowedScope = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                           (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
                    }
                    else
                    {
                        managerIsInAllowedScope = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
                    }
                    if (managerIsInAllowedScope) return true;
                }

                // Manager cannot manage Admin users
                if (targetRole == "Admin")
                    return false;

                // Determine if target is in allowed scope hierarchy
                bool managerScopeCheck;
                if (myScope == "Mastery")
                {
                    managerScopeCheck = targetScopeLevel is "Mastery" or "Province" or "City";
                }
                else if (myScope == "Province")
                {
                    managerScopeCheck = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                       (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
                }
                else
                {
                    managerScopeCheck = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
                }

                if (!managerScopeCheck)
                    return false;

                return true;
            }

            // Determine if target is in allowed scope hierarchy
            bool isInAllowedScopeAny;
            if (myScope == "Mastery")
            {
                isInAllowedScopeAny = targetScopeLevel is "Mastery" or "Province" or "City";
            }
            else if (myScope == "Province")
            {
                isInAllowedScopeAny = (targetScopeLevel == "Province" && targetProvinceId == User.ScopeProvinceId()) ||
                                   (targetScopeLevel == "City" && targetProvinceId == User.ScopeProvinceId());
            }
            else
            {
                isInAllowedScopeAny = targetScopeLevel == "City" && targetCityId == User.ScopeCityId();
            }

            if (!isInAllowedScopeAny)
                return false;

            // SportsManager and SportsOfficer can ONLY manage FederationRep users
            if (myRole is "SportsManager" or "SportsOfficer")
            {
                // Can only manage FederationRep users
                if (targetRole != "FederationRep")
                    return false;
                return true;
            }

            // Role-based restrictions for other roles
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

                // HumanResourceManager and HumanResourceOfficer CANNOT manage FederationRep users
                if (targetRole == "FederationRep")
                    return false;

                // HumanResourceOfficer can only manage HumanResourceOfficer users if at same or lower scope
                if (myRole == "HumanResourceOfficer" && targetRole == "HumanResourceOfficer")
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
            }

            return true;
        }

        private Guid CurrentUserId => Guid.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        // Helper methods for role-based permissions
        private bool HasUserManagementRole() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to create users
        private bool CanCreateUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to edit users
        private bool CanEditUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer" or "SportsManager" or "SportsOfficer";

        // Check if current user has permission to deactivate users
        // HumanResourceOfficer and SportsOfficer are explicitly excluded
        private bool CanDeactivateUsers() =>
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "SportsManager";

        // Check if current user can manage FederationRep users
        private bool CanManageFederationRepUsers() =>
            User.Role() is "Admin" or "Manager" or "SportsManager" or "SportsOfficer";

        // Check if user can create FederationRep users specifically
        private bool CanCreateFederationRepUsers() =>
            User.Role() is "Admin" or "Manager" or "SportsManager" or "SportsOfficer";

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
            var myRole = User.Role();

            // Apply scope-based filtering
            if (myRole is "SportsManager" or "SportsOfficer")
            {
                // SportsManager and SportsOfficer can only see FederationRep users in their scope
                qs = qs.Where(u => u.Role == UserRole.FederationRep);
                if (myScope == "Mastery")
                {
                    // Mastery can see all FederationRep users
                }
                else if (myScope == "Province")
                {
                    // Province can see FederationRep in their province
                    qs = qs.Where(u => 
                        (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                        (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
                }
                else if (myScope == "City")
                {
                    // City can only see FederationRep in their city
                    qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
                }
            }
            else if (myRole is "HumanResourceManager" or "HumanResourceOfficer")
            {
                // HumanResourceManager and HumanResourceOfficer CANNOT see FederationRep users
                qs = qs.Where(u => u.Role != UserRole.FederationRep);
                if (myScope == "Mastery")
                {
                    // Mastery can see all users except FederationRep
                }
                else if (myScope == "Province")
                {
                    // Province can see Province and City users (except FederationRep) in their province
                    qs = qs.Where(u => 
                        (u.ScopeLevel == ScopeLevel.Province && u.ScopeProvinceId == myProvinceId) ||
                        (u.ScopeLevel == ScopeLevel.City && u.ScopeProvinceId == myProvinceId));
                }
                else if (myScope == "City")
                {
                    // City can only see City users (except FederationRep) in their city
                    qs = qs.Where(u => u.ScopeLevel == ScopeLevel.City && u.ScopeCityId == myCityId);
                }
            }
            else if (myScope == "Mastery")
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

            // SportsManager and SportsOfficer can ONLY create FederationRep users
            if (User.Role() is "SportsManager" or "SportsOfficer")
            {
                if (roleEnum != UserRole.FederationRep)
                    return BadRequest(new { detail = "مدیران و کارشناسان ورزشی فقط مجاز به ایجاد کاربر با نقش نماینده فدراسیون هستند" });
            }

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

            // SportsManager and SportsOfficer can ONLY edit FederationRep users
            if (User.Role() is "SportsManager" or "SportsOfficer")
            {
                if (roleEnum != UserRole.FederationRep)
                    return BadRequest(new { detail = "مدیران و کارشناسان ورزشی فقط مجاز به ویرایش کاربر با نقش نماینده فدراسیون هستند" });
            }

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
