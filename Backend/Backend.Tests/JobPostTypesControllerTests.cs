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

public class JobPostTypesControllerTests
{
    [Fact]
    public async Task CreateTrimsTitleAndRejectsDuplicate()
    {
        await using var context = CreateContext();
        var controller = new JobPostTypesController(context);

        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(new JobPostTypeUpsertDto("  مدیر  ")));
        var duplicate = Assert.IsType<ConflictObjectResult>(
            await controller.Create(new JobPostTypeUpsertDto("مدیر")));

        Assert.Equal("مدیر", Assert.IsType<JobPostTypeOutDto>(created.Value).Title);
        Assert.Equal(1, await context.JobPostTypes.CountAsync());
        Assert.NotNull(duplicate.Value);
    }

    [Fact]
    public async Task ImportSkipsExistingAndRepeatedTitlesAndReportsInvalidRows()
    {
        await using var context = CreateContext();
        context.JobPostTypes.Add(new JobPostType { Id = Guid.NewGuid(), Title = "موجود" });
        await context.SaveChangesAsync();

        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = workbook.Worksheets.Add("انواع پست");
            worksheet.Cell(1, 1).Value = "عنوان نوع پست";
            worksheet.Cell(2, 1).Value = " موجود ";
            worksheet.Cell(3, 1).Value = "جدید";
            worksheet.Cell(4, 1).Value = "جدید";
            worksheet.Cell(5, 2).Value = "مقدار دیگر";
            workbook.SaveAs(workbookStream);
        }

        var bytes = workbookStream.ToArray();
        await using var uploadStream = new MemoryStream(bytes);
        var file = new FormFile(uploadStream, 0, bytes.Length, "file", "job-post-types.xlsx");
        var controller = new JobPostTypesController(context);

        var result = Assert.IsType<OkObjectResult>(await controller.Import(file));
        var importResult = Assert.IsType<JobPostTypeImportResultDto>(result.Value);

        Assert.Equal(1, importResult.Created);
        Assert.Equal(2, importResult.SkippedExisting);
        Assert.Equal(1, importResult.Failed);
        Assert.Equal(5, Assert.Single(importResult.Errors).RowNumber);
        var savedTitles = await context.JobPostTypes.Select(type => type.Title).ToArrayAsync();
        Assert.Equal(2, savedTitles.Length);
        Assert.Contains("موجود", savedTitles);
        Assert.Contains("جدید", savedTitles);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
