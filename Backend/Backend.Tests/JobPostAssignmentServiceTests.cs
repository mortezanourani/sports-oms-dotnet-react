using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class JobPostAssignmentServiceTests
{
    [Fact]
    public async Task AssignAsync_EndsPreviousAssignmentAndAllowsMultipleUsersAtSameLocation()
    {
        await using var db = CreateDbContext();
        var firstPost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Post one",
            PlacementScope = JobPostPlacementScope.Both
        };
        var secondPost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Post two",
            PlacementScope = JobPostPlacementScope.Both
        };
        var firstUser = new ApplicationUser { Id = Guid.NewGuid() };
        var secondUser = new ApplicationUser { Id = Guid.NewGuid() };
        db.JobPosts.AddRange(firstPost, secondPost);
        db.Users.AddRange(firstUser, secondUser);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(TehranDateTime.TodayUtc);
        var initialStart = today.AddDays(-10);
        var provinceId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var service = new JobPostAssignmentService(db);

        var first = await service.AssignAsync(
            firstPost, firstUser, provinceId, cityId, initialStart, Guid.NewGuid());
        var second = await service.AssignAsync(
            firstPost, secondUser, provinceId, cityId, initialStart, Guid.NewGuid());

        Assert.Null(first.Error);
        Assert.Null(second.Error);

        firstPost.Name = "Renamed post";
        await db.SaveChangesAsync();
        Assert.Equal("Post one", (await db.JobPostAssignments.SingleAsync(
            assignment => assignment.UserId == firstUser.Id)).JobPostName);

        var twoCurrentAssignees = await db.JobPostAssignments
            .Where(assignment =>
                assignment.JobPostId == firstPost.Id &&
                assignment.ProvinceId == provinceId &&
                assignment.CityId == cityId &&
                !assignment.EndedOn.HasValue)
            .CountAsync();
        Assert.Equal(2, twoCurrentAssignees);

        var replacementStart = today.AddDays(-2);
        var replacement = await service.AssignAsync(
            secondPost, firstUser, provinceId, null, replacementStart, Guid.NewGuid());
        Assert.Null(replacement.Error);

        var history = await db.JobPostAssignments
            .Where(assignment => assignment.UserId == firstUser.Id)
            .OrderBy(assignment => assignment.StartedOn)
            .ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(replacementStart.AddDays(-1), history[0].EndedOn);
        Assert.Equal(replacementStart, history[1].StartedOn);
        Assert.Null(history[1].EndedOn);

        var samePostAtSameLocation = await db.JobPostAssignments
            .Where(assignment =>
                assignment.JobPostId == firstPost.Id &&
                assignment.ProvinceId == provinceId &&
                assignment.CityId == cityId &&
                !assignment.EndedOn.HasValue)
            .CountAsync();
        Assert.Equal(1, samePostAtSameLocation);
        Assert.Equal(secondUser.Id, (await db.JobPostAssignments.SingleAsync(
            assignment => assignment.JobPostId == firstPost.Id && !assignment.EndedOn.HasValue)).UserId);
    }

    [Fact]
    public async Task Database_AllowsMultipleUsersPerPostLocationAndOnlyOneCurrentPostPerUser()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var province = new Province { Id = Guid.NewGuid(), Name = "Province" };
        var post = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Shared post",
            PlacementScope = JobPostPlacementScope.Both
        };
        var firstUser = new ApplicationUser { Id = Guid.NewGuid() };
        var secondUser = new ApplicationUser { Id = Guid.NewGuid() };
        db.AddRange(province, post, firstUser, secondUser);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(TehranDateTime.TodayUtc);
        db.JobPostAssignments.AddRange(
            CreateAssignment(post, firstUser, province, today),
            CreateAssignment(post, secondUser, province, today));
        await db.SaveChangesAsync();

        db.JobPostAssignments.Add(CreateAssignment(post, firstUser, province, today));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AssignAsync_CanReplaceCurrentAssignmentWithinUniqueIndex()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var province = new Province { Id = Guid.NewGuid(), Name = "Province" };
        var firstPost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "First post",
            PlacementScope = JobPostPlacementScope.Both
        };
        var nextPost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Next post",
            PlacementScope = JobPostPlacementScope.Both
        };
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        db.AddRange(province, firstPost, nextPost, user);
        await db.SaveChangesAsync();

        var today = DateOnly.FromDateTime(TehranDateTime.TodayUtc);
        var service = new JobPostAssignmentService(db);
        var first = await service.AssignAsync(
            firstPost, user, province.Id, null, today.AddDays(-2), Guid.NewGuid());
        var next = await service.AssignAsync(
            nextPost, user, province.Id, null, today, Guid.NewGuid());

        Assert.Null(first.Error);
        Assert.Null(next.Error);
        var assignments = await db.JobPostAssignments.OrderBy(item => item.StartedOn).ToListAsync();
        Assert.Equal(2, assignments.Count);
        Assert.Equal(today.AddDays(-1), assignments[0].EndedOn);
        Assert.Null(assignments[1].EndedOn);
    }

    [Fact]
    public async Task AssignAsync_AllowsProvincePostWithoutCity()
    {
        await using var db = CreateDbContext();
        var provincePost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Province post",
            PlacementScope = JobPostPlacementScope.Province,
            IsActive = true
        };
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        db.JobPosts.Add(provincePost);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var provinceId = Guid.NewGuid();
        var startedOn = DateOnly.FromDateTime(TehranDateTime.TodayUtc).AddDays(-1);
        var service = new JobPostAssignmentService(db);

        var result = await service.AssignAsync(
            provincePost, user, provinceId, null, startedOn, Guid.NewGuid());

        Assert.Null(result.Error);
        Assert.NotNull(result.Assignment);
        Assert.Equal(provinceId, result.Assignment.ProvinceId);
        Assert.Null(result.Assignment.CityId);
    }

    [Fact]
    public async Task AssignAsync_RejectsFutureStartAndUnsupportedPlacement()
    {
        await using var db = CreateDbContext();
        var provincePost = new JobPost
        {
            Id = Guid.NewGuid(),
            Name = "Province post",
            PlacementScope = JobPostPlacementScope.Province
        };
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        db.JobPosts.Add(provincePost);
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var service = new JobPostAssignmentService(db);
        var futureDate = DateOnly.FromDateTime(TehranDateTime.TodayUtc).AddDays(1);
        var result = await service.AssignAsync(
            provincePost, user, Guid.NewGuid(), Guid.NewGuid(), futureDate, Guid.NewGuid());

        Assert.Null(result.Assignment);
        Assert.NotNull(result.Error);

        var currentDateResult = await service.AssignAsync(
            provincePost,
            user,
            Guid.NewGuid(),
            Guid.NewGuid(),
            DateOnly.FromDateTime(TehranDateTime.TodayUtc),
            Guid.NewGuid());
        Assert.Null(currentDateResult.Assignment);
        Assert.NotNull(currentDateResult.Error);
        Assert.Empty(await db.JobPostAssignments.ToListAsync());
    }

    [Fact]
    public async Task EndAsync_RejectsEndDateBeforeStart()
    {
        await using var db = CreateDbContext();
        var assignment = new JobPostAssignment
        {
            Id = Guid.NewGuid(),
            JobPostId = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            ProvinceId = Guid.NewGuid(),
            StartedOn = DateOnly.FromDateTime(TehranDateTime.TodayUtc),
        };
        var service = new JobPostAssignmentService(db);

        var error = await service.EndAsync(assignment, assignment.StartedOn.AddDays(-1));

        Assert.NotNull(error);
        Assert.Null(assignment.EndedOn);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static JobPostAssignment CreateAssignment(
        JobPost post,
        ApplicationUser user,
        Province province,
        DateOnly startedOn) => new()
    {
        Id = Guid.NewGuid(),
        JobPost = post,
        User = user,
        Province = province,
        StartedOn = startedOn
    };
}
