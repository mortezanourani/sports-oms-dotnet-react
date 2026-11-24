using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class OrgUnitsControllerTests
{
    [Fact]
    public async Task ImportResolvesParentTitlesRegardlessOfRowOrder()
    {
        await using var context = CreateContext();
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "فرزند";
            worksheet.Cell(2, 2).Value = "گروه";
            worksheet.Cell(2, 3).Value = "City (شهرستانی)";
            worksheet.Cell(2, 4).Value = "والد";
            worksheet.Cell(3, 1).Value = "والد";
            worksheet.Cell(3, 2).Value = "اداره";
            worksheet.Cell(3, 3).Value = "Provincial";
            workbook.SaveAs(workbookStream);
        }

        var result = Assert.IsType<OkObjectResult>(await ImportAsync(
            new OrgUnitsController(context),
            workbookStream.ToArray()));
        var importResult = Assert.IsType<OrgUnitImportResultDto>(result.Value);
        var parent = await context.OrgUnits.SingleAsync(unit => unit.Title == "والد");
        var child = await context.OrgUnits.SingleAsync(unit => unit.Title == "فرزند");

        Assert.Equal(2, importResult.Created);
        Assert.Equal(0, importResult.Failed);
        Assert.Equal(parent.Id, child.ParentOrgUnitId);
        Assert.Equal(OrgUnitType.Group, child.Type);
        Assert.Equal(OrgUnitScope.City, child.Scope);
        Assert.Null(parent.ParentOrgUnitId);
    }

    [Fact]
    public async Task ImportRejectsParentCycles()
    {
        await using var context = CreateContext();
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            worksheet.Cell(2, 1).Value = "واحد الف";
            worksheet.Cell(2, 2).Value = "اداره";
            worksheet.Cell(2, 3).Value = "Provincial";
            worksheet.Cell(2, 4).Value = "واحد ب";
            worksheet.Cell(3, 1).Value = "واحد ب";
            worksheet.Cell(3, 2).Value = "اداره";
            worksheet.Cell(3, 3).Value = "Provincial";
            worksheet.Cell(3, 4).Value = "واحد الف";
            workbook.SaveAs(workbookStream);
        }

        var result = Assert.IsType<OkObjectResult>(await ImportAsync(
            new OrgUnitsController(context),
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
        var parent = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "والد",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial
        };
        var child = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "فرزند",
            Type = OrgUnitType.Group,
            Scope = OrgUnitScope.City,
            ParentOrgUnitId = parent.Id
        };
        context.OrgUnits.AddRange(parent, child);
        await context.SaveChangesAsync();

        var result = await new OrgUnitsController(context).Update(
            parent.Id,
            new OrgUnitUpsertDto("والد", "Administration", "Provincial", child.Id));

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(parent.Id, (await context.OrgUnits.FindAsync(child.Id))!.ParentOrgUnitId);
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
        worksheet.Cell(1, 4).Value = "واحد سازمانی والد";
        return worksheet;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
