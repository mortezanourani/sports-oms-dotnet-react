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

public class MsyGovFacilityImportControllerTests
{
    [Fact]
    public void ImportAndTemplate_AreRestrictedToMsyGovFacilityImportRoles()
    {
        const string roles = "Admin,Manager,M88Manager,M88Officer";
        var importAuthorization = typeof(FacilitiesController)
            .GetMethod(nameof(FacilitiesController.ImportMsyGovFacilities))!
            .GetCustomAttribute<AuthorizeAttribute>();
        var templateAuthorization = typeof(FacilitiesController)
            .GetMethod(nameof(FacilitiesController.DownloadMsyGovFacilityImportTemplate))!
            .GetCustomAttribute<AuthorizeAttribute>();

        Assert.Equal(roles, importAuthorization?.Roles);
        Assert.Equal(roles, templateAuthorization?.Roles);
    }

    [Fact]
    public void Template_IncludesFacilityTypeAndLocationColumns()
    {
        using var stream = new MemoryStream(MsyGovFacilityImportWorkbook.CreateTemplate());
        using var workbook = new XLWorkbook(stream);
        var headers = workbook.Worksheets.First().Row(1).CellsUsed()
            .Select(cell => cell.GetString())
            .ToArray();

        Assert.Contains("استان", headers);
        Assert.Contains("شهر", headers);
        Assert.Contains("نوع مکان", headers);
        Assert.Contains("نام مکان", headers);
        Assert.Contains("نام طرف قرارداد", headers);
        Assert.Contains("شماره قرارداد", headers);
        Assert.Contains("تاریخ شروع قرارداد", headers);
    }

    [Fact]
    public async Task Import_CreatesMsyAndGovFacilitiesAndSkipsExistingRows()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        context.Facilities.Add(new Facility
        {
            FacilityType = FacilityType.Msy,
            Name = "مرکز نمونه",
            CityId = scope.City.Id
        });
        await context.SaveChangesAsync();
        var controller = CreateController(context, Principal("M88Manager", "Province", scope.Province.Id));
        var file = CreateImportFile(
            ValidRow("Msy", "مرکز نمونه"),
            ValidRow("Msy", "مرکز  نمونه"),
            ValidRow("Gov", "مرکز دولتی"));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportMsyGovFacilities(file));
        var counts = Assert.IsType<MsyGovFacilityImportResultDto>(result.Value);

        Assert.Equal(1, counts.CreatedFacilities);
        Assert.Equal(2, counts.ExistingFacilities);
        Assert.Equal(2, await context.Facilities.CountAsync());
        Assert.Contains(await context.Facilities.ToListAsync(), facility => facility.FacilityType == FacilityType.Gov);
    }

    [Fact]
    public async Task Import_CreatesContractForNewFacilityAndSkipsInvalidContractWithoutSkippingFacility()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var validRow = ValidRow("Msy", "دارای قرارداد");
        validRow[8] = "CN-1";
        validRow[11] = "1403/01/15";
        validRow[12] = "1404/01/15";
        var invalidRow = ValidRow("Gov", "بدون قرارداد معتبر");
        invalidRow[8] = "";
        invalidRow[11] = "تاریخ نامعتبر";
        var controller = CreateController(context, Principal("M88Manager", "Province", scope.Province.Id));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportMsyGovFacilities(
            CreateImportFile(validRow, invalidRow)));
        var counts = Assert.IsType<MsyGovFacilityImportResultDto>(result.Value);

        Assert.Equal(2, counts.CreatedFacilities);
        Assert.Equal(1, counts.CreatedContracts);
        Assert.Equal(1, counts.SkippedInvalidContracts);
        Assert.Equal(2, await context.Facilities.CountAsync());
        var contract = await context.FacilityContracts.SingleAsync();
        Assert.Equal("CN-1", contract.ContractNumber);
        Assert.Equal("طرف قرارداد", contract.ThirdPartyName);
        Assert.Equal(new DateOnly(2024, 4, 3), contract.StartDate);
        Assert.Equal(new DateOnly(2025, 4, 4), contract.EndDate);
    }

    [Fact]
    public async Task Import_AddsContractToExistingFacilityAndSkipsDuplicateContract()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var facility = new Facility
        {
            FacilityType = FacilityType.Msy,
            Name = "مرکز نمونه",
            CityId = scope.City.Id
        };
        context.Facilities.Add(facility);
        context.FacilityContracts.Add(new FacilityContract
        {
            Facility = facility,
            ContractNumber = "CN-1",
            ThirdPartyName = "طرف قرارداد",
            ThirdPartyContact = "021-123",
            StartDate = new DateOnly(2024, 4, 3),
            EndDate = new DateOnly(2025, 4, 4)
        });
        await context.SaveChangesAsync();
        var row = ValidRow("Msy", "مرکز نمونه");
        row[8] = "CN-1";
        row[11] = "1403/01/15";
        row[12] = "1404/01/15";
        var controller = CreateController(context, Principal("M88Manager", "Province", scope.Province.Id));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportMsyGovFacilities(
            CreateImportFile(row, row)));
        var counts = Assert.IsType<MsyGovFacilityImportResultDto>(result.Value);

        Assert.Equal(2, counts.ExistingFacilities);
        Assert.Equal(0, counts.CreatedContracts);
        Assert.Equal(2, counts.SkippedExistingContracts);
        Assert.Single(await context.Facilities.ToListAsync());
        Assert.Single(await context.FacilityContracts.ToListAsync());
    }

    [Fact]
    public async Task Import_M88OfficerSkipsGovContractButCreatesGovFacility()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var row = ValidRow("Gov", "مکان دولتی");
        row[8] = "";
        row[11] = "1403/01/15";
        var controller = CreateController(context, Principal("M88Officer", "City", cityId: scope.City.Id));

        var result = Assert.IsType<OkObjectResult>(
            await controller.ImportMsyGovFacilities(CreateImportFile(row)));
        var counts = Assert.IsType<MsyGovFacilityImportResultDto>(result.Value);

        Assert.Equal(1, counts.CreatedFacilities);
        Assert.Equal(1, counts.SkippedInvalidContracts);
        Assert.Single(await context.Facilities.ToListAsync());
        Assert.Empty(await context.FacilityContracts.ToListAsync());
    }

    [Fact]
    public async Task ContractEndpoints_CreateAndReturnContractNumber()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var facility = new Facility
        {
            FacilityType = FacilityType.Msy,
            Name = "مرکز نمونه",
            CityId = scope.City.Id
        };
        context.Facilities.Add(facility);
        await context.SaveChangesAsync();
        var controller = CreateController(context, Principal("M88Manager", "Province", scope.Province.Id));
        var dto = new FacilityContractDto(
            "طرف قرارداد",
            null,
            new DateTime(2024, 4, 3),
            null,
            null,
            "CN-2024");

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.CreateContract(facility.Id, dto));
        var contractId = Assert.IsType<Guid>(created.Value!.GetType().GetProperty("id")!.GetValue(created.Value));
        var result = Assert.IsType<OkObjectResult>(await controller.GetContract(facility.Id, contractId));
        var contractNumber = result.Value!.GetType().GetProperty("ContractNumber")!.GetValue(result.Value);

        Assert.Equal("CN-2024", contractNumber);
        Assert.Equal("CN-2024", (await context.FacilityContracts.SingleAsync()).ContractNumber);
    }

    [Fact]
    public async Task Import_RejectsPrivateOrInvalidFacilityTypes()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var controller = CreateController(context, Principal("M88Officer", "City", cityId: scope.City.Id));

        var result = Assert.IsType<OkObjectResult>(await controller.ImportMsyGovFacilities(
            CreateImportFile(ValidRow("Private", "خصوصی"), ValidRow("invalid", "نامعتبر"))));
        var counts = Assert.IsType<MsyGovFacilityImportResultDto>(result.Value);

        Assert.Equal(2, counts.SkippedInvalid);
        Assert.Empty(await context.Facilities.ToListAsync());
        Assert.Equal(2, counts.Errors.Count);
    }

    [Fact]
    public async Task Import_RestrictsProvinceAndCityRolesToTheirAssignedScopes()
    {
        await using var context = CreateContext();
        var scope = await AddScopeDataAsync(context);
        var otherProvince = new Province { Name = "استان دیگر" };
        var otherCity = new City { Name = "شهر دیگر", Province = otherProvince };
        context.AddRange(otherProvince, otherCity);
        await context.SaveChangesAsync();
        var file = CreateImportFile(
            ValidRow("Msy", "مرکز استان خود", scope.Province.Name, scope.City.Name),
            ValidRow("Gov", "مرکز استان دیگر", otherProvince.Name, otherCity.Name));

        var provinceController = CreateController(
            context, Principal("M88Officer", "Province", scope.Province.Id));
        var provinceResult = Assert.IsType<OkObjectResult>(
            await provinceController.ImportMsyGovFacilities(file));
        var provinceCounts = Assert.IsType<MsyGovFacilityImportResultDto>(provinceResult.Value);

        Assert.Equal(1, provinceCounts.CreatedFacilities);
        Assert.Equal(1, provinceCounts.SkippedOutOfScope);

        var cityController = CreateController(context, Principal("M88Officer", "City", cityId: scope.City.Id));
        var cityResult = Assert.IsType<OkObjectResult>(await cityController.ImportMsyGovFacilities(
            CreateImportFile(
                ValidRow("Msy", "مرکز شهر خود", scope.Province.Name, scope.City.Name),
                ValidRow("Gov", "مرکز شهر دیگر", otherProvince.Name, otherCity.Name))));
        var cityCounts = Assert.IsType<MsyGovFacilityImportResultDto>(cityResult.Value);

        Assert.Equal(1, cityCounts.CreatedFacilities);
        Assert.Equal(1, cityCounts.SkippedOutOfScope);
        Assert.Equal(2, await context.Facilities.CountAsync());
    }

    [Fact]
    public async Task Import_RejectsPrivateFacilityRoles()
    {
        await using var context = CreateContext();
        var controller = CreateController(context, Principal("M5Officer", "Mastery"));

        var result = await controller.ImportMsyGovFacilities(CreateImportFile());

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
        string facilityType,
        string name,
        string provinceName = "استان نمونه",
        string cityName = "شهر نمونه") =>
    [
        provinceName, cityName, facilityType, name, "آدرس", "شنا", "مالک", "تماس",
        "", "طرف قرارداد", "021-123", "", ""
    ];

    private static IFormFile CreateImportFile(params string[][] rows)
    {
        var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("اماکن MSY و دولتی");
            var headers = new[]
            {
                "استان", "شهر", "نوع مکان", "نام مکان", "آدرس", "نوع ورزش", "نام مالک", "تماس مالک",
                "شماره قرارداد", "نام طرف قرارداد", "تماس طرف قرارداد", "تاریخ شروع قرارداد", "تاریخ پایان قرارداد"
            };
            for (var column = 0; column < headers.Length; column++)
                worksheet.Cell(1, column + 1).Value = headers[column];
            for (var row = 0; row < rows.Length; row++)
            for (var column = 0; column < rows[row].Length; column++)
                worksheet.Cell(row + 2, column + 1).Value = rows[row][column];
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        return new FormFile(stream, 0, stream.Length, "file", "msy-gov-facilities-import.xlsx");
    }
}
