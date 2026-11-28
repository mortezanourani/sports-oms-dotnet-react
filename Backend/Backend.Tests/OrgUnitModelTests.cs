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
        var foreignKeys = entity.GetForeignKeys().ToArray();
        var parentRelationship = Assert.Single(
            foreignKeys,
            relationship => relationship.Properties.Single().Name == nameof(OrgUnit.ParentOrgUnitId));
        var provinceRelationship = Assert.Single(
            foreignKeys,
            relationship => relationship.Properties.Single().Name == nameof(OrgUnit.ProvinceId));
        var cityRelationship = Assert.Single(
            foreignKeys,
            relationship => relationship.Properties.Single().Name == nameof(OrgUnit.CityId));
        var titleIndex = Assert.Single(
            entity.GetIndexes(),
            index => index.Properties.Single().Name == nameof(OrgUnit.Title));

        Assert.Equal("org_units", entity.GetTableName());
        Assert.True(titleIndex.IsUnique);
        Assert.False(parentRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, parentRelationship.DeleteBehavior);
        Assert.True(provinceRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, provinceRelationship.DeleteBehavior);
        Assert.False(cityRelationship.IsRequired);
        Assert.Equal(DeleteBehavior.Restrict, cityRelationship.DeleteBehavior);
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
        Assert.Equal("استانی", typeof(OrgUnitScope)
            .GetMember(nameof(OrgUnitScope.Provincial))[0]
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .Single()
            .Name);
    }
}
