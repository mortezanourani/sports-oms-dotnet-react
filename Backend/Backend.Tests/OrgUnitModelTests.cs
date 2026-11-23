using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class OrgUnitModelTests
{
    [Fact]
    public void OrgUnitSupportsOptionalRestrictedParentAndUniqueTitle()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new ApplicationDbContext(options);

        var entity = context.Model.FindEntityType(typeof(OrgUnit))!;
        var parentRelationship = Assert.Single(entity.GetForeignKeys());
        var titleIndex = Assert.Single(
            entity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(OrgUnit.Title));

        Assert.Equal("org_units", entity.GetTableName());
        Assert.True(titleIndex.IsUnique);
        Assert.False(parentRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, parentRelationship.DeleteBehavior);
    }
}
