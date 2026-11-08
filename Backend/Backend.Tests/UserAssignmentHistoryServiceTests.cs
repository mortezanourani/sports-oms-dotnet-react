using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Xunit;

namespace Backend.Tests;

public class UserAssignmentHistoryServiceTests
{
    [Fact]
    public async Task RecordChange_ClosesPreviousAssignmentAndStartsNextOne()
    {
        await using var db = CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.HumanResourceOfficer,
            ScopeLevel = ScopeLevel.Province
        };
        var creatorId = Guid.NewGuid();
        var changerId = Guid.NewGuid();
        db.Users.Add(user);

        var service = new UserAssignmentHistoryService(db);
        service.AddInitialAssignment(user, creatorId);
        await db.SaveChangesAsync();

        user.Role = UserRole.HumanResourceManager;
        user.ScopeLevel = ScopeLevel.City;
        await service.RecordChangeAsync(
            user, UserRole.HumanResourceOfficer, ScopeLevel.Province, null, null, changerId);
        await db.SaveChangesAsync();

        var history = await db.UserAssignmentHistories
            .Where(entry => entry.UserId == user.Id)
            .OrderBy(entry => entry.StartedAtUtc)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.Equal(UserRole.HumanResourceOfficer, history[0].Role);
        Assert.NotNull(history[0].EndedAtUtc);
        Assert.Equal(creatorId, history[0].ChangedByUserId);
        Assert.Equal(UserRole.HumanResourceManager, history[1].Role);
        Assert.Equal(ScopeLevel.City, history[1].ScopeLevel);
        Assert.Null(history[1].EndedAtUtc);
        Assert.Equal(changerId, history[1].ChangedByUserId);
    }

    [Fact]
    public async Task RecordChange_CreatesUnknownDateLegacyEntryWhenNoHistoryExists()
    {
        await using var db = CreateDbContext();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Manager,
            ScopeLevel = ScopeLevel.Mastery
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        user.Role = UserRole.Admin;
        var service = new UserAssignmentHistoryService(db);
        await service.RecordChangeAsync(
            user, UserRole.Manager, ScopeLevel.Mastery, null, null, Guid.NewGuid());
        await db.SaveChangesAsync();

        var history = await db.UserAssignmentHistories
            .Where(entry => entry.UserId == user.Id)
            .OrderBy(entry => entry.StartedAtUtc)
            .ToListAsync();

        Assert.Equal(2, history.Count);
        Assert.True(history[0].IsLegacy);
        Assert.Null(history[0].StartedAtUtc);
        Assert.NotNull(history[0].EndedAtUtc);
        Assert.False(history[1].IsLegacy);
        Assert.Equal(UserRole.Admin, history[1].Role);
        Assert.NotNull(history[1].StartedAtUtc);
        Assert.Null(history[1].EndedAtUtc);
    }

    [Fact]
    public async Task AddInitialAssignment_PersistsWithNewUserInOneSave()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var db = new ApplicationDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Role = UserRole.Admin,
            ScopeLevel = ScopeLevel.Mastery
        };
        var service = new UserAssignmentHistoryService(db);
        service.AddInitialAssignment(user, Guid.NewGuid());
        db.Users.Add(user);

        await db.SaveChangesAsync();

        var history = await db.UserAssignmentHistories.SingleAsync(entry => entry.UserId == user.Id);
        Assert.Equal(UserRole.Admin, history.Role);
        Assert.NotNull(history.StartedAtUtc);
        Assert.Null(history.EndedAtUtc);
    }

    private static ApplicationDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
