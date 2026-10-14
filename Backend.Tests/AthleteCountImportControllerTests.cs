using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class AthleteCountImportControllerTests
{
    [Fact]
    public async Task Import_CreatesMaleAndFemaleCountsForValidRow()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var controller = CreateController(context, Principal("Admin", "Mastery"));
        var file = CreateImportFile(
            ["فدراسیون نمونه", "استان نمونه", "شهر نمونه", "۱۴۰۳", "۲", "۱۲", "۸"]);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));

        Assert.NotNull(result.Value);
        var saved = await context.AthleteCounts.OrderBy(record => record.Gender).ToListAsync();
        Assert.Equal(2, saved.Count);
        Assert.All(saved, record => Assert.Equal(scope.Federation.Id, record.FederationId));
        Assert.All(saved, record => Assert.Equal(scope.City.Id, record.CityId));
        Assert.Contains(saved, record => record.Gender == Gender.Male && record.Count == 12);
        Assert.Contains(saved, record => record.Gender == Gender.Female && record.Count == 8);
    }

    [Fact]
    public async Task Import_SkipsInvalidCountRowWithoutPersistingRecords()
    {
        await using var context = CreateContext();
        await AddScopeDataAsync(context);
        var controller = CreateController(context, Principal("Admin", "Mastery"));
        var file = CreateImportFile(
            ["فدراسیون نمونه", "استان نمونه", "شهر نمونه", "۱۴۰۳", "۲", "نامعتبر", "۸"]);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));

        Assert.NotNull(result.Value);
        Assert.Empty(await context.AthleteCounts.ToListAsync());
    }

    [Fact]
    public async Task Import_SkipsValidRowsOutsideProvinceScope()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        var otherCity = new City { Id = Guid.NewGuid(), Name = "شهر دیگر", ProvinceId = otherProvince.Id };
        context.AddRange(otherProvince, otherCity);
        await context.SaveChangesAsync();
        var controller = CreateController(context, Principal("Admin", "Province", scope.Province.Id));
        var file = CreateImportFile(
            ["فدراسیون نمونه", otherProvince.Name, otherCity.Name, "۱۴۰۳", "۲", "۱۲", "۸"]);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));

        Assert.NotNull(result.Value);
        Assert.Empty(await context.AthleteCounts.ToListAsync());
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

    private static AthleteCountsController CreateController(
        ApplicationDbContext context,
        ClaimsPrincipal principal)
    {
        return new AthleteCountsController(context, new ScopeService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };
    }

    private static ClaimsPrincipal Principal(
        string role,
        string scope,
        Guid? provinceId = null,
        Guid? cityId = null)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Role, role),
            new(AuthClaimTypes.ScopeLevel, scope)
        };
        if (provinceId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeProvinceId, provinceId.Value.ToString()));
        if (cityId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeCityId, cityId.Value.ToString()));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    private static IFormFile CreateImportFile(string[] row)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("ورزشکاران");
            var headers = new[] { "فدراسیون", "استان", "شهرستان", "سال", "ماه", "آقایان", "بانوان" };
            for (var column = 0; column < headers.Length; column++)
            {
                worksheet.Cell(1, column + 1).Value = headers[column];
                worksheet.Cell(2, column + 1).Value = row[column];
            }
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "athlete-counts-import.xlsx");
    }
}
