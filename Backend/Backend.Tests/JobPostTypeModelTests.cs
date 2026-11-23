using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class JobPostTypeModelTests
{
    [Fact]
    public void JobPostTypeHasUniqueIdAndRequiredTitle()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new ApplicationDbContext(options);

        var entity = context.Model.FindEntityType(typeof(JobPostType))!;

        Assert.Equal("job_post_types", entity.GetTableName());
        Assert.Equal(nameof(JobPostType.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.False(entity.FindProperty(nameof(JobPostType.Title))!.IsNullable);
    }
}
