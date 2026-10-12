using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;
using Microsoft.AspNetCore.Identity;
using Backend.Services;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/federations")]
    [Authorize] // read access is open to any authenticated user; write actions are checked manually below
    public class FederationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly FederationPositionService _federationPositionService;
        public FederationsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            FederationPositionService federationPositionService)
        {
            _db = db;
            _userManager = userManager;
            _federationPositionService = federationPositionService;
        }

        // Users who can manage federations (Admin, Manager, SportsManager, SportsOfficer)
        private static readonly string[] FederationManagerRoles = { "Admin", "Manager", "SportsManager", "SportsOfficer" };

        // Users who can delete federations (excludes SportsOfficer)
        private static readonly string[] FederationDeleterRoles = { "Admin", "Manager", "SportsManager" };

        private bool HasFederationPermission() => FederationManagerRoles.Contains(User.Role());

        private bool HasDeletePermission() => FederationDeleterRoles.Contains(User.Role());

        // Can this user manage entities at the given scope level or lower?
        // Hierarchy: Mastery > Province > City
        private bool CanManageScope(string targetScopeLevel)
        {
            if (!HasFederationPermission()) return false;

            var myScope = User.ScopeLevel();
            
            // Mastery can manage everything
            if (myScope == "Mastery") return true;
            
            // Province can manage Province and City scopes
            if (myScope == "Province")
            {
                return targetScopeLevel == "Province" || targetScopeLevel == "City";
            }
            
            // City can only manage City scope
            if (myScope == "City")
            {
                return targetScopeLevel == "City";
            }
            
            return false;
        }

        // Can this user manage a National-level federation? Only if their
        // OWN scope is National - a Province- or City-scoped user cannot
        // touch the national federation itself, only their own region's entries.
        private bool CanManageMastery()
        {
            if (!HasFederationPermission()) return false;
            return User.ScopeLevel() == "Mastery";
        }

        // Can this user manage a Provincial federation for a given province?
        // Province-scoped users can only manage their own province.
        // City-scoped users cannot manage province-level federations.
        private bool CanManageProvincial(Guid targetProvinceId)
        {
            if (!HasFederationPermission()) return false;
            
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            
            // Mastery can manage any province
            if (myScope == "Mastery") return true;
            
            // Province can only manage their own province
            if (myScope == "Province") return myProvinceId == targetProvinceId;
            
            // City cannot manage province-level federations
            return false;
        }

        // Can this user manage a City federation for a given city?
        // City-scoped users can only manage their own city.
        // Province-scoped users can manage all cities in their province.
        // Mastery can manage all cities.
        private bool CanManageCity(Guid targetCityId, Guid targetProvinceId)
        {
            if (!HasFederationPermission()) return false;
            
            var myScope = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();
            
            // Mastery can manage any city
            if (myScope == "Mastery") return true;
            
            // Province can manage all cities in their province
            if (myScope == "Province") return myProvinceId == targetProvinceId;
            
            // City can only manage their own city
            if (myScope == "City") return myCityId == targetCityId;
            
            return false;
        }

        // Filter federations query based on user's scope level for read operations
        private IQueryable<Federation> ApplyScopeFilter(IQueryable<Federation> query)
        {
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();
            
            if (myScopeLevel == "Province")
            {
                // Province users can see federations in their province (Provincial + Cities)
                query = query.Where(f => 
                    (f.Level == FederationLevel.Provincial && f.ProvinceId == myProvinceId) ||
                    (f.Level == FederationLevel.City && f.ProvinceId == myProvinceId));
            }
            else if (myScopeLevel == "City")
            {
                // City users can only see their own city federation
                query = query.Where(f => f.Level == FederationLevel.City && f.CityId == myCityId);
            }
            // Mastery users can see everything (no filter needed)
            
            return query;
        }

        private static string ComposeDisplayName(Federation f)
        {
            if (f.Level == FederationLevel.National) return f.Name ?? "";
            var parentName = f.ParentFederation?.Name ?? "";
            var regionName = f.Level == FederationLevel.Provincial ? f.Province?.Name : f.City?.Name;
            return $"{parentName} - {regionName}";
        }

        [HttpGet]
        public async Task<IActionResult> List(string? level, Guid? parentFederationId, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Federations
                .Include(f => f.ParentFederation).Include(f => f.Province).Include(f => f.City)
                .Include(f => f.ChildFederations)
                .AsQueryable();

            // Apply scope filter based on user's scope level
            qs = ApplyScopeFilter(qs);

            if (!string.IsNullOrEmpty(level))
            {
                if (!Enum.TryParse<FederationLevel>(level, out var levelEnum))
                    return BadRequest(new { detail = "سطح فurasیون نامعتبر است" });
                qs = qs.Where(f => f.Level == levelEnum);
            }

            if (cityId.HasValue) qs = qs.Where(f => f.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(f => f.ProvinceId == provinceId.Value);
            
            if (parentFederationId.HasValue)
                qs = qs.Where(f => f.ParentFederationId == parentFederationId.Value);
            else if (string.IsNullOrEmpty(level))
                qs = qs.Where(f => f.Level == FederationLevel.National);

            var federations = await qs.OrderBy(f => f.Name ?? f.Province!.Name).ToListAsync();

            var result = federations.Select(f => new FederationOutDto(
                f.Id, ComposeDisplayName(f), f.Category.ToString(), f.Level.ToString(),
                f.ParentFederationId, f.ParentFederation?.Name,
                f.ProvinceId, f.Province?.Name, f.CityId, f.City?.Name,
                f.ChildFederations.Any()));

            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var f = await _db.Federations
                .Include(x => x.ParentFederation).Include(x => x.Province).Include(x => x.City).Include(x => x.ChildFederations)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (f == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            // Check if user has access to this federation based on scope
            var myScopeLevel = User.ScopeLevel();
            
            if (myScopeLevel == "Province")
            {
                var myProvinceId = User.ScopeProvinceId();
                if (f.Level == FederationLevel.Provincial && f.ProvinceId != myProvinceId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
                if (f.Level == FederationLevel.City && f.ProvinceId != myProvinceId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
            }
            else if (myScopeLevel == "City")
            {
                var myCityId = User.ScopeCityId();
                if (f.Level != FederationLevel.City || f.CityId != myCityId)
                    return Forbid("شما اجازه مشاهده این فurasیون را ندارید");
            }

            return Ok(new FederationOutDto(
                f.Id, ComposeDisplayName(f), f.Category.ToString(), f.Level.ToString(),
                f.ParentFederationId, f.ParentFederation?.Name,
                f.ProvinceId, f.Province?.Name, f.CityId, f.City?.Name,
                f.ChildFederations.Any()));
        }

        [HttpPost]
        public async Task<IActionResult> Create(FederationCreateDto dto)
        {
            if (!Enum.TryParse<FederationLevel>(dto.Level, out var levelEnum))
                return BadRequest(new { detail = "سطح فurasیون نامعتبر است" });

            if (levelEnum == FederationLevel.National)
            {
                if (!CanManageMastery())
                    return Forbid("فقط کاربران با سطح دسترسی ملی مجاز به ایجاد فurasیون ملی هستند");

                if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Category))
                    return BadRequest(new { detail = "لطفا نام و دسته‌بندی را کامل وارد کنید" });
                if (!Enum.TryParse<FederationCategory>(dto.Category, out var categoryEnum))
                    return BadRequest(new { detail = "دسته‌بندی نامعتبر است" });
                if (dto.ParentFederationId.HasValue || dto.ProvinceId.HasValue || dto.CityId.HasValue)
                    return BadRequest(new { detail = "فurasیون ملی نباید والد یا موقعیت جغرافیایی داشته باشد" });

                var duplicate = await _db.Federations.AnyAsync(f => f.Level == FederationLevel.National && f.Name == dto.Name);
                if (duplicate) return Conflict(new { detail = "این نام برای فurasیون ملی قبلا استفاده شده است" });

                var federation = new Federation { Name = dto.Name, Category = categoryEnum, Level = FederationLevel.National };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            if (levelEnum == FederationLevel.Provincial)
            {
                if (!dto.ParentFederationId.HasValue || !dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "لطفا والد و استان مربوطه را کامل وارد کنید" });
                if (dto.CityId.HasValue)
                    return BadRequest(new { detail = "فurasیون استانی نباید دارای شهر باشد" });

                if (!CanManageProvincial(dto.ProvinceId.Value))
                    return Forbid("شما اجازه ایجاد فurasیون در این استان را ندارید");

                var myProvinceId = User.ScopeProvinceId();
                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.National)
                    return BadRequest(new { detail = "فurasیون والد معتبر نیست" });

                if (!await _db.Provinces.AnyAsync(p => p.Id == dto.ProvinceId.Value))
                    return BadRequest(new { detail = "استان وارد شده معتبر نیست" });

                // Verify province is within user's scope
                if (User.ScopeLevel() == "Province" && myProvinceId != dto.ProvinceId.Value)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های استانی در استان خود هستید");

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.ProvinceId == dto.ProvinceId);
                if (duplicate) return Conflict(new { detail = "این فurasیون استانی قبلا برای این استان ثبت شده است" });

                var federation = new Federation
                {
                    Category = parent.Category,
                    Level = FederationLevel.Provincial,
                    ParentFederationId = parent.Id,
                    ProvinceId = dto.ProvinceId,
                };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            // City
            {
                if (!dto.ParentFederationId.HasValue || !dto.CityId.HasValue)
                    return BadRequest(new { detail = "لطفا والد و شهر مربوطه را کامل وارد کنید" });
                if (dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "فurasیون شهری نباید دارای استان مستقل باشد" });

                var city = await _db.Cities.FindAsync(dto.CityId.Value);
                if (city == null) return BadRequest(new { detail = "شهر وارد شده معتبر نیست" });

                if (!CanManageCity(dto.CityId.Value, city.ProvinceId))
                    return Forbid("شما اجازه ایجاد فurasیون در این شهر را ندارید");

                var myProvinceId = User.ScopeProvinceId();
                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.Provincial)
                    return BadRequest(new { detail = "فurasیون والد معتبر نیست" });

                // Verify city is within user's scope
                if (User.ScopeLevel() == "Province" && myProvinceId != city.ProvinceId)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های شهری در شهرهای استان خود هستید");
                if (User.ScopeLevel() == "City" && User.ScopeCityId() != dto.CityId.Value)
                    return Forbid("شما تنها مجاز به مدیریت فurasیون‌های شهری در شهر خود هستید");

                if (city.ProvinceId != parent.ProvinceId)
                    return BadRequest(new { detail = "استان شهر وارد شده با استان فurasیون والد مطابقت ندارد" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.CityId == dto.CityId);
                if (duplicate) return Conflict(new { detail = "این فurasیون شهری قبلا برای این شهر ثبت شده است" });

                var federation = new Federation
                {
                    Category = parent.Category,
                    Level = FederationLevel.City,
                    ParentFederationId = parent.Id,
                    CityId = dto.CityId,
                };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, FederationUpdateDto dto)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فurasیون‌های استانی و شهری قابل ویرایش نیستند" });

            if (!CanManageMastery())
                return Forbid("فقط کاربران با سطح دسترسی ملی مجاز به ویرایش فurasیون ملی هستند");

            federation.Name = dto.Name;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!HasDeletePermission())
                return Forbid("شما اجازه حذف فurasیون‌ها را ندارید");

            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var allowed = federation.Level switch
            {
                FederationLevel.National => CanManageMastery(),
                FederationLevel.Provincial => CanManageProvincial(federation.ProvinceId!.Value),
                FederationLevel.City => CanManageCity(federation.CityId!.Value, federation.City!.ProvinceId),
                _ => false,
            };
            if (!allowed) return Forbid("شما اجازه حذف این فurasیون را ندارید");

            try
            {
                _db.Federations.Remove(federation);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این فurasیون دارای ارتباطاتی است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }

        [HttpGet("{id}/positions")]
        public async Task<IActionResult> GetPositions(Guid id)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Id == id);
            if (!exists) return NotFound(new { detail = "فurasیون یافت نشد" });

            var positions = await _db.FederationPositions
                .Include(p => p.User).Include(p => p.Federation)
                .Where(p => p.FederationId == id)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();

            return Ok(positions.Select(p => new FederationPositionOutDto(
                p.Id, p.Position.ToString(), p.StartDate,
                p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate),
                p.UserId, p.User.FirstName ?? "", p.User.LastName ?? "",
                p.FederationId, p.Federation.Name ?? "")));
        }

        // FederationRep membership is represented by federation position history;
        // a user may keep a main role and hold positions across different federations.
        [HttpGet("{id}/federation-reps")]
        public async Task<IActionResult> GetFederationReps(Guid id)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مشاهده نمایندگان فurasیون در این سطح را ندارید");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var assignments = await _db.FederationPositions
                .Include(p => p.User)
                .Where(p => p.FederationId == id && p.StartDate <= today)
                .ToListAsync();
            var reps = assignments
                .Where(p => (p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate)) > today)
                .GroupBy(p => p.UserId)
                .Select(group => ToFederationRepOutDto(group.OrderByDescending(p => p.StartDate).First().User, federation))
                .OrderBy(user => user.Username)
                .ToList();

            return Ok(reps);
        }

        [HttpGet("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> GetFederationRep(Guid id, Guid userId)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مشاهده نمایندگان فurasیون در این سطح را ندارید");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var assignment = await _db.FederationPositions
                .Include(p => p.User)
                .Where(p => p.FederationId == id && p.UserId == userId && p.StartDate <= today)
                .OrderByDescending(p => p.StartDate)
                .ToListAsync();
            var currentAssignment = assignment.FirstOrDefault(p =>
                (p.EndDate ?? FederationPositionService.GetTermEndDate(p.StartDate)) > today);
            if (currentAssignment == null) return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            return Ok(ToFederationRepOutDto(currentAssignment.User, federation));
        }

        [HttpPost("{id}/federation-reps")]
        public async Task<IActionResult> CreateFederationRep(Guid id, FederationRepAssignDto dto)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City).ThenInclude(c => c!.Province)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه مدیریت نمایندگان فurasیون در این سطح را ندارید");

            if (string.IsNullOrWhiteSpace(dto.Username))
                return BadRequest(new { detail = "نام کاربری الزامی است" });

            if (!Enum.TryParse<FederationPositionType>(dto.Position, out var positionEnum) ||
                !Enum.IsDefined(positionEnum))
                return BadRequest(new { detail = "سمت انتخاب‌شده معتبر نیست" });

            var username = dto.Username.Trim();
            var user = await _userManager.FindByNameAsync(username);
            var isNewUser = user == null;
            if (user == null)
            {
                if (string.IsNullOrWhiteSpace(dto.Password))
                    return BadRequest(new { detail = "برای ایجاد حساب جدید، کلمه عبور الزامی است" });

                var scopeLevel = federation.Level switch
                {
                    FederationLevel.National => ScopeLevel.Mastery,
                    FederationLevel.Provincial => ScopeLevel.Province,
                    FederationLevel.City => ScopeLevel.City,
                    _ => throw new InvalidOperationException("Unsupported federation level.")
                };
                user = new ApplicationUser
                {
                    UserName = username,
                    Email = $"{username}@local",
                    FirstName = dto.FirstName ?? "",
                    LastName = dto.LastName ?? "",
                    Role = UserRole.FederationRep,
                    ScopeLevel = scopeLevel,
                    ScopeProvinceId = federation.Level == FederationLevel.City
                        ? federation.City?.ProvinceId
                        : federation.ProvinceId,
                    ScopeCityId = federation.CityId,
                    EmailConfirmed = true
                };
            }

            await using var transaction = await _db.Database.BeginTransactionAsync();
            if (isNewUser)
            {
                var createResult = await _userManager.CreateAsync(user!, dto.Password!);
                if (!createResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    var errors = string.Join(" / ", createResult.Errors.Select(e => e.Description));
                    return BadRequest(new { detail = errors });
                }
            }

            var (position, error) = await _federationPositionService.AssignAsync(
                user!, federation, positionEnum, dto.StartDate, dto.EndDate);
            if (error != null)
            {
                await transaction.RollbackAsync();
                return Conflict(new { detail = error });
            }

            await transaction.CommitAsync();
            return CreatedAtAction(nameof(GetFederationRep), new { id, userId = user!.Id },
                new { userId = user.Id, username = user.UserName, positionId = position!.Id });
        }

        // Update FederationRep account
        [HttpPut("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> UpdateFederationRep(Guid id, Guid userId, UserUpdateDto dto)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه ویرایش این نماینده فurasیون را ندارید");

            if (!await _db.FederationPositions.AnyAsync(p => p.FederationId == id && p.UserId == userId))
                return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            user.FirstName = dto.FirstName;
            user.LastName = dto.LastName;

            var result = await _userManager.UpdateAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(" / ", result.Errors.Select(e => e.Description));
                return BadRequest(new { detail = errors });
            }

            return Ok(new { success = true });
        }

        // End this user's assignments in the selected federation without disabling
        // their account or affecting positions held in other federations.
        [HttpDelete("{id}/federation-reps/{userId}")]
        public async Task<IActionResult> DeactivateFederationRep(Guid id, Guid userId)
        {
            var federation = await _db.Federations.FindAsync(id);
            if (federation == null) return NotFound(new { detail = "فurasیون یافت نشد" });

            var user = await _db.Users.FindAsync(userId);
            if (user == null) return NotFound(new { detail = "کاربر یافت نشد" });

            if (!CanManageFederationRepScope(federation))
                return Forbid("شما اجازه غیرفعال‌سازی این نماینده فurasیون را ندارید");

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var assignments = await _db.FederationPositions
                .Where(p => p.FederationId == id && p.UserId == userId && p.StartDate <= today)
                .ToListAsync();
            if (assignments.Count == 0)
                return NotFound(new { detail = "نماینده فدراسیون یافت نشد" });

            foreach (var assignment in assignments)
            {
                var effectiveEndDate = assignment.EndDate ??
                    FederationPositionService.GetTermEndDate(assignment.StartDate);
                if (effectiveEndDate > today)
                    assignment.EndDate = today;
            }
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        private static UserOutDto ToFederationRepOutDto(ApplicationUser user, Federation federation)
        {
            var scopeLevel = federation.Level switch
            {
                FederationLevel.National => ScopeLevel.Mastery,
                FederationLevel.Provincial => ScopeLevel.Province,
                FederationLevel.City => ScopeLevel.City,
                _ => throw new InvalidOperationException("Unsupported federation level.")
            };

            return new UserOutDto(
                user.Id, user.UserName!, user.FirstName ?? "", user.LastName ?? "",
                UserRole.FederationRep.ToString(), scopeLevel.ToString(),
                federation.Level == FederationLevel.City ? federation.City?.ProvinceId : federation.ProvinceId,
                federation.Province?.Name ?? federation.City?.Province?.Name,
                federation.CityId, federation.City?.Name);
        }

        // Check if current user can manage FederationRep accounts for this federation
        private bool CanManageFederationRepScope(Federation federation)
        {
            var myRole = User.Role();
            var myScopeLevel = User.ScopeLevel();

            // Admin, Manager, SportsManager can manage FederationRep accounts
            if (myRole is not ("Admin" or "Manager" or "SportsManager"))
                return false;

            // Scope validation based on federation level
            if (federation.Level == FederationLevel.National)
            {
                // Only Mastery can manage national federation reps
                return myScopeLevel == "Mastery";
            }
            
            if (federation.Level == FederationLevel.Provincial)
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province") return User.ScopeProvinceId() == federation.ProvinceId;
                return false;
            }
            
            if (federation.Level == FederationLevel.City)
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province") return User.ScopeProvinceId() == federation.ProvinceId;
                if (myScopeLevel == "City") return User.ScopeCityId() == federation.CityId;
                return false;
            }

            return false;
        }
    }
}
