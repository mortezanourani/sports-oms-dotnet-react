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
                return querySet.Where(f => f.CityId == cityId);
            }

            if (scopeLevel == "Province")
            {
                var provinceId = user.ScopeProvinceId();
                return querySet.Where(f => f.City.ProvinceId == provinceId);
            }
            return querySet;
        }

        public IQueryable<Champion> FilterChampions(IQueryable<Champion> querySet, ClaimsPrincipal user)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "City")
            {
                var cityId = user.ScopeCityId();
                return querySet.Where(c => c.CityId == cityId);
            }
            if (scopeLevel == "Province")
            {
                var provinceId = user.ScopeProvinceId();
                return querySet.Where(c => c.City != null && c.City.ProvinceId == provinceId);
            }
            return querySet; // National — no restriction, includes champions with no city set
        }

        public async Task CheckCityAllowedAsync(ClaimsPrincipal user, Guid cityId)
        {
            var scopeLevel = user.ScopeLevel();
            if (scopeLevel == "National") return;

            var city = await _db.Cities.FindAsync(cityId)
                ?? throw new KeyNotFoundException("City not found");

            if (scopeLevel == "City" && city.Id != user.ScopeCityId())
                throw new UnauthorizedAccessException("شما فقط به داده های شهر خود دسترسی دارید");

            if (scopeLevel == "Province" && city.ProvinceId != user.ScopeProvinceId())
                throw new UnauthorizedAccessException("شما فقط به داده های استان خود دسترسی دارید");
        }
    }
}
