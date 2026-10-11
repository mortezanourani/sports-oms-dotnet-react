using Backend.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace Backend.Tests;

public class DbSeederTests
{
    [Theory]
    [InlineData(null, null)]
    [InlineData("admin", null)]
    [InlineData(null, "password")]
    public async Task SeedAdminAsync_ProductionRequiresBothBootstrapCredentials(string? username, string? password)
    {
        var config = Configuration(username, password);
        using var services = new ServiceCollection().BuildServiceProvider();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbSeeder.SeedAdminAsync(services, config, new TestHostEnvironment(Environments.Production)));

        Assert.Contains("SeedAdmin:Username and SeedAdmin:Password", exception.Message);
    }

    [Fact]
    public async Task SeedAdminAsync_DevelopmentSkipsSeedingWhenBothCredentialsAreAbsent()
    {
        var config = Configuration(null, null);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        await DbSeeder.SeedAdminAsync(services, config, new TestHostEnvironment(Environments.Development));
    }

    [Fact]
    public async Task SeedAdminAsync_DevelopmentRejectsIncompleteCredentials()
    {
        var config = Configuration("admin", null);
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DbSeeder.SeedAdminAsync(services, config, new TestHostEnvironment(Environments.Development)));
    }

    private static IConfiguration Configuration(string? username, string? password) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SeedAdmin:Username"] = username,
                ["SeedAdmin:Password"] = password
            })
            .Build();

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;
        public string ApplicationName { get; set; } = nameof(Backend.Tests);
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
