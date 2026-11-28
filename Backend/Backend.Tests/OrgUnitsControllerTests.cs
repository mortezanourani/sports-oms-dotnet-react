using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Xunit;

namespace Backend.Tests;

public class OrgUnitsControllerTests
{
    [Fact]
    public async Task ImportResolvesParentTitlesRegardlessOfRowOrder()
    {
        await using var context = CreateContext();
        var (province, city) = await AddLocationAsync(context);
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "فرزند";
            worksheet.Cell(2, 2).Value = "گروه";
            worksheet.Cell(2, 3).Value = "City (شهرستانی)";
            worksheet.Cell(2, 4).Value = province.Name;
            worksheet.Cell(2, 5).Value = city.Name;
            worksheet.Cell(2, 6).Value = "والد";
            worksheet.Cell(3, 1).Value = "والد";
            worksheet.Cell(3, 2).Value = "اداره";
            worksheet.Cell(3, 3).Value = "Provincial";
            worksheet.Cell(3, 4).Value = province.Name;
            workbook.SaveAs(workbookStream);
        }

        var result = Assert.IsType<OkObjectResult>(await ImportAsync(
            CreateController(context),
            workbookStream.ToArray()));
        var importResult = Assert.IsType<OrgUnitImportResultDto>(result.Value);
        var parent = await context.OrgUnits.SingleAsync(unit => unit.Title == "والد");
        var child = await context.OrgUnits.SingleAsync(unit => unit.Title == "فرزند");

        Assert.Equal(2, importResult.Created);
        Assert.Equal(0, importResult.Failed);
        Assert.Equal(parent.Id, child.ParentOrgUnitId);
        Assert.Equal(OrgUnitType.Group, child.Type);
        Assert.Equal(OrgUnitScope.City, child.Scope);
        Assert.Equal(province.Id, child.ProvinceId);
        Assert.Equal(city.Id, child.CityId);
        Assert.Equal(province.Id, parent.ProvinceId);
        Assert.Null(parent.CityId);
        Assert.Null(parent.ParentOrgUnitId);
    }

    [Fact]
    public async Task ImportRejectsParentCycles()
    {
        await using var context = CreateContext();
        var (province, _) = await AddLocationAsync(context);
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "واحد الف";
            worksheet.Cell(2, 2).Value = "اداره";
            worksheet.Cell(2, 3).Value = "Provincial";
            worksheet.Cell(2, 4).Value = province.Name;
            worksheet.Cell(2, 6).Value = "واحد ب";
            worksheet.Cell(3, 1).Value = "واحد ب";
            worksheet.Cell(3, 2).Value = "اداره";
            worksheet.Cell(3, 3).Value = "Provincial";
            worksheet.Cell(3, 4).Value = province.Name;
            worksheet.Cell(3, 6).Value = "واحد الف";
            workbook.SaveAs(workbookStream);
        }

        var result = Assert.IsType<OkObjectResult>(await ImportAsync(
            CreateController(context),
            workbookStream.ToArray()));
        var importResult = Assert.IsType<OrgUnitImportResultDto>(result.Value);

        Assert.Equal(0, importResult.Created);
        Assert.Equal(2, importResult.Failed);
        Assert.Empty(await context.OrgUnits.ToListAsync());
    }

    [Fact]
    public async Task UpdateRejectsUsingDescendantAsParent()
    {
        await using var context = CreateContext();
        var (province, _) = await AddLocationAsync(context);
        var parent = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "والد",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial,
            ProvinceId = province.Id
        };
        var child = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "فرزند",
            Type = OrgUnitType.Group,
            Scope = OrgUnitScope.Provincial,
            ProvinceId = province.Id,
            ParentOrgUnitId = parent.Id
        };
        context.OrgUnits.AddRange(parent, child);
        await context.SaveChangesAsync();

        var result = await CreateController(context).Update(
            parent.Id,
            new OrgUnitUpsertDto("والد", "Administration", "Provincial", province.Id, null, child.Id));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(parent.Id, (await context.OrgUnits.FindAsync(child.Id))!.ParentOrgUnitId);
    }

    [Fact]
    public async Task CreateRequiresCityOnlyForCityScopeAndMatchingProvince()
    {
        await using var context = CreateContext();
        var (province, city) = await AddLocationAsync(context);
        var controller = CreateController(context);

        var missingCity = await controller.Create(
            new OrgUnitUpsertDto("واحد شهر", "Group", "City", province.Id, null, null));
        var cityOnProvincialUnit = await controller.Create(
            new OrgUnitUpsertDto("واحد استان", "Group", "Provincial", province.Id, city.Id, null));

        Assert.IsType<BadRequestObjectResult>(missingCity);
        Assert.IsType<BadRequestObjectResult>(cityOnProvincialUnit);
        Assert.Empty(await context.OrgUnits.ToListAsync());
    }

    [Fact]
    public async Task CreateForbidsProvinceOutsideUserScope()
    {
        await using var context = CreateContext();
        var (province, _) = await AddLocationAsync(context);
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        context.Provinces.Add(otherProvince);
        await context.SaveChangesAsync();
        var controller = CreateController(context, "Province", province.Id);

        var result = await controller.Create(
            new OrgUnitUpsertDto("واحد خارج از دسترس", "Group", "Provincial", otherProvince.Id, null, null));

        Assert.IsType<ForbidResult>(result);
        Assert.Empty(await context.OrgUnits.ToListAsync());
    }

    [Fact]
    public async Task ImportRejectsRowsOutsideUserScope()
    {
        await using var context = CreateContext();
        var (allowedProvince, _) = await AddLocationAsync(context);
        var otherProvince = new Province { Id = Guid.NewGuid(), Name = "استان دیگر" };
        context.Provinces.Add(otherProvince);
        await context.SaveChangesAsync();
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "واحد خارج از دسترس";
            worksheet.Cell(2, 2).Value = "اداره";
            worksheet.Cell(2, 3).Value = "Provincial";
            worksheet.Cell(2, 4).Value = otherProvince.Name;
            workbook.SaveAs(workbookStream);
        }

        var result = Assert.IsType<OkObjectResult>(await ImportAsync(
            CreateController(context, "Province", allowedProvince.Id),
            workbookStream.ToArray()));
        var importResult = Assert.IsType<OrgUnitImportResultDto>(result.Value);

        Assert.Equal(0, importResult.Created);
        Assert.Equal(1, importResult.Failed);
        Assert.Empty(await context.OrgUnits.ToListAsync());
    }

    private static async Task<IActionResult> ImportAsync(OrgUnitsController controller, byte[] bytes)
    {
        await using var stream = new MemoryStream(bytes);
        var file = new FormFile(stream, 0, bytes.Length, "file", "org-units.xlsx");
        return await controller.Import(file);
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("واحدهای سازمانی");
        worksheet.Cell(1, 1).Value = "عنوان واحد سازمانی";
        worksheet.Cell(1, 2).Value = "نوع واحد سازمانی";
        worksheet.Cell(1, 3).Value = "دامنه";
        worksheet.Cell(1, 4).Value = "استان";
        worksheet.Cell(1, 5).Value = "شهرستان";
        worksheet.Cell(1, 6).Value = "واحد سازمانی والد";
        return worksheet;
    }

    private static async Task<(Province Province, City City)> AddLocationAsync(ApplicationDbContext context)
    {
        var province = new Province { Id = Guid.NewGuid(), Name = "استان نمونه" };
        var city = new City { Id = Guid.NewGuid(), Name = "شهر نمونه", ProvinceId = province.Id };
        context.AddRange(province, city);
        await context.SaveChangesAsync();
        return (province, city);
    }

    private static OrgUnitsController CreateController(
        ApplicationDbContext context,
        string scopeLevel = "Mastery",
        Guid? provinceId = null,
        Guid? cityId = null)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Role, "Admin"),
            new(AuthClaimTypes.ScopeLevel, scopeLevel)
        };
        if (provinceId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeProvinceId, provinceId.Value.ToString()));
        if (cityId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeCityId, cityId.Value.ToString()));

        var controller = new OrgUnitsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return controller;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
