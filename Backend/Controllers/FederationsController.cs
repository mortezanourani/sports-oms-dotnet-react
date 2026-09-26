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
        private bool CanManageMastery() => HasAllowedRole() && User.ScopeLevel() == "Mastery";

        // Can this user manage a Provincial federation for a given province?
        private bool CanManageProvincial(Guid targetProvinceId)
        {
            if (!HasAllowedRole()) return false;
            var scope = User.ScopeLevel();
            if (scope == "Mastery") return true;
            if (scope == "Province") return User.ScopeProvinceId() == targetProvinceId;
            return false; // City-scoped users cannot manage province-level entries
        }

        // Can this user manage a City federation for a given city/province?
        private bool CanManageCity(Guid targetCityId, Guid targetProvinceId)
        {
            if (!HasAllowedRole()) return false;
            var scope = User.ScopeLevel();
            if (scope == "Mastery") return true;
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
        public async Task<IActionResult> List(string? level, Guid? parentFederationId, Guid? provinceId, Guid? cityId)
        {
            var qs = _db.Federations
                .Include(f => f.ParentFederation).Include(f => f.Province).Include(f => f.City)
                .Include(f => f.ChildFederations)
                .AsQueryable();

            if (!string.IsNullOrEmpty(level))
            {
                if (!Enum.TryParse<FederationLevel>(level, out var levelEnum))
                    return BadRequest(new { detail = "??? ???????? ??????? ???" });
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

            if (f == null) return NotFound(new { detail = "???????? ???? ???" });

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
                return BadRequest(new { detail = "??? ???????? ??????? ???" });

            if (levelEnum == FederationLevel.National)
            {
                if (!CanManageMastery())
                    return Forbid("??? ????? ????? ???????? ????? ?? ??????");

                if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.Category))
                    return BadRequest(new { detail = "??? ? ????????? ???? ???????? ????? ?????? ???" });
                if (!Enum.TryParse<FederationCategory>(dto.Category, out var categoryEnum))
                    return BadRequest(new { detail = "????????? ??????? ???" });
                if (dto.ParentFederationId.HasValue || dto.ProvinceId.HasValue || dto.CityId.HasValue)
                    return BadRequest(new { detail = "???????? ????? ????? ????? ????? ?? ??? ????? ????" });

                var duplicate = await _db.Federations.AnyAsync(f => f.Level == FederationLevel.National && f.Name == dto.Name);
                if (duplicate) return Conflict(new { detail = "???????? ????? ?? ??? ??? ????? ??? ??? ???" });

                var federation = new Federation { Name = dto.Name, Category = categoryEnum, Level = FederationLevel.National };
                _db.Federations.Add(federation);
                await _db.SaveChangesAsync();
                return CreatedAtAction(nameof(Get), new { id = federation.Id }, new { id = federation.Id });
            }

            if (levelEnum == FederationLevel.Provincial)
            {
                if (!dto.ParentFederationId.HasValue || !dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "?????? ???????? ????? ? ????? ?????? ???" });
                if (dto.CityId.HasValue)
                    return BadRequest(new { detail = "???????? ?????? ????? ??? ????? ????" });

                if (!CanManageProvincial(dto.ProvinceId.Value))
                    return Forbid("??? ????? ????? ???????? ???? ??? ????? ?? ??????");

                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.National)
                    return BadRequest(new { detail = "???? ?????????? ???? ?? ???????? ????? ????? ????" });

                if (!await _db.Provinces.AnyAsync(p => p.Id == dto.ProvinceId.Value))
                    return BadRequest(new { detail = "????? ?????????? ????? ????" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.ProvinceId == dto.ProvinceId);
                if (duplicate) return Conflict(new { detail = "???????? ??? ????? ????? ???? ??? ???? ??? ??? ???" });

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
                    return BadRequest(new { detail = "?????? ???????? ?????? ? ??? ?????? ???" });
                if (dto.ProvinceId.HasValue)
                    return BadRequest(new { detail = "???????? ???? ????? ????? ?????? ????? ????" });

                var city = await _db.Cities.FindAsync(dto.CityId.Value);
                if (city == null) return BadRequest(new { detail = "??? ?????????? ????? ????" });

                if (!CanManageCity(dto.CityId.Value, city.ProvinceId))
                    return Forbid("??? ????? ????? ???????? ???? ??? ??? ?? ??????");

                var parent = await _db.Federations.FindAsync(dto.ParentFederationId.Value);
                if (parent == null || parent.Level != FederationLevel.Provincial)
                    return BadRequest(new { detail = "???? ?????????? ???? ?? ???????? ?????? ????? ????" });

                if (city.ProvinceId != parent.ProvinceId)
                    return BadRequest(new { detail = "??? ?????????? ????? ?? ????? ???????? ???? ????" });

                var duplicate = await _db.Federations.AnyAsync(f =>
                    f.ParentFederationId == dto.ParentFederationId && f.CityId == dto.CityId);
                if (duplicate) return Conflict(new { detail = "???????? ??? ??? ????? ???? ??? ???? ??? ??? ???" });

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
            if (federation == null) return NotFound(new { detail = "???????? ???? ???" });

            if (federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "??? ???????? ????? ???? ?????? ??? ???" });

            if (!CanManageMastery())
                return Forbid("??? ????? ?????? ??? ???????? ?? ??????");

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
            if (federation == null) return NotFound(new { detail = "???????? ???? ???" });

            var allowed = federation.Level switch
            {
                FederationLevel.National => CanManageMastery(),
                FederationLevel.Provincial => CanManageProvincial(federation.ProvinceId!.Value),
                FederationLevel.City => CanManageCity(federation.CityId!.Value, federation.City!.ProvinceId),
                _ => false,
            };
            if (!allowed) return Forbid("??? ????? ??? ??? ???????? ?? ??????");

            try
            {
                _db.Federations.Remove(federation);
                await _db.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                return Conflict(new { detail = "??? ???????? ????? ????????? ?? ??????? ?????? ??? ? ???? ??? ????" });
            }

            return Ok(new { success = true });
        }

        [HttpGet("{id}/positions")]
        public async Task<IActionResult> GetPositions(Guid id)
        {
            var exists = await _db.Federations.AnyAsync(f => f.Id == id);
            if (!exists) return NotFound(new { detail = "???????? ???? ???" });

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
