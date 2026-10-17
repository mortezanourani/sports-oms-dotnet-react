using System.Reflection;
using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class PrivateFacilityImportControllerTests
{
    [Fact]
    public void Template_IncludesLicenseColumns()
    {
        using var stream = new MemoryStream(PrivateFacilityImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);
        var headers = workbook.Worksheets.First().Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Contains("شماره مجوز", headers);
        Assert.Contains("نام دارنده مجوز", headers);
        Assert.Contains("تاریخ صدور مجوز", headers);
        Assert.Contains("تاریخ انقضای مجوز", headers);
    }

    [Fact]
    public void ImportAndTemplate_AreRestrictedToPrivateFacilityImportRoles()
    {
        var roles = "Admin,Manager,M5Manager,M5Officer";
        var importAuthorization = typeof(FacilitiesController)
            .GetMethod(nameof(FacilitiesController.ImportPrivateFacilities))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var templateAuthorization = typeof(FacilitiesController)
            .GetMethod(nameof(FacilitiesController.DownloadPrivateFacilityImportTemplate))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal(roles, importAuthorization?.Roles);
        Assert.Equal(roles, templateAuthorization?.Roles);
    }

    [Fact]
    public async Task Import_CreatesFacilityWithoutInvalidLicense()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var controller = CreateController(context, Principal("M5Officer", "City", cityId: scope.City.Id));
        var file = CreateImportFile(ValidRow("باشگاه نمونه", "مجوز نامعتبر"));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportPrivateFacilities(file));
        var counts = Assert.IsType<PrivateFacilityImportResultDto>(result.Value);

        Assert.Equal(1, counts.CreatedFacilities);
        Assert.Equal(0, counts.CreatedLicenses);
        Assert.Equal(1, counts.SkippedInvalidLicenses);
        Assert.Single(await context.Facilities.ToListAsync());
        Assert.Empty(await context.FacilityLicenses.ToListAsync());
    }

    [Fact]
    public async Task Import_SkipsLicenseWithExpiryBeforeIssueButCreatesFacility()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var row = ValidRow("باشگاه بدون مجوز معتبر", "1403/01/15");
        row[10] = "1402/01/15";
        var controller = CreateController(context, Principal("M5Officer", "City", cityId: scope.City.Id));

        var result = Assert.IsType<OkObjectResult>(
            await controller.ImportPrivateFacilities(CreateImportFile(row)));
        var counts = Assert.IsType<PrivateFacilityImportResultDto>(result.Value);

        Assert.Equal(1, counts.CreatedFacilities);
        Assert.Equal(1, counts.SkippedInvalidLicenses);
        Assert.Empty(await context.FacilityLicenses.ToListAsync());
    }

    [Fact]
    public async Task Import_AddsValidLicenseToExistingFacilityWithoutCreatingAnotherFacility()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        context.Facilities.Add(new Facility
        {
            FacilityType = FacilityType.Private,
            Name = "باشگاه  نمونه",
            CityId = scope.City.Id
        });
        await context.SaveChangesAsync();
        var controller = CreateController(context, Principal("M5Manager", "Province", scope.Province.Id));
        var file = CreateImportFile(ValidRow(" باشگاه نمونه ", "1403/01/15"));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportPrivateFacilities(file));
        var counts = Assert.IsType<PrivateFacilityImportResultDto>(result.Value);

        Assert.Equal(0, counts.CreatedFacilities);
        Assert.Equal(1, counts.ExistingFacilities);
        Assert.Equal(1, counts.CreatedLicenses);
        Assert.Single(await context.Facilities.ToListAsync());
        var license = await context.FacilityLicenses.SingleAsync();
        Assert.Equal("LIC-1", license.LicenseNumber);
        Assert.Equal(new DateOnly(2024, 4, 3), license.IssueDate);
    }

    [Fact]
    public async Task Import_RestrictsProvinceAndCityUsersToTheirAssignedScopes()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var otherProvince = new Province { Name = "استان دیگر" };
        var otherCity = new City { Name = "شهر دیگر", Province = otherProvince };
        context.AddRange(otherProvince, otherCity);
        await context.SaveChangesAsync();

        var provinceController = CreateController(
            context, Principal("M5Officer", "Province", scope.Province.Id));
        var provinceResult = Assert.IsType<OkObjectResult>(await provinceController.ImportPrivateFacilities(
            CreateImportFile(
                ValidRow("باشگاه داخل استان", "1403/01/15"),
                ValidRow("باشگاه خارج استان", "1403/01/15", otherProvince.Name, otherCity.Name))));
        var provinceCounts = Assert.IsType<PrivateFacilityImportResultDto>(provinceResult.Value);

        Assert.Equal(1, provinceCounts.CreatedFacilities);
        Assert.Equal(1, provinceCounts.SkippedOutOfScope);

        var cityController = CreateController(context, Principal("M5Officer", "City", cityId: scope.City.Id));
        var cityResult = Assert.IsType<OkObjectResult>(await cityController.ImportPrivateFacilities(
            CreateImportFile(
                ValidRow("باشگاه شهر خودم", "1403/01/15"),
                ValidRow("باشگاه شهر دیگر", "1403/01/15", otherProvince.Name, otherCity.Name))));
        var cityCounts = Assert.IsType<PrivateFacilityImportResultDto>(cityResult.Value);

        Assert.Equal(1, cityCounts.CreatedFacilities);
        Assert.Equal(1, cityCounts.SkippedOutOfScope);
        Assert.Equal(2, await context.Facilities.CountAsync());
    }

    [Fact]
    public async Task Import_RejectsNonPrivateImportRoles()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Principal("M88Officer", "Mastery"));

        var result = await controller.ImportPrivateFacilities(CreateImportFile());

        Assert.IsType<ForbidResult>(result);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task<(Province Province, City City)> AddScopeDataAsync(ApplicationDbContext context)
    {
        var province = new Province { Name = "استان نمونه" };
        var city = new City { Name = "شهر نمونه", Province = province };
        context.AddRange(province, city);
        await context.SaveChangesAsync();
        return (province, city);
    }

    private static FacilitiesController CreateController(
        ApplicationDbContext context,
        ClaimsPrincipal principal) =>
        new(context, new ScopeService(context))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = principal }
            }
        };

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

    private static string[] ValidRow(
        string name,
        string issueDate,
        string provinceName = "استان نمونه",
        string cityName = "شهر نمونه") =>
    [
        provinceName, cityName, name, "آدرس", "شنا", "مالک", "تماس",
        "LIC-1", "دارنده مجوز", issueDate, ""
    ];

    private static IFormFile CreateImportFile(params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("اماکن خصوصی");
            var headers = new[]
            {
                "استان", "شهر", "نام مکان", "آدرس", "نوع ورزش", "نام مالک", "تماس مالک",
                "شماره مجوز", "نام دارنده مجوز", "تاریخ صدور مجوز", "تاریخ انقضای مجوز"
            };
            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];
            for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                worksheet.Cell(row + 2, column + 1).Value = rows[row][column];
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "private-facilities-import.xlsx");
    }
}
