using Backend.Controllers;
using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Backend.Tests;

public class AuthControllerTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Refresh_RejectsExpiredOrRevokedTokens(bool expired, bool revoked)
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            UserName = $"invalid-refresh-{Guid.NewGuid():N}",
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };
        Assert.True((await fixture.UserManager.CreateAsync(user, "P@ssw0rd")).Succeeded);
        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString(),
            UserId = user.Id,
            ExpiresAt = expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddDays(1),
            Revoked = revoked
        };
        fixture.Db.RefreshTokens.Add(refreshToken);
        await fixture.Db.SaveChangesAsync();

        var result = await fixture.Controller.Refresh(new AuthController.RefreshRequest(refreshToken.Token));

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.True(await fixture.Db.RefreshTokens.AsNoTracking()
            .AnyAsync(token => token.Id == refreshToken.Id));
        Assert.Equal(1, await fixture.Db.RefreshTokens.CountAsync());
    }

    [Fact]
    public async Task Refresh_RotatesValidTokenAndRevokesTheOriginal()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            UserName = "refresh-user",
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };
        Assert.True((await fixture.UserManager.CreateAsync(user, "P@ssw0rd")).Succeeded);
        var original = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = "original-refresh-token",
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(1)
        };
        fixture.Db.RefreshTokens.Add(original);
        await fixture.Db.SaveChangesAsync();

        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Refresh(
            new AuthController.RefreshRequest(original.Token)));
        Assert.NotNull(result.Value);
        var responseType = result.Value.GetType();
        var accessToken = responseType.GetProperty("access")?.GetValue(result.Value) as string;
        var rotatedToken = responseType.GetProperty("refresh")?.GetValue(result.Value) as string;

        Assert.NotNull(accessToken);
        Assert.NotEmpty(accessToken);
        Assert.NotNull(rotatedToken);
        Assert.NotEqual(original.Token, rotatedToken);
        var storedOriginal = await fixture.Db.RefreshTokens.AsNoTracking()
            .SingleAsync(token => token.Id == original.Id);
        Assert.True(storedOriginal.Revoked);
        var rotated = await fixture.Db.RefreshTokens.SingleAsync(token => token.Token == rotatedToken);
        Assert.Equal(user.Id, rotated.UserId);
        Assert.False(rotated.Revoked);
        Assert.True(rotated.ExpiresAt > DateTime.UtcNow.AddDays(1).AddMinutes(-1));

        var replayResult = await fixture.Controller.Refresh(new AuthController.RefreshRequest(original.Token));
        Assert.IsType<UnauthorizedObjectResult>(replayResult);
    }

    [Fact]
    public async Task Login_WithInvalidPasswordDoesNotCreateRefreshToken()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            UserName = "login-user",
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };
        Assert.True((await fixture.UserManager.CreateAsync(user, "P@ssw0rd")).Succeeded);

        var result = await fixture.Controller.Login(new AuthController.LoginRequest("login-user", "wrong"));

        Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Empty(await fixture.Db.RefreshTokens.ToListAsync());
    }

    [Fact]
    public async Task Login_WithValidCredentialsReturnsTokensAndStoresRefreshToken()
    {
        await using var fixture = await AuthFixture.CreateAsync();
        var user = new ApplicationUser
        {
            UserName = "login-user",
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };
        Assert.True((await fixture.UserManager.CreateAsync(user, "P@ssw0rd")).Succeeded);

        var result = Assert.IsType<OkObjectResult>(await fixture.Controller.Login(
            new AuthController.LoginRequest(" login-user ", " P@ssw0rd ")));

        Assert.NotNull(result.Value);
        var responseType = result.Value.GetType();
        var accessToken = responseType.GetProperty("access")?.GetValue(result.Value) as string;
        var refreshToken = responseType.GetProperty("refresh")?.GetValue(result.Value) as string;
        Assert.False(string.IsNullOrWhiteSpace(accessToken));
        Assert.False(string.IsNullOrWhiteSpace(refreshToken));
        var stored = await fixture.Db.RefreshTokens.SingleAsync();
        Assert.Equal(refreshToken, stored.Token);
        Assert.Equal(user.Id, stored.UserId);
        Assert.False(stored.Revoked);
    }

    private sealed class AuthFixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly ServiceProvider _services;

        private AuthFixture(
            SqliteConnection connection,
            ServiceProvider services,
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuthController controller)
        {
            _connection = connection;
            _services = services;
            Db = db;
            UserManager = userManager;
            Controller = controller;
        }

        public ApplicationDbContext Db { get; }
        public UserManager<ApplicationUser> UserManager { get; }
        public AuthController Controller { get; }

        public static async Task<AuthFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthentication();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(connection));
            services.AddIdentityCore<ApplicationUser>()
                .AddRoles<IdentityRole<Guid>>()
                .AddEntityFrameworkStores<ApplicationDbContext>()
                .AddSignInManager();
            var provider = services.BuildServiceProvider();
            var db = provider.GetRequiredService<ApplicationDbContext>();
            await db.Database.EnsureCreatedAsync();
            var userManager = provider.GetRequiredService<UserManager<ApplicationUser>>();
            var signInManager = provider.GetRequiredService<SignInManager<ApplicationUser>>();
            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "0123456789abcdef0123456789abcdef",
                    ["Jwt:Issuer"] = "test-issuer",
                    ["Jwt:Audience"] = "test-audience",
                    ["Jwt:AccessTokenExpiryMinutes"] = "30",
                    ["Jwt:RefreshTokenExpirationDays"] = "1"
                })
                .Build();
            var controller = new AuthController(
                signInManager, userManager, new TokenService(config), db, config);
            return new AuthFixture(connection, provider, db, userManager, controller);
        }

        public async ValueTask DisposeAsync()
        {
            await Db.DisposeAsync();
            await _services.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
