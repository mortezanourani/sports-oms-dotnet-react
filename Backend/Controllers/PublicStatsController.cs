using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/public")]
    // No [Authorize] anywhere on this controller at all — this is the one
    // controller in the whole backend where that's intentional and correct,
    // not an oversight. Contrast with Facilities/Champions/AthleteCount,
    // which are Authorize by default on their List/Get actions.
    public class PublicStatsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PublicStatsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET /api/public/facilities/stats
        // Returns counts grouped by type + province + city — exactly the
        // shape a chart/table on a public stats page would consume directly,
        // without the frontend needing to do its own aggregation client-side.
        [HttpGet("facilities/stats")]
        public async Task<IActionResult> FacilityStats(string? provinceName)
        {
            var qs = _db.Facilities
                .Include(f => f.City).ThenInclude(c => c.Province)
                .AsQueryable();

            if (!string.IsNullOrEmpty(provinceName))
                qs = qs.Where(f => f.City.Province.Name == provinceName);

            var stats = await qs
                .GroupBy(f => new { f.FacilityType, ProvinceName = f.City.Province.Name, CityName = f.City.Name })
                .Select(g => new PublicFacilityStatDto(
                    g.Key.FacilityType.ToString(), g.Key.ProvinceName, g.Key.CityName, g.Count()))
                .ToListAsync();

            return Ok(stats);
        }

        // GET /api/public/champions
        // A public "hall of fame" style listing — every champion with their
        // medal counts, since this is exactly the kind of content meant to
        // be showcased publicly, unlike facility ownership details.
        [HttpGet("champions")]
        public async Task<IActionResult> Champions(Guid? federationId, int page = 1, int pageSize = 20)
        {
            var qs = _db.Champions
                .Include(c => c.Federation)
                .Include(c => c.City)
                .Include(c => c.Medals)
                .AsQueryable();

            if (federationId.HasValue)
                qs = qs.Where(c => c.FederationId == federationId.Value);

            var count = await qs.CountAsync();
            var items = await qs
                .OrderByDescending(c => c.Medals.Count) // most decorated champions surface first
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new PublicChampionDto(
                    c.Id, c.FullName, c.Gender.ToString(), c.Federation.Name,
                    c.City != null ? c.City.Name : null,
                    c.PhotoPath != null ? $"/files/{c.PhotoPath}" : null,
                    c.Medals.Count,
                    c.Medals.Sum(m => m.MedalType == Models.MedalType.Gold ? m.Count : 0),
                    c.Medals.Sum(m => m.MedalType == Models.MedalType.Silver ? m.Count : 0),
                    c.Medals.Sum(m => m.MedalType == Models.MedalType.Bronze ? m.Count : 0)))
                .ToListAsync();

            return Ok(new { items, count });
        }

        // GET /api/public/athletes/stats
        // Same aggregate-shape principle as facility stats — grouped counts
        // ready for direct charting, not raw per-record data.
        [HttpGet("athletes/stats")]
        public async Task<IActionResult> AthleteStats(int? year, string? provinceName)
        {
            var qs = _db.AthleteCounts
                .Include(a => a.Federation)
                .Include(a => a.City).ThenInclude(c => c.Province)
                .AsQueryable();

            if (year.HasValue) qs = qs.Where(a => a.Year == year.Value);
            if (!string.IsNullOrEmpty(provinceName)) qs = qs.Where(a => a.City.Province.Name == provinceName);

            var stats = await qs
                .Select(a => new PublicAthleteStatDto(
                    a.Federation.Name, a.City.Province.Name, a.City.Name,
                    a.Year, a.Month, a.Gender.ToString(), a.Count))
                .ToListAsync();

            return Ok(stats);
        }

        // GET /api/public/federations
        // A simple lookup list, needed so the public pages can offer a
        // "filter by federation" dropdown without requiring login just to
        // see which federations exist.
        [HttpGet("federations")]
        public async Task<IActionResult> Federations()
        {
            var federations = await _db.Federations
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, f.Name, f.SportType })
                .ToListAsync();

            return Ok(federations);
        }
    }
}
