using Backend.Data;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace Backend.Tests;

public class ScopeServiceTests
{
    [Fact]
    public void FilterFacilities_WhenScopeClaimIsMissing_FailsClosed()
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new ScopeService(context);
        var facilities = new[] { new Backend.Models.Facility() }.AsQueryable();

        var result = service.FilterFacilities(facilities, Principal());

        Assert.Empty(result);
    }

    [Fact]
    public async Task CheckCityAllowedAsync_WhenScopeClaimIsInvalid_RejectsAccess()
    {
        using var context = new ApplicationDbContext(new DbContextOptions<ApplicationDbContext>());
        var service = new ScopeService(context);

        var exception = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.CheckCityAllowedAsync(Principal(), Guid.NewGuid()));

        Assert.Contains("scope is invalid", exception.Message);
    }

    [Fact]
    public void FilterFacilities_ProvinceScopeReturnsOnlyFacilitiesInProvince()
    {
        var provinceId = Guid.NewGuid();
        var otherProvinceId = Guid.NewGuid();
        var facilities = new[]
        {
            FacilityInCity(provinceId),
            FacilityInCity(otherProvinceId)
        }.AsQueryable();

        var result = new ScopeService(CreateContext())
            .FilterFacilities(facilities, Principal("Province", provinceId));

        Assert.Single(result);
        Assert.Equal(provinceId, result.Single().City.ProvinceId);
    }

    [Fact]
    public void FilterChampions_CityScopeReturnsOnlyChampionsInCity()
    {
        var cityId = Guid.NewGuid();
        var champions = new[]
        {
            ChampionInCity(cityId),
            ChampionInCity(Guid.NewGuid()),
            new Champion { Id = Guid.NewGuid(), CityId = null }
        }.AsQueryable();

        var result = new ScopeService(CreateContext())
            .FilterChampions(champions, Principal("City", cityId));

        Assert.Single(result);
        Assert.Equal(cityId, result.Single().CityId);
    }

    [Fact]
    public void FilterAthleteCounts_ProvinceScopeReturnsOnlyCountsInProvince()
    {
        var provinceId = Guid.NewGuid();
        var otherProvinceId = Guid.NewGuid();
        var counts = new[]
        {
            AthleteCountInCity(provinceId),
            AthleteCountInCity(otherProvinceId)
        }.AsQueryable();

        var result = new ScopeService(CreateContext())
            .FilterAthleteCounts(counts, Principal("Province", provinceId));

        Assert.Single(result);
        Assert.Equal(provinceId, result.Single().City.ProvinceId);
    }

    private static ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().Options);

    private static Facility FacilityInCity(Guid provinceId) => new()
    {
        Id = Guid.NewGuid(),
        City = new City { Id = Guid.NewGuid(), ProvinceId = provinceId }
    };

    private static Champion ChampionInCity(Guid cityId) => new()
    {
        Id = Guid.NewGuid(),
        CityId = cityId
    };

    private static AthleteCount AthleteCountInCity(Guid provinceId) => new()
    {
        Id = Guid.NewGuid(),
        City = new City { Id = Guid.NewGuid(), ProvinceId = provinceId }
    };

    private static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.Role, "InsuranceOfficer")
        }, authenticationType: "test"));

    private static ClaimsPrincipal Principal(string scopeLevel, Guid scopeId) =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.Role, "SportsOfficer"),
            new Claim(AuthClaimTypes.ScopeLevel, scopeLevel),
            new Claim(scopeLevel == "Province" ? AuthClaimTypes.ScopeProvinceId : AuthClaimTypes.ScopeCityId,
                scopeId.ToString())
        }, authenticationType: "test"));
}
