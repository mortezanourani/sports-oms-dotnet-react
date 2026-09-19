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
        private readonly FileStorageService _fileStorage;
        public ChampionsController(ApplicationDbContext db, ScopeService scope, FileStorageService fileStorage)
        {
            _db = db;
            _scope = scope;
            _fileStorage = fileStorage;
        }

        // GET /api/champions
        // Open to any authenticated user — the dashboard's "view" side has no
        // role restriction anywhere in this system, only "write" actions do.
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
                    c.FederationId, c.Federation.Name,
                    c.CityId, c.City != null ? c.City.Name : null,
                    c.Medals.Count))
                .ToListAsync();

            return Ok(new { items, count });
        }

        // GET /api/champions/{id}
        // Returns the full medal history — this is the detail/edit page's endpoint.
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
                champion.FederationId, champion.Federation.Name,
                champion.CityId, champion.City?.ProvinceId, champion.City?.Name, champion.City?.Province.Name,
                champion.PhotoPath, champion.Medals
                    .OrderByDescending(m => m.Year)
                    .Select(m => new MedalOutDto(m.Id, m.MedalType.ToString(), m.AgeGroup.ToString(),
                            m.Level.ToString(), m.CompetitionName, m.Year, m.Count, m.Field))
                    .ToList()));
        }

        // POST /api/champions
        // Restricted to Admin, Manager, SportOfficer — matches the role table's
        // "Sport officer: can add/update/delete champions and their medals."
        [HttpPost]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Create(ChampionCreateDto dto)
        {
            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federationExists = await _db.Federations.AnyAsync(f => f.Id == dto.FederationId);
            if (!federationExists)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده معتبر نیست" });

            if (dto.CityId.HasValue)
            {
                try { await _scope.CheckCityAllowedAsync(User, dto.CityId.Value); }
                catch (UnauthorizedAccessException ex) { return Forbid(ex.Message); }
                catch (KeyNotFoundException) { return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" }); }
            }
            // Deliberate decision: a champion created with no CityId is allowed
            // through, even for a scoped officer — leaving city blank means
            // "unknown," which isn't the same as trying to claim jurisdiction
            // over a city outside the officer's scope. The scope check only
            // fires when a specific city IS provided.

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

        // PUT /api/champions/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Update(Guid id, ChampionUpdateDto dto)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            if (!Enum.TryParse<Gender>(dto.Gender, out var genderEnum))
                return BadRequest(new { detail = "جنسیت نامعتبر است" });

            var federationExists = await _db.Federations.AnyAsync(f => f.Id == dto.FederationId);
            if (!federationExists)
                return BadRequest(new { detail = "فدراسیون انتخاب‌شده معتبر نیست" });

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

        // DELETE /api/champions/{id}
        // No try/catch around a Restrict-based FK failure needed here — unlike
        // Province/City/Federation, nothing else in the system points *at* a
        // Champion as a required foreign key except Medal, and Medal's FK
        // uses Cascade by EF Core's default (required FK, no explicit
        // override configured) — meaning deleting a champion correctly and
        // intentionally deletes their medals along with them, which is the
        // desired behavior here: a medal has no meaning without its champion.
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null)
                return NotFound(new { detail = "قهرمان یافت نشد" });

            _db.Champions.Remove(champion);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [HttpPost("{id}/photo")]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> UploadPhoto(Guid id, IFormFile file)
        {
            var champion = await _db.Champions.FindAsync(id);
            if (champion == null) return NotFound(new { detail = "قهرمان یافت نشد" });

            var (success, relativePath, error) = await _fileStorage.SaveAsync(file, "champions", isPublic: true, isImage: true);
            if (!success) return BadRequest(new { detail = error });

            // If a photo already existed, delete the old physical file so uploads
            // don't accumulate orphaned files on disk every time someone replaces
            // a champion's photo.
            if (!string.IsNullOrEmpty(champion.PhotoPath))
                _fileStorage.Delete(champion.PhotoPath);

            champion.PhotoPath = relativePath;
            await _db.SaveChangesAsync();

            return Ok(new { photoUrl = $"/files/{relativePath}" });
        }


        // POST /api/champions/{championId}/medals
        // Nested under the champion's own route — this is what makes the
        // parent-child relationship explicit in the API's shape, not just in
        // the database schema.
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
                return BadRequest(new { detail = "تعداد مدال باید حداقل 1 باشد" });

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

        // DELETE /api/champions/{championId}/medals/{medalId}
        // Scoped under the champion's ID too, not just the medal's ID — this
        // guards against a subtle bug: without checking championId matches,
        // someone could pass any valid medalId in the URL regardless of which
        // champion it actually belongs to, and it would still delete
        // successfully. Requiring both IDs to match confirms the medal
        // actually belongs to the champion the caller thinks they're editing.
        [HttpDelete("{championId}/medals/{medalId}")]
        [Authorize(Roles = "Admin,Manager,SportOfficer")]
        public async Task<IActionResult> DeleteMedal(Guid championId, Guid medalId)
        {
            var medal = await _db.Medals
                .FirstOrDefaultAsync(m => m.Id == medalId && m.ChampionId == championId);

            if (medal == null)
                return NotFound(new { detail = "مدال یافت نشد" });

            _db.Medals.Remove(medal);
            await _db.SaveChangesAsync();

            return Ok(new { success = true });
        }
    }

}
