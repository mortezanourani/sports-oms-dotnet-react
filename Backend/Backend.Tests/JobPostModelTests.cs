using System.ComponentModel.DataAnnotations;
using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class JobPostModelTests
{
    [Fact]
    public void JobPostHasRequiredReferencesAndFieldsWithNullableBreakOut()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new ApplicationDbContext(options);

        var entity = context.Model.FindEntityType(typeof(JobPost))!;
        var relationships = entity.GetForeignKeys().ToDictionary(
            relationship => relationship.Properties.Single().Name);
        var indexes = entity.GetIndexes();

        Assert.False(entity.FindProperty(nameof(JobPost.TypeCode))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(JobPost.Title))!.IsNullable);
        Assert.False(entity.FindProperty(nameof(JobPost.ManagerType))!.IsNullable);
        Assert.True(entity.FindProperty(nameof(JobPost.BreakOut))!.IsNullable);
        Assert.Equal(typeof(string), entity.FindProperty(nameof(JobPost.ManagerType))!.GetProviderClrType());
        Assert.True(relationships[nameof(JobPost.JobPostTypeId)].IsRequired);
        Assert.True(relationships[nameof(JobPost.OrgUnitId)].IsRequired);
        Assert.All(relationships.Values, relationship =>
            Assert.Equal(DeleteBehavior.Restrict, relationship.DeleteBehavior));
        Assert.Contains(indexes, index =>
            index.IsUnique && index.Properties.Single().Name == nameof(JobPost.Title));
        Assert.DoesNotContain(indexes, index =>
            index.IsUnique && index.Properties.Single().Name == nameof(JobPost.TypeCode));
    }

    [Fact]
    public void ManagerTypeOptionsHavePersianDisplayLabels()
    {
        Assert.Equal("نامشخص", GetDisplayName(JobPostManagerType.Unspecified));
        Assert.Equal("مدیر پایه", GetDisplayName(JobPostManagerType.BasicManager));
        Assert.Equal("مدیر میانی", GetDisplayName(JobPostManagerType.MiddleManager));
        Assert.Equal("مدیر ارشد", GetDisplayName(JobPostManagerType.SeniorManager));
    }

    private static string? GetDisplayName(JobPostManagerType value) =>
        typeof(JobPostManagerType)
            .GetMember(value.ToString())[0]
            .GetCustomAttributes(typeof(DisplayAttribute), false)
            .Cast<DisplayAttribute>()
            .Single()
            .Name;
}
