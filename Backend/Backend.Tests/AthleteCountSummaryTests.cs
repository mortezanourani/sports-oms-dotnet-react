using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class AthleteCountSummaryTests
{
    [Fact]
    public async Task CreateAndUpdate_StoreBothGenderCountsInOneRecord()
    {
        await using var context = CreateContext();
        var province = new Province { Id = Guid.NewGuid(), Name = "استان نمونه" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر نمونه", Province = province };
        var federation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون نمونه",
            Level = FederationLevel.National
        };
        context.AddRange(province, city, federation);
        await context.SaveChangesAsync();

        var controller = CreateController(context);
        var createResult = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(new AthleteCountCreateDto(
                federation.Id, city.Id, 1405, 7, 18, 12)));
        var recordId = Assert.IsType<Guid>(createResult.RouteValues!["id"]);

        var created = await context.AthleteCounts.SingleAsync();
        Assert.Equal(18, created.MaleCount);
        Assert.Equal(12, created.FemaleCount);

        Assert.IsType<OkObjectResult>(
            await controller.Update(recordId, new AthleteCountUpdateDto(21, 15)));
        var updated = await context.AthleteCounts.SingleAsync();
        Assert.Equal(21, updated.MaleCount);
        Assert.Equal(15, updated.FemaleCount);
    }

    [Fact]
    public async Task Summary_AggregatesTheLastTwelveMonthsAndAppliesLocationFilters()
    {
        await using var context = CreateContext();
        var province = new Province { Id = Guid.NewGuid(), Name = "استان نمونه" };
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        var firstCity = new City { Id = Guid.NewGuid(), Name = "شهر اول", Province = province };
        var secondCity = new City { Id = Guid.NewGuid(), Name = "شهر دوم", Province = province };
        var otherCity = new City { Id = Guid.NewGuid(), Name = "شهر دیگر", Province = otherProvince };
        var firstFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون اول",
            Level = FederationLevel.National
        };
        var secondFederation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون دوم",
            Level = FederationLevel.National
        };
        context.AddRange(
            province, otherProvince, firstCity, secondCity, otherCity,
            firstFederation, secondFederation);

        var calendar = new System.Globalization.PersianCalendar();
        var tehranToday = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Tehran"));
        var currentMonth = calendar.ToDateTime(
            calendar.GetYear(tehranToday), calendar.GetMonth(tehranToday), 1, 0, 0, 0, 0);
        var previousMonth = calendar.AddMonths(currentMonth, -1);
        var outsideWindowMonth = calendar.AddMonths(currentMonth, -12);
        context.AthleteCounts.AddRange(
            CreateCount(firstFederation, firstCity, calendar.GetYear(currentMonth), calendar.GetMonth(currentMonth), 5, 3),
            CreateCount(firstFederation, secondCity, calendar.GetYear(previousMonth), calendar.GetMonth(previousMonth), 7, 0),
            CreateCount(firstFederation, firstCity, calendar.GetYear(outsideWindowMonth), calendar.GetMonth(outsideWindowMonth), 20, 0),
            CreateCount(secondFederation, otherCity, calendar.GetYear(currentMonth), calendar.GetMonth(currentMonth), 9, 0));
        await context.SaveChangesAsync();

        var controller = CreateController(context);

        var provinceResult = Assert.IsType<OkObjectResult>(
            await controller.Summary(province.Id, null));
        var provinceSummary = Assert.IsAssignableFrom<IEnumerable<AthleteCountFederationSummaryDto>>(
            provinceResult.Value).ToDictionary(item => item.FederationId);

        Assert.Equal(12, provinceSummary[firstFederation.Id].MaleCount);
        Assert.Equal(3, provinceSummary[firstFederation.Id].FemaleCount);
        Assert.Equal(15, provinceSummary[firstFederation.Id].TotalCount);
        Assert.Equal(0, provinceSummary[secondFederation.Id].TotalCount);

        var cityResult = Assert.IsType<OkObjectResult>(
            await controller.Summary(province.Id, firstCity.Id));
        var citySummary = Assert.IsAssignableFrom<IEnumerable<AthleteCountFederationSummaryDto>>(
            cityResult.Value).ToDictionary(item => item.FederationId);

        Assert.Equal(5, citySummary[firstFederation.Id].MaleCount);
        Assert.Equal(3, citySummary[firstFederation.Id].FemaleCount);
        Assert.Equal(8, citySummary[firstFederation.Id].TotalCount);

        var selectedYearResult = Assert.IsType<OkObjectResult>(
            await controller.Summary(province.Id, firstCity.Id, calendar.GetYear(currentMonth)));
        var selectedYearSummary = Assert.IsAssignableFrom<IEnumerable<AthleteCountFederationSummaryDto>>(
            selectedYearResult.Value).ToDictionary(item => item.FederationId);
        Assert.Equal(8, selectedYearSummary[firstFederation.Id].TotalCount);

        var selectedMonthResult = Assert.IsType<OkObjectResult>(
            await controller.Summary(
                province.Id, firstCity.Id,
                calendar.GetYear(currentMonth), calendar.GetMonth(currentMonth)));
        var selectedMonthSummary = Assert.IsAssignableFrom<IEnumerable<AthleteCountFederationSummaryDto>>(
            selectedMonthResult.Value).ToDictionary(item => item.FederationId);
        Assert.Equal(5, selectedMonthSummary[firstFederation.Id].MaleCount);
        Assert.Equal(3, selectedMonthSummary[firstFederation.Id].FemaleCount);
        Assert.NotNull(selectedMonthSummary[firstFederation.Id].RecordId);

        var optionsResult = Assert.IsType<OkObjectResult>(
            await controller.SummaryFilters(
                province.Id, firstCity.Id, calendar.GetYear(currentMonth)));
        var options = Assert.IsType<AthleteCountSummaryFiltersDto>(optionsResult.Value);
        Assert.Contains(calendar.GetYear(currentMonth), options.Years);
        Assert.Equal([calendar.GetMonth(currentMonth)], options.Months);
    }

    private static AthleteCount CreateCount(
        Federation federation, City city, int year, int month, int maleCount, int femaleCount) => new()
    {
        Federation = federation,
        FederationId = federation.Id,
        City = city,
        CityId = city.Id,
        Year = year,
        Month = month,
        MaleCount = maleCount,
        FemaleCount = femaleCount
    };

    private static AthleteCountsController CreateController(ApplicationDbContext context) =>
        new(context, new ScopeService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = MasteryPrincipal() }
            }
        };

    private static ClaimsPrincipal MasteryPrincipal() => new(
        new ClaimsIdentity(
        [
            new Claim(AuthClaimTypes.Role, "Admin"),
            new Claim(AuthClaimTypes.ScopeLevel, "Mastery")
        ],
        authenticationType: "test"));

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
