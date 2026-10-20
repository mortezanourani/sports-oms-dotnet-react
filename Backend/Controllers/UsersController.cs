using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    [Route("api/users")]
    [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer,SportsManager,SportsOfficer")]
    public class UsersController : ControllerBase
    {
        private const string ImportDefaultPassword = "P@ssw0rd";
        private const string UserManagementRoles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer";

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

        // Geographic restrictions apply to all target roles:
        // - Mastery scope can manage any scope
        // - Province scope can manage that province and its cities
        // - City scope can manage only that city
        // 
        // FederationRep accounts are managed through Federations API only
        // This controller NEVER handles FederationRep role users
        private bool CanManageTargetScope(
            string targetScopeLevel,
            Guid? targetProvinceId,
            Guid? targetCityId,
            string? targetRole,
            bool enforceRoleRestrictions = true)
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
                if (enforceRoleRestrictions)
                {
                    if (targetRole == "Admin")
                        return false;
                    if (myRole == "HumanResourceManager" && targetRole == "Manager")
                        return false;
                    if (myRole == "HumanResourceOfficer" &&
                        targetRole is "HumanResourceManager" or "Manager" or "Admin")
                        return false;
                }

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
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to create users
        // Does NOT include FederationRep - that's handled in FederationsController
        private bool CanCreateUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to edit users
        private bool CanEditUsers() => 
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "HumanResourceOfficer";

        // Check if current user has permission to deactivate users
        private bool CanDeactivateUsers() =>
            User.Role() is "Admin" or "Manager" or "HumanResourceManager" or "SportsManager";

        private static int RolePriority(UserRole role) => role switch
        {
            UserRole.Admin => 4,
            UserRole.Manager => 3,
            UserRole.HumanResourceManager => 2,
            UserRole.HumanResourceOfficer => 1,
            _ => 0
        };

        private static int ScopePriority(ScopeLevel scope) => scope switch
        {
            ScopeLevel.Mastery => 2,
            ScopeLevel.Province => 1,
            _ => 0
        };

        private bool CanResetPassword(ApplicationUser target)
        {
            if (!Enum.TryParse<UserRole>(User.Role(), out var currentRole) ||
                !Enum.TryParse<ScopeLevel>(User.ScopeLevel(), out var currentScope))
                return false;

            var hasHigherPriority =
                RolePriority(currentRole) > RolePriority(target.Role) ||
                ScopePriority(currentScope) > ScopePriority(target.ScopeLevel);

            return hasHigherPriority &&
                   CanManageTargetScope(
                       target.ScopeLevel.ToString(),
                       target.ScopeProvinceId,
                       target.ScopeCityId,
                       target.Role.ToString(),
                       enforceRoleRestrictions: false);
        }

        // GET /api/users?scopeLevel=&provinceId=&cityId=
        [HttpGet]
        [Authorize(Roles = UserManagementRoles)]
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
        [Authorize(Roles = UserManagementRoles)]
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
        [Authorize(Roles = UserManagementRoles)]
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

        [HttpGet("import/template")]
        [Authorize(Roles = UserManagementRoles)]
        public IActionResult DownloadImportTemplate()
        {
            if (!CanCreateUsers())
                return Forbid("شما اجازه ایجاد کاربر ندارید");

            return File(
                UserImportWorkbook.CreateTemplate(),
                UserImportWorkbook.ContentType,
                "users-import-template.xlsx");
        }

        [HttpPost("import")]
        [Authorize(Roles = UserManagementRoles)]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (!CanCreateUsers())
                return Forbid("شما اجازه ایجاد کاربر ندارید");

            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<UserImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = UserImportWorkbook.Read(stream);
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

            var provinces = await _db.Provinces
                .Select(province => new { province.Id, province.Name })
                .ToListAsync();
            var cities = await _db.Cities
                .Select(city => new { city.Id, city.Name, city.ProvinceId })
                .ToListAsync();
            var created = 0;
            var skippedExisting = 0;
            var skippedRequired = 0;
            var errors = new List<UserImportRowError>();

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Username) ||
                    string.IsNullOrWhiteSpace(row.FirstName) ||
                    string.IsNullOrWhiteSpace(row.LastName) ||
                    string.IsNullOrWhiteSpace(row.Scope))
                {
                    skippedRequired++;
                    continue;
                }

                var username = row.Username.Trim();
                if (await _userManager.FindByNameAsync(username) != null)
                {
                    skippedExisting++;
                    continue;
                }

                if (!UserImportWorkbook.TryParseRole(row.Role, out var role) ||
                    role == UserRole.FederationRep)
                {
                    errors.Add(new UserImportRowError(row.RowNumber, "نقش نامعتبر است یا پشتیبانی نمی‌شود."));
                    continue;
                }

                if (!UserImportWorkbook.TryParseScope(row.Scope, out var scopeLevel))
                {
                    errors.Add(new UserImportRowError(row.RowNumber, "سطح دسترسی نامعتبر است."));
                    continue;
                }

                var provinceId = (Guid?)null;
                var cityId = (Guid?)null;
                if (scopeLevel == ScopeLevel.Mastery)
                {
                    if (row.ProvinceName != null || row.CityName != null)
                    {
                        errors.Add(new UserImportRowError(row.RowNumber, "در سطح Mastery نباید استان یا شهری وارد شود."));
                        continue;
                    }
                }
                else if (scopeLevel == ScopeLevel.Province)
                {
                    var provinceMatches = provinces
                        .Where(province => string.Equals(
                            province.Name.Trim(), row.ProvinceName, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    if (row.CityName != null || provinceMatches.Count != 1)
                    {
                        errors.Add(new UserImportRowError(
                            row.RowNumber,
                            provinceMatches.Count == 0
                                ? "نام استان وارد نشده یا با استان ثبت‌شده مطابقت ندارد."
                                : "نام استان تکراری است یا برای سطح Province شهر وارد شده است."));
                        continue;
                    }

                    provinceId = provinceMatches[0].Id;
                }
                else
                {
                    var matchingProvinces = row.ProvinceName == null
                        ? null
                        : provinces.Where(province => string.Equals(
                            province.Name.Trim(), row.ProvinceName, StringComparison.OrdinalIgnoreCase)).ToList();

                    if (matchingProvinces is { Count: not 1 })
                    {
                        errors.Add(new UserImportRowError(
                            row.RowNumber,
                            matchingProvinces.Count == 0
                                ? "نام استان با استان ثبت‌شده مطابقت ندارد."
                                : "نام استان تکراری است."));
                        continue;
                    }

                    var cityMatches = cities
                        .Where(city => string.Equals(
                            city.Name.Trim(), row.CityName, StringComparison.OrdinalIgnoreCase))
                        .Where(city => matchingProvinces == null || city.ProvinceId == matchingProvinces[0].Id)
                        .ToList();

                    if (row.CityName == null || cityMatches.Count != 1)
                    {
                        errors.Add(new UserImportRowError(
                            row.RowNumber,
                            cityMatches.Count == 0
                                ? "نام شهر وارد نشده یا با شهر ثبت‌شده مطابقت ندارد."
                                : "نام شهر تکراری است؛ نام استان را نیز وارد کنید."));
                        continue;
                    }

                    cityId = cityMatches[0].Id;
                    provinceId = cityMatches[0].ProvinceId;
                }

                if (!CanManageTargetScope(
                        scopeLevel.ToString(), provinceId, cityId, role.ToString(),
                        enforceRoleRestrictions: false))
                {
                    errors.Add(new UserImportRowError(row.RowNumber, "شما اجازه ایجاد کاربر با این نقش و سطح دسترسی را ندارید."));
                    continue;
                }

                if (!CanImportTargetRole(role))
                {
                    errors.Add(new UserImportRowError(row.RowNumber, "شما اجازه ایجاد کاربر با این سمت را ندارید."));
                    continue;
                }

                var gender = (Gender?)null;
                if (row.Gender != null)
                {
                    if (!UserImportWorkbook.TryParseGender(row.Gender, out var parsedGender))
                    {
                        errors.Add(new UserImportRowError(row.RowNumber, "جنسیت باید مرد یا زن باشد."));
                        continue;
                    }

                    gender = parsedGender;
                }

                var user = new ApplicationUser
                {
                    UserName = username,
                    Email = $"{username}@local",
                    PhoneNumber = row.PhoneNumber,
                    FirstName = row.FirstName.Trim(),
                    LastName = row.LastName.Trim(),
                    FatherName = row.FatherName,
                    Gender = gender,
                    Role = role,
                    ScopeLevel = scopeLevel,
                    ScopeProvinceId = provinceId,
                    ScopeCityId = cityId,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, ImportDefaultPassword);
                if (result.Succeeded)
                {
                    created++;
                    continue;
                }

                if (result.Errors.Any(error => error.Code == "DuplicateUserName"))
                {
                    skippedExisting++;
                    continue;
                }

                errors.Add(new UserImportRowError(
                    row.RowNumber,
                    string.Join(" / ", result.Errors.Select(error => error.Description))));
            }

            return Ok(new UserImportResultDto(
                created,
                skippedExisting,
                skippedRequired,
                errors.Count,
                errors));
        }

        private bool CanImportTargetRole(UserRole targetRole)
        {
            if (targetRole == UserRole.FederationRep ||
                !Enum.TryParse<UserRole>(User.Role(), out var uploaderRole))
                return false;

            return UserImportAuthorization.CanAssignRole(uploaderRole, targetRole);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = UserManagementRoles)]
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
        [Authorize(Roles = UserManagementRoles)]
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

        [HttpPost("{id}/password")]
        [Authorize(Roles = UserManagementRoles)]
        public async Task<IActionResult> ResetPassword(Guid id, UserPasswordResetDto request)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null || user.Role == UserRole.FederationRep)
                return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanResetPassword(user))
                return Forbid();

            if (request == null || string.IsNullOrWhiteSpace(request.NewPassword))
                return BadRequest(new { detail = "رمز عبور جدید الزامی است" });

            var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, resetToken, request.NewPassword);
            if (!result.Succeeded)
                return BadRequest(new { detail = string.Join(" / ", result.Errors.Select(error => error.Description)) });

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
