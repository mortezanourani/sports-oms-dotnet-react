using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.Extensions.Configuration;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace Backend.Tests;

public class AuthClaimsTests
{
    [Fact]
    public void GenerateAccessToken_UsesSharedRoleAndScopeClaimNames()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "0123456789abcdef0123456789abcdef",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "30"
            })
            .Build();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "test-user",
            Role = UserRole.InsuranceOfficer,
            ScopeLevel = ScopeLevel.Province,
            ScopeProvinceId = Guid.NewGuid()
        };

        var encodedToken = new TokenService(config).GenerateAccessToken(user);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(encodedToken).Claims
            .ToDictionary(claim => claim.Type, claim => claim.Value);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims.Select(claim => new Claim(claim.Key, claim.Value)),
            authenticationType: "test",
            nameType: ClaimTypes.Name,
            roleType: AuthClaimTypes.Role));

        Assert.Equal(nameof(UserRole.InsuranceOfficer), principal.Role());
        Assert.True(principal.IsInRole(nameof(UserRole.InsuranceOfficer)));
        Assert.Equal(nameof(ScopeLevel.Province), principal.ScopeLevel());
        Assert.Equal(user.ScopeProvinceId, principal.ScopeProvinceId());
        Assert.Null(principal.ScopeCityId());
    }

    [Fact]
    public void GenerateAccessToken_WhenNamesAreMissing_UsesUsernameAsDisplayName()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "0123456789abcdef0123456789abcdef",
                ["Jwt:Issuer"] = "test-issuer",
                ["Jwt:Audience"] = "test-audience",
                ["Jwt:AccessTokenExpiryMinutes"] = "30"
            })
            .Build();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "fallback-user",
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };

        var claims = new JwtSecurityTokenHandler().ReadJwtToken(new TokenService(config).GenerateAccessToken(user)).Claims
            .ToDictionary(claim => claim.Type, claim => claim.Value);

        Assert.Equal("fallback-user", claims[AuthClaimTypes.FullName]);
    }

    [Fact]
    public void ScopeIdClaims_WhenMalformed_AreNotAccepted()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(AuthClaimTypes.ScopeProvinceId, "not-a-guid"),
            new Claim(AuthClaimTypes.ScopeCityId, "")
        }));

        Assert.Null(principal.ScopeProvinceId());
        Assert.Null(principal.ScopeCityId());
    }
}
