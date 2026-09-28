using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using Backend.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/champions")]
    [Authorize]
    public class ChampionsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly ScopeService _scope;

        public ChampionsController(ApplicationDbContext db, ScopeService scope)
        {
            _db = db;
            _scope = scope;
        }

        // Helper methods for permission checks
        private bool IsAdminOrManager() => User.Role() is "Admin" or "Manager" or "SportsManager";
        private bool IsSportsOfficer() => User.Role() == "SportsOfficer";

        private bool HasChampionPermission() => User.Role() is "Admin" or "Manager" or "SportsManager" or "SportsOfficer";
        private bool HasChampionDeletePermission() => User.Role() is "Admin" or "Manager" or "SportsManager";

        private bool CanAccessChampion(Champion champion)
        {
            var role = User.Role();
            var myScopeLevel = User.ScopeLevel();

            // Admin, Manager, SportsManager have full access based on scope
            if (role is "Admin" or "Manager" or "SportsManager")
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    // Champion can be in province or city within province
                    return champion.Federation.ProvinceId == myProvinceId ||
                           (champion.City != null && champion.City.ProvinceId == myProvinceId);
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    // Champion can be in this city
                    return champion.CityId == myCityId;
                }
            }

            // SportsOfficer has same permissions as above
            if (role == "SportsOfficer")
            {
                if (myScopeLevel == "Mastery") return true;
                if (myScopeLevel == "Province")
                {
                    var myProvinceId = User.ScopeProvinceId();
                    return champion.Federation.ProvinceId == myProvinceId ||
                           (champion.City != null && champion.City.ProvinceId == myProvinceId);
                }
                if (myScopeLevel == "City")
                {
                    var myCityId = User.ScopeCityId();
                    return champion.CityId == myCityId;
                }
            }

            return false;
        }

        [HttpGet]
        public async Task<IActionResult> List(Guid? federationId, Guid? provinceId, Guid? cityId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .AsQueryable();

            if (federationId.HasValue)
                qs = qs.Where(c => c.FederationId == federationId.Value);

            if (cityId.HasValue) qs = qs.Where(c => c.CityId == cityId.Value);
            else if (provinceId.HasValue) qs = qs.Where(c => c.City != null && c.City.ProvinceId == provinceId.Value);

            // Apply scope filtering based on role
            var role = User.Role();
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();
            var myCityId = User.ScopeCityId();

            if (role is "Admin" or "Manager" or "SportsManager" or "SportsOfficer")
            {
                if (myScopeLevel == "Province")
                {
                    qs = qs.Where(c =>
                        (c.Federation.Level == FederationLevel.Provincial && c.Federation.ProvinceId == myProvinceId) ||
                        (c.City != null && c.City.ProvinceId == myProvinceId));
                }
                else if (myScopeLevel == "City")
                {
                    qs = qs.Where(c => c.CityId == myCityId);
                }
                // Mastery has no filter (can see all)
            }

            var count = await qs.CountAsync();
            var items = await qs
                .OrderBy(c => c.FullName)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new ChampionListOutDto(
                    c.Id, c.FullName, c.BirthDate, c.Gender.ToString(),
                    c.FederationId, c.Federation.Name!, // National federations always have a Name
                    c.CityId, c.City != null ? c.City.Name : null,
                    c.Medals.Sum(m => m.Count))) // sums actual medal Count, not row count
                .ToListAsync();

            return Ok(new { items, count });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var champion = await _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه مشاهده این قهرمان را ندارید");

            return Ok(new ChampionDetailOutDto(
                champion.Id, champion.FullName, champion.BirthDate, champion.Gender.ToString(),
                champion.FederationId, champion.Federation.Name!,
                champion.CityId, champion.City?.ProvinceId, champion.City?.Name, champion.City?.Province?.Name,
                champion.PhotoPath, champion.Medals
                    .OrderByDescending(m => m.Year)
                    .Select(m => new MedalOutDto(
                        m.Id, m.MedalType.ToString(), m.AgeGroup.ToString(), m.Level.ToString(),
                        m.CompetitionName, m.Year, m.Count, m.Field))
                    .ToList()));
        }

        [HttpPost]
        public async Task<IActionResult> Create(ChampionCreateDto dto)
        {
            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده باید یک فدراسیون کشوری باشد" });

            // Check if user has permission for this federation's scope
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();

            if (myScopeLevel == "Province" && federation.ProvinceId != myProvinceId)
                return Forbid("شما اجازه ایجاد قهرمان برای این فدراسیون را ندارید");

            if (dto.CityId.HasValue)
            {
                try { await _scope.CheckCityAllowedAsync(User, dto.CityId.Value); }
                catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
                catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }
            }

            var champion = new Champion
            {
                FullName = dto.FullName,
                BirthDate = dto.BirthDate,
                Gender = genderEnum,
                FederationId = dto.FederationId,
                CityId = dto.CityId,
                PhotoPath = dto.PhotoPath
            };

            _db.Champions.Add(champion);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = champion.Id }, new { id = champion.Id });
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(Guid id, ChampionUpdateDto dto)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه ویرایش این قهرمان را ندارید");

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فadorasیون انتخاب‌شده باید یک فadorasیون کشوری باشد" });

            // Verify user can access the new federation scope
            var myScopeLevel = User.ScopeLevel();
            var myProvinceId = User.ScopeProvinceId();

            if (myScopeLevel == "Province" && federation.ProvinceId != myProvinceId)
                return Forbid("شما اجازه ویرایش قهرمان برای این فadorasیون را ندارید");

            if (dto.CityId.HasValue)
            {
                try { await _scope.CheckCityAllowedAsync(User, dto.CityId.Value); }
                catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
                catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }
            }

            champion.FullName = dto.FullName;
            champion.BirthDate = dto.BirthDate;
            champion.Gender = genderEnum;
            champion.FederationId = dto.FederationId;
            champion.CityId = dto.CityId;
            champion.PhotoPath = dto.PhotoPath;
            champion.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { id = champion.Id });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه حذف این قهرمان را ندارید");

            if (!HasChampionDeletePermission())
                return Forbid("شما اجازه حذف قهرمان را ندارید");

            _db.Champions.Remove(champion); // cascades to Medals (default EF behavior, required FK)
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("{championId}/medals")]
        public async Task<IActionResult> AddMedal(Guid championId, MedalCreateDto dto)
        {
            var champion = await _db.Champions.FindAsync(championId);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!CanAccessChampion(champion))
                return Forbid("شما اجازه افزودن مدال به این قهرمان را ندارید");

            if (!Enum.TryParse<MedalType>(dto.MedalType, out var medalTypeEnum))
                return BadRequest(new { detail = "نوع مدال نامعتبر است" });
            if (!Enum.TryParse<AgeGroup>(dto.AgeGroup, out var ageGroupEnum))
                return BadRequest(new { detail = "گروه سنی نامعتبر است" });
            if (!Enum.TryParse<TournamentLevel>(dto.Level, out var levelEnum))
                return BadRequest(new { detail = "سطح مسابقه نامعتبر است" });
            if (dto.Count < 1)
                return BadRequest(new { detail = "تعداد مدال باید حداقل ۱ باشد" });

            var medal = new Medal
            {
                ChampionId = championId,
                MedalType = medalTypeEnum,
                AgeGroup = ageGroupEnum,
                Level = levelEnum,
                CompetitionName = dto.CompetitionName,
                Year = dto.Year,
                Count = dto.Count,
                Field = dto.Field
            };

            _db.Medals.Add(medal);
            await _db.SaveChangesAsync();

            return Ok(new MedalOutDto(medal.Id, medal.MedalType.ToString(), medal.AgeGroup.ToString(),
                medal.Level.ToString(), medal.CompetitionName, medal.Year, medal.Count, medal.Field));
        }

        [HttpDelete("{championId}/medals/{medalId}")]
        public async Task<IActionResult> DeleteMedal(Guid championId, Guid medalId)
        {
            var medal = await _db.Medals.FirstOrDefaultAsync(m => m.Id == medalId && m.ChampionId == championId);
            if (medal == null)
                return NotFound(new { detail = "مدال یافت نشد" });

            // Get champion to check access
            var champion = await _db.Champions.FindAsync(championId);
            if (champion == null || !CanAccessChampion(champion))
                return Forbid("شما اجازه حذف این مدال را ندارید");

            if (!HasChampionDeletePermission())
                return Forbid("شما اجازه حذف مدال را ندارید");

            _db.Medals.Remove(medal);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
