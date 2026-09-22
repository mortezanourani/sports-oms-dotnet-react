using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/public")]
    public class PublicStatsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public PublicStatsController(ApplicationDbContext db) => _db = db;

        [HttpGet("facilities/stats")]
        public async Task<IActionResult> FacilityStats(string? provinceName)
        {
            var qs = _db.Facilities.Include(f => f.City).ThenInclude(c => c.Province).AsQueryable();

            if (!string.IsNullOrEmpty(provinceName))
                qs = qs.Where(f => f.City.Province.Name == provinceName);

            var stats = await qs
                .GroupBy(f => new { f.FacilityType, ProvinceName = f.City.Province.Name, CityName = f.City.Name })
                .Select(g => new PublicFacilityStatDto(
                    g.Key.FacilityType.ToString(), g.Key.ProvinceName, g.Key.CityName, g.Count()))
                .ToListAsync();

            return Ok(stats);
        }

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
                .OrderByDescending(c => c.Medals.Sum(m => m.Count))
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new PublicChampionDto(
                    c.Id, c.FullName, c.Gender.ToString(), c.Federation.Name!,
                    c.City != null ? c.City.Name : null,
                    c.PhotoPath != null ? $"/files/{c.PhotoPath}" : null,
                    c.Medals.Sum(m => m.Count),
                    c.Medals.Where(m => m.MedalType == MedalType.Gold).Sum(m => m.Count),
                    c.Medals.Where(m => m.MedalType == MedalType.Silver).Sum(m => m.Count),
                    c.Medals.Where(m => m.MedalType == MedalType.Bronze).Sum(m => m.Count)))
                .ToListAsync();

            return Ok(new { items, count });
        }

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
                    a.Federation.Name!, a.City.Province.Name, a.City.Name,
                    a.Year, a.Month, a.Gender.ToString(), a.Count))
                .ToListAsync();

            return Ok(stats);
        }

        // Filtered to National level only — Province/City federations have
        // no Name of their own and aren't meaningful choices for a public
        // "filter by federation" dropdown anyway.
        [HttpGet("federations")]
        public async Task<IActionResult> Federations()
        {
            var federations = await _db.Federations
                .Where(f => f.Level == FederationLevel.National)
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, f.Name, Category = f.Category.ToString() })
                .ToListAsync();

            return Ok(federations);
        }

        [HttpGet("provinces")]
        public async Task<IActionResult> Provinces()
        {
            var provinces = await _db.Provinces
                .OrderBy(p => p.Name).Select(p => new { p.Id, p.Name })
                .ToListAsync();

            return Ok(provinces);
        }
    }
}
