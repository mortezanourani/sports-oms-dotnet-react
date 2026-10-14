using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class FederationCategoryTests
{
    [Fact]
    public void FederationCategoryIsOptionalInTheDatabaseModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new ApplicationDbContext(options);

        var category = context.Model.FindEntityType(typeof(Federation))!
            .FindProperty(nameof(Federation.Category))!;

        Assert.True(category.IsNullable);
    }
}
