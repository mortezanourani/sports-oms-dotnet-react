using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;
using System.ComponentModel.DataAnnotations;

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

    [Fact]
    public void OrgUnitTypeAndScopeAreRequiredEnumsStoredByName()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new ApplicationDbContext(options);

        var entity = context.Model.FindEntityType(typeof(OrgUnit))!;
        var type = entity.FindProperty(nameof(OrgUnit.Type))!;
        var scope = entity.FindProperty(nameof(OrgUnit.Scope))!;

        Assert.False(type.IsNullable);
        Assert.Equal(typeof(string), type.GetProviderClrType());
        Assert.False(scope.IsNullable);
        Assert.Equal(typeof(string), scope.GetProviderClrType());
        Assert.Equal("اداره کل", typeof(OrgUnitType)
            .GetMember(nameof(OrgUnitType.GeneralAdministration))[0]
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .Single()
            .Name);
        Assert.Equal("Provincial (استانی)", typeof(OrgUnitScope)
            .GetMember(nameof(OrgUnitScope.Provincial))[0]
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .Single()
            .Name);
    }
}
