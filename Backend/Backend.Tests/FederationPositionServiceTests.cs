using Backend.Data;
using Backend.Models;
using Backend.Services;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class FederationPositionServiceTests
{
    [Fact]
    public void TehranDateTime_ConvertsLocalMidnightToUtc()
    {
        Assert.Equal(
            new DateTime(2024, 4, 2, 20, 30, 0, DateTimeKind.Utc),
            TehranDateTime.ToUtc(new DateOnly(2024, 4, 3)));
    }

    [Fact]
    public async Task AssignAsync_CreatesPositionWithCalculatedOneYearTerm()
    {
        await using var context = CreateContext();
        var service = new FederationPositionService(context);
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var federation = new Federation { Id = Guid.NewGuid(), Level = FederationLevel.National };

        var (position, error) = await service.AssignAsync(
            user, federation, FederationPositionType.President, TehranDate(2025, 1, 1), null);

        Assert.Null(error);
        Assert.NotNull(position);
        Assert.Equal(TehranDate(2026, 1, 1), position.EndDate);
        Assert.Same(position, await context.FederationPositions.SingleAsync());
    }

    [Theory]
    [InlineData(2025, 1, 1, 2024, 12, 31)]
    [InlineData(2025, 1, 1, 2026, 1, 2)]
    public async Task AssignAsync_RejectsEndDateOutsideOneYearTerm(
        int startYear, int startMonth, int startDay, int endYear, int endMonth, int endDay)
    {
        await using var context = CreateContext();
        var service = new FederationPositionService(context);

        var (position, error) = await service.AssignAsync(
            new ApplicationUser { Id = Guid.NewGuid() },
            new Federation { Id = Guid.NewGuid() },
            FederationPositionType.President,
            TehranDate(startYear, startMonth, startDay),
            TehranDate(endYear, endMonth, endDay));

        Assert.Null(position);
        Assert.NotNull(error);
        Assert.Empty(await context.FederationPositions.ToListAsync());
    }

    [Fact]
    public async Task AssignAsync_RejectsSamePositionWithinOneYear()
    {
        await using var context = CreateContext();
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var federation = new Federation { Id = Guid.NewGuid() };
        context.FederationPositions.Add(new FederationPosition
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FederationId = federation.Id,
            Position = FederationPositionType.President,
            StartDate = TehranDate(2024, 6, 1),
            EndDate = TehranDate(2025, 6, 1)
        });
        await context.SaveChangesAsync();

        var (position, error) = await new FederationPositionService(context).AssignAsync(
            user, federation, FederationPositionType.President, TehranDate(2025, 5, 31), null);

        Assert.Null(position);
        Assert.Contains("یک سال گذشته", error);
        Assert.Single(await context.FederationPositions.ToListAsync());
    }

    [Fact]
    public async Task AssignAsync_RejectsOverlappingDifferentPosition()
    {
        await using var context = CreateContext();
        var user = new ApplicationUser { Id = Guid.NewGuid() };
        var federation = new Federation { Id = Guid.NewGuid() };
        context.FederationPositions.Add(new FederationPosition
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            FederationId = federation.Id,
            Position = FederationPositionType.President,
            StartDate = TehranDate(2024, 1, 1),
            EndDate = TehranDate(2024, 12, 31)
        });
        await context.SaveChangesAsync();

        var (position, error) = await new FederationPositionService(context).AssignAsync(
            user, federation, FederationPositionType.Miss, TehranDate(2024, 6, 1), null);

        Assert.Null(position);
        Assert.Contains("سمت دیگری", error);
        Assert.Single(await context.FederationPositions.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static DateTime TehranDate(int year, int month, int day) =>
        TehranDateTime.ToUtc(new DateOnly(year, month, day));
}
