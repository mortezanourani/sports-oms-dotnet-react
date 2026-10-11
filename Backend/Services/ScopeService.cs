using Backend.Data;
using Backend.Extensions;
using Backend.Models;
using System.Security.Claims;

namespace Backend.Services
{
    public class ScopeService
    {
        private readonly ApplicationDbContext _db;
        public ScopeService(ApplicationDbContext db) => _db = db;

        public IQueryable<Facility> FilterFacilities(IQueryable<Facility> querySet, ClaimsPrincipal user)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "City")
            {
                var cityId = user.ScopeCityId();
                return cityId.HasValue
                    ? querySet.Where(f => f.CityId == cityId.Value)
                    : querySet.Where(_ => false);
            }

            if (scopeLevel == "Province")
            {
                var provinceId = user.ScopeProvinceId();
                return provinceId.HasValue
                    ? querySet.Where(f => f.City.ProvinceId == provinceId.Value)
                    : querySet.Where(_ => false);
            }

            return scopeLevel == "Mastery" ? querySet : querySet.Where(_ => false);
        }

        public IQueryable<Champion> FilterChampions(IQueryable<Champion> querySet, ClaimsPrincipal user)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "City")
            {
                var cityId = user.ScopeCityId();
                return cityId.HasValue
                    ? querySet.Where(c => c.CityId == cityId.Value)
                    : querySet.Where(_ => false);
            }
            if (scopeLevel == "Province")
            {
                var provinceId = user.ScopeProvinceId();
                return provinceId.HasValue
                    ? querySet.Where(c => c.City != null && c.City.ProvinceId == provinceId.Value)
                    : querySet.Where(_ => false);
            }
            return scopeLevel == "Mastery"
                ? querySet // Mastery — no restriction, includes champions with no city set
                : querySet.Where(_ => false);
        }

        public IQueryable<AthleteCount> FilterAthleteCounts(IQueryable<AthleteCount> querySet, ClaimsPrincipal user)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "City")
            {
                var cityId = user.ScopeCityId();
                return cityId.HasValue
                    ? querySet.Where(a => a.CityId == cityId.Value)
                    : querySet.Where(_ => false);
            }
            if (scopeLevel == "Province")
            {
                var provinceId = user.ScopeProvinceId();
                return provinceId.HasValue
                    ? querySet.Where(a => a.City.ProvinceId == provinceId.Value)
                    : querySet.Where(_ => false);
            }
            return scopeLevel == "Mastery" ? querySet : querySet.Where(_ => false);
        }

        public async Task CheckCityAllowedAsync(ClaimsPrincipal user, Guid cityId)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "Mastery") return;

            if (scopeLevel == "City" && !user.ScopeCityId().HasValue ||
                scopeLevel == "Province" && !user.ScopeProvinceId().HasValue ||
                scopeLevel is not ("City" or "Province"))
                throw new UnauthorizedAccessException("The authenticated user's geographic scope is invalid.");

            var city = await _db.Cities.FindAsync(cityId)
                ?? throw new KeyNotFoundException("City not found");

            if (scopeLevel == "City" && city.Id != user.ScopeCityId())
                throw new UnauthorizedAccessException("شما فقط به داده های شهر خود دسترسی دارید");

            if (scopeLevel == "Province" && city.ProvinceId != user.ScopeProvinceId())
                throw new UnauthorizedAccessException("شما فقط به داده های استان خود دسترسی دارید");
        }
    }
}
