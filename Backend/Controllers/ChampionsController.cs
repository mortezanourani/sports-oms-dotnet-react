using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;

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

        [HttpGet]
        public async Task<IActionResult> List(Guid? federationId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .AsQueryable();

            if (federationId.HasValue)
                qs = qs.Where(c => c.FederationId == federationId.Value);

            qs = _scope.FilterChampions(qs, User);

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
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Create(ChampionCreateDto dto)
        {
            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده باید یک فدراسیون کشوری باشد" });

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
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Update(Guid id, ChampionUpdateDto dto)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federation = await _db.Federations.FindAsync(dto.FederationId);
            if (federation == null || federation.Level != FederationLevel.National)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده باید یک فدراسیون کشوری باشد" });

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
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            _db.Champions.Remove(champion); // cascades to Medals (default EF behavior, required FK)
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("{championId}/medals")]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> AddMedal(Guid championId, MedalCreateDto dto)
        {
            var champion = await _db.Champions.FindAsync(championId);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

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
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> DeleteMedal(Guid championId, Guid medalId)
        {
            var medal = await _db.Medals.FirstOrDefaultAsync(m => m.Id == medalId && m.ChampionId == championId);
            if (medal == null)
                return NotFound(new { detail = "مدال یافت نشد" });

            _db.Medals.Remove(medal);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }
}
