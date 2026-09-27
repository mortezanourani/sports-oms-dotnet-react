using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Extensions;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/federations")]
    [Authorize] // read access is open to any authenticated user; write actions are checked manually below
    public class FederationsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        public FederationsController(ApplicationDbContext db) => _db = db;

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
                .Include(f => f.Province).Include(f => f.City)
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
                .Select(p => new FederationPositionOutDto(
                    p.Id, p.Position.ToString(), p.StartDate, p.EndDate,
                    p.UserId, p.User.FirstName!, p.User.LastName!,
                    p.FederationId, p.Federation.Name ?? ""))
                .ToListAsync();

            return Ok(positions);
        }
    }
}
