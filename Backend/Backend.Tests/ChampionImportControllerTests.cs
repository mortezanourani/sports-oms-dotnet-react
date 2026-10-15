using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class ChampionImportControllerTests
{
    [Fact]
    public async Task Import_CreatesChampionAndMedalForValidRow()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var controller = CreateController(context, MasteryPrincipal());
        var file = CreateImportFile(ValidRow("قهرمان نمونه", "طلا"));

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));
        var counts = ReadCounts(result);

        Assert.Equal(1, counts.CreatedChampions);
        Assert.Equal(1, counts.CreatedMedals);
        Assert.Equal(1, await context.Champions.CountAsync());
        var medal = await context.Medals.SingleAsync();
        Assert.Equal(MedalType.Gold, medal.MedalType);
        Assert.Equal(scope.Federation.Id, (await context.Champions.SingleAsync()).FederationId);
        Assert.Equal(new DateOnly(2024, 4, 3), (await context.Champions.SingleAsync()).BirthDate);
    }

    [Fact]
    public async Task Import_CreatesChampionAndSkipsMedalWhenMedalDataIsInvalid()
    {
        await using var context = CreateContext();
        await AddScopeDataAsync(context);
        var controller = CreateController(context, MasteryPrincipal());
        var row = ValidRow("قهرمان بدون مدال", "مدال نامعتبر");
        row[7] = "رده نامعتبر";
        var file = CreateImportFile(row);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));
        var counts = ReadCounts(result);

        Assert.Equal(1, counts.CreatedChampions);
        Assert.Equal(0, counts.CreatedMedals);
        Assert.Equal(1, counts.SkippedInvalid);
        Assert.Single(await context.Champions.ToListAsync());
        Assert.Empty(await context.Medals.ToListAsync());
    }

    [Fact]
    public async Task Import_SkipsDuplicateChampionAndMedalRows()
    {
        await using var context = CreateContext();
        await AddScopeDataAsync(context);
        var controller = CreateController(context, MasteryPrincipal());
        var row = ValidRow("قهرمان تکراری", "طلا");
        var file = CreateImportFile(row, row);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));
        var counts = ReadCounts(result);

        Assert.Equal(1, counts.CreatedChampions);
        Assert.Equal(1, counts.ExistingChampions);
        Assert.Equal(1, counts.CreatedMedals);
        Assert.Equal(1, counts.SkippedExistingMedals);
        Assert.Single(await context.Champions.ToListAsync());
        Assert.Single(await context.Medals.ToListAsync());
    }

    [Fact]
    public async Task Import_SkipsRowsOutsideUploaderProvinceScope()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        var otherCity = new City
        {
            Id = Guid.NewGuid(),
            Name = "شهر دیگر",
            ProvinceId = otherProvince.Id
        };
        context.Provinces.Add(otherProvince);
        context.Cities.Add(otherCity);
        await context.SaveChangesAsync();

        var controller = CreateController(context, ProvincePrincipal(scope.Province.Id));
        var row = ValidRow("قهرمان خارج از محدوده", "طلا");
        row[4] = otherProvince.Name;
        row[5] = otherCity.Name;
        var file = CreateImportFile(row);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));
        var counts = ReadCounts(result);

        Assert.Equal(1, counts.SkippedOutOfScope);
        Assert.Equal(0, counts.CreatedChampions);
        Assert.Equal(0, counts.CreatedMedals);
        Assert.Empty(await context.Champions.ToListAsync());
        Assert.Empty(await context.Medals.ToListAsync());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Province Province, City City, Federation Federation)> AddScopeDataAsync(
        ApplicationDbContext context)
    {
        var province = new Province { Id = Guid.NewGuid(), Name = "استان نمونه" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر نمونه", ProvinceId = province.Id };
        var federation = new Federation
        {
            Id = Guid.NewGuid(),
            Name = "فدراسیون نمونه",
            Level = FederationLevel.National
        };
        context.AddRange(province, city, federation);
        await context.SaveChangesAsync();
        return (province, city, federation);
    }

    private static ChampionsController CreateController(
        ApplicationDbContext context,
        ClaimsPrincipal principal)
    {
        var controller = new ChampionsController(context, new ScopeService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
        return controller;
    }

    private static ClaimsPrincipal MasteryPrincipal() => Principal(
        new Claim(AuthClaimTypes.Role, "Admin"),
        new Claim(AuthClaimTypes.ScopeLevel, "Mastery"));

    private static ClaimsPrincipal ProvincePrincipal(Guid provinceId) => Principal(
        new Claim(AuthClaimTypes.Role, "SportsOfficer"),
        new Claim(AuthClaimTypes.ScopeLevel, "Province"),
        new Claim(AuthClaimTypes.ScopeProvinceId, provinceId.ToString()));

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, authenticationType: "test"));

    private static string[] ValidRow(string fullName, string medalType) =>
    [
        fullName, "زن", "۱۴۰۳/۰۱/۱۵", "فدراسیون نمونه",
        "استان نمونه", "شهر نمونه", medalType, "بزرگسالان", "کشوری",
        "۱۴۰۳", "۲", "مسابقات نمونه", "شنا"
    ];

    private static IFormFile CreateImportFile(params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("قهرمانان و مدال‌ها");
            var headers = new[]
            {
                "نام کامل", "جنسیت", "تاریخ تولد", "فدراسیون", "استان", "شهرستان",
                "مدال", "رده سنی", "سطح مسابقات", "سال", "تعداد", "عنوان مسابقات", "رشته ورزشی"
            };
            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];

            for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                worksheet.Cell(row + 2, column + 1).Value = rows[row][column];

            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "champions-import.xlsx");
    }

    private static ImportCounts ReadCounts(OkObjectResult result)
    {
        var value = Assert.IsType<ChampionImportResultDto>(result.Value);
        return new ImportCounts(
            value.CreatedChampions,
            value.ExistingChampions,
            value.CreatedMedals,
            value.SkippedExistingMedals,
            value.SkippedInvalid,
            value.SkippedOutOfScope);
    }

    private sealed record ImportCounts(
        int CreatedChampions,
        int ExistingChampions,
        int CreatedMedals,
        int SkippedExistingMedals,
        int SkippedInvalid,
        int SkippedOutOfScope);
}
