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

        private static readonly string[] AllowedManagerRoles = { "Admin", "Manager", "SportOfficer" };

        private bool HasAllowedRole() => AllowedManagerRoles.Contains(User.Role());

        // Can this user manage a National-level federation? Only if their
        // OWN scope is National — a Province- or City-scoped Admin/Manager/
        // SportOfficer cannot touch the national federation itself, only
        // their own region's entries under it.
        private bool CanManageNational() => HasAllowedRole() && User.ScopeLevel() == "National";

        // Can this user manage a Provincial federation for a given province?
        private bool CanManageProvincial(Guid targetProvinceId)
        {
            if (!HasAllowedRole()) return false;
            var scope = User.ScopeLevel();
            if (scope == "National") return true;
            if (scope == "Province") return User.ScopeProvinceId() == targetProvinceId;
            return false; // City-scoped users cannot manage province-level entries
        }

        // Can this user manage a City federation for a given city/province?
        private bool CanManageCity(Guid targetCityId, Guid targetProvinceId)
        {
            if (!HasAllowedRole()) return false;
            var scope = User.ScopeLevel();
            if (scope == "National") return true;
            if (scope == "Province") return User.ScopeProvinceId() == targetProvinceId;
            if (scope == "City") return User.ScopeCityId() == targetCityId;
            return false;
        }

        private static string ComposeDisplayName(Federation f)
        {
            if (f.Level == FederationLevel.National) return f.Name ?? "";
            var parentName = f.ParentFederation?.Name ?? "";
            var regionName = f.Level == FederationLevel.Provincial ? f.Province?.Name : f.City?.Name;
            return $"{parentName} - {regionName}";
        }

        [HttpGet]
        public async Task<IActionResult> List(string? level, Guid? parentFederationId)
        {
            var qs = _db.Federations
                .Include(f => f.ParentFederation).Include(f => f.Province).Include(f => f.City)
                .Include(f => f.ChildFederations)
                .AsQueryable();

            if (!string.IsNullOrEmpty(level))
            {
                if (!Enum.TryParse<FederationLevel>(level, out var levelEnum))
                    return BadRequest(new { detail = "سطح فدراسیون نامعتبر است" });
                qs = qs.Where(f => f.Level == levelEnum);
            }

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

            if (f == null) return NotFound(new { detail = "فدراسیون یافت نشد" });

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
                return BadRequest(new { detail = "سطح فدراسیون نامعتبر است" });

            if (levelEnum == FederationLevel.National)
            {
                if (!CanManageNational())
                    return Forbid("شما اجازه ایجاد فدراسیون کشوری را ندارید");

                if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Category))
                    return BadRequest(new { detail = "نام و دسته‌بندی برای فدراسیون کشوری الزامی است" });
                if (!Enum.TryParse<FederationCategory>(dto.Category, out var categoryEnum))
                    return BadRequest(new { detail = "دسته‌بندی نامعتبر است" });
                if (dto.ParentFederationId.HasValue || dto.ProvinceId.HasValue || dto.CityId.HasValue)
                    return BadRequest(new { detail = "فدراسیون کشوری نباید والد، استان یا شهر داشته باشد" });

                var duplicate = await _db.Federations.AnyAsync(f => f.Level == FederationLevel.National && f.Name == dto.Name);
                if (duplicate) return Conflict(new { detail = "فدراسیون کشوری با این نام قبلاً ثبت شده است" });

                var federation = new Federation { Name = dto.Name, Category = categoryEnum, Level = FederationLevel.National };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            if (levelEnum == FederationLevel.Provincial)
            {
                if (!dto.ParentFederationId.HasValue || !dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "انتخاب فدراسیون کشوری و استان الزامی است" });
                if (dto.CityId.HasValue)
                    return BadRequest(new { detail = "فدراسیون استانی نباید شهر داشته باشد" });

                if (!CanManageProvincial(dto.ProvinceId.Value))
                    return Forbid("شما اجازه ایجاد فدراسیون برای این استان را ندارید");

                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.National)
                    return BadRequest(new { detail = "والد انتخاب‌شده باید یک فدراسیون کشوری معتبر باشد" });

                if (!await _db.Provinces.AnyAsync(p => p.Id == dto.ProvinceId.Value))
                    return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.ProvinceId == dto.ProvinceId);
                if (duplicate) return Conflict(new { detail = "فدراسیون این استان قبلاً برای این رشته ثبت شده است" });

                // Category is never taken from the client for non-National
                // levels — it's always inherited directly from the parent,
                // guaranteeing the match rather than merely validating it.
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
                    return BadRequest(new { detail = "انتخاب فدراسیون استانی و شهر الزامی است" });
                if (dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "فدراسیون شهری نباید استان مستقیم داشته باشد" });

                var city = await _db.Cities.FindAsync(dto.CityId.Value);
                if (city == null) return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" });

                if (!CanManageCity(dto.CityId.Value, city.ProvinceId))
                    return Forbid("شما اجازه ایجاد فدراسیون برای این شهر را ندارید");

                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.Provincial)
                    return BadRequest(new { detail = "والد انتخاب‌شده باید یک فدراسیون استانی معتبر باشد" });

                if (city.ProvinceId != parent.ProvinceId)
                    return BadRequest(new { detail = "شهر انتخاب‌شده متعلق به استان فدراسیون والد نیست" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.CityId == dto.CityId);
                if (duplicate) return Conflict(new { detail = "فدراسیون این شهر قبلاً برای این رشته ثبت شده است" });

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
            if (federation == null) return NotFound(new { detail = "فدراسیون یافت نشد" });

            if (federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فقط فدراسیون کشوری قابل ویرایش نام است" });

            if (!CanManageNational())
                return Forbid("شما اجازه ویرایش این فدراسیون را ندارید");

            federation.Name = dto.Name;
            await _db.SaveChangesAsync();
            return Ok(new { success = true });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var federation = await _db.Federations
                .Include(f => f.Province).Include(f => f.City)
                .FirstOrDefaultAsync(f => f.Id == id);
            if (federation == null) return NotFound(new { detail = "فدراسیون یافت نشد" });

            var allowed = federation.Level switch
            {
                FederationLevel.National => CanManageNational(),
                FederationLevel.Provincial => CanManageProvincial(federation.ProvinceId!.Value),
                FederationLevel.City => CanManageCity(federation.CityId!.Value, federation.City!.ProvinceId),
                _ => false,
            };
            if (!allowed) return Forbid("شما اجازه حذف این فدراسیون را ندارید");

            try
            {
                _db.Federations.Remove(federation);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "این فدراسیون دارای زیرمجموعه یا اطلاعات وابسته است و قابل حذف نیست" });
            }

            return Ok(new { success = true });
        }

        [HttpGet("{id}/positions")]
        public async Task<IActionResult> GetPositions(Guid id)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Id == id);
            if (!exists) return NotFound(new { detail = "فدراسیون یافت نشد" });

            var positions = await _db.FederationPositions
                .Include(p => p.User).Include(p => p.Federation)
                .Where(p => p.FederationId == id)
                .OrderByDescending(p => p.StartDate)
                .Select(p => new FederationPositionOutDto(
                    p.Id, p.Position.ToString(), p.StartDate, p.EndDate,
                    p.UserId, p.User.FirstName!, p.User.LastName!,
                    p.FederationId, p.Federation.Name ?? "")) // Name can be null for Provincial/City now — fall back to empty string
                .ToListAsync();

            return Ok(positions);
        }
    }
}
