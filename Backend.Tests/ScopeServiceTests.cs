using Backend.Data;
using Backend.Extensions;
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

    private static ClaimsPrincipal Principal() =>
        new(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.Role, "InsuranceOfficer")
        }, authenticationType: "test"));
}
