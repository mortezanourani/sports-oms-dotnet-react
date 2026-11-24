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

public class JobPostsControllerImportTests
{
    [Fact]
    public async Task ImportResolvesReferencesAndHandlesDuplicateTitlesAndNullableBreakOut()
    {
        await using var context = CreateContext();
        var postType = new JobPostType { Id = Guid.NewGuid(), Title = "اداری" };
        var orgUnit = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "واحد مرکزی",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial
        };
        context.AddRange(postType, orgUnit);
        await context.SaveChangesAsync();

        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            AddRow(worksheet, 2, "اداری", "واحد مرکزی", "001", "کارشناس اول", "مدیر پایه", "true");
            AddRow(worksheet, 3, "اداری", "واحد مرکزی", "001", "کارشناس دوم", "مدیر ارشد", "");
            AddRow(worksheet, 4, "اداری", "واحد مرکزی", "002", "کارشناس اول", "نامشخص", "false");
            AddRow(worksheet, 5, "اداری", "واحد ناموجود", "003", "مدیر", "مدیر میانی", "true");
            workbook.SaveAs(workbookStream);
        }

        var bytes = workbookStream.ToArray();
        await using var uploadStream = new MemoryStream(bytes);
        var file = new FormFile(uploadStream, 0, bytes.Length, "file", "job-posts.xlsx");
        var result = Assert.IsType<OkObjectResult>(
            await new JobPostsController(context).Import(file));
        var importResult = Assert.IsType<JobPostImportResultDto>(result.Value);
        var posts = await context.JobPosts.OrderBy(post => post.Title).ToListAsync();

        Assert.Equal(2, importResult.Created);
        Assert.Equal(1, importResult.SkippedExisting);
        Assert.Equal(1, importResult.Failed);
        Assert.Equal(5, Assert.Single(importResult.Errors).RowNumber);
        Assert.Equal(2, posts.Count);
        Assert.All(posts, post =>
        {
            Assert.Equal(postType.Id, post.JobPostTypeId);
            Assert.Equal(orgUnit.Id, post.OrgUnitId);
            Assert.Equal("001", post.TypeCode);
        });
        Assert.Equal(JobPostManagerType.BasicManager, posts[0].ManagerType);
        Assert.True(posts[0].BreakOut);
        Assert.Equal(JobPostManagerType.SeniorManager, posts[1].ManagerType);
        Assert.Null(posts[1].BreakOut);
    }

    [Fact]
    public async Task ImportRejectsInvalidBreakOutValue()
    {
        await using var context = CreateContext();
        context.Add(new JobPostType { Id = Guid.NewGuid(), Title = "اداری" });
        context.Add(new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "واحد مرکزی",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial
        });
        await context.SaveChangesAsync();

        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            AddRow(worksheet, 2, "اداری", "واحد مرکزی", "001", "عنوان", "Unspecified", "sometimes");
            workbook.SaveAs(workbookStream);
        }

        var bytes = workbookStream.ToArray();
        await using var uploadStream = new MemoryStream(bytes);
        var file = new FormFile(uploadStream, 0, bytes.Length, "file", "job-posts.xlsx");
        var result = Assert.IsType<OkObjectResult>(
            await new JobPostsController(context).Import(file));
        var importResult = Assert.IsType<JobPostImportResultDto>(result.Value);

        Assert.Equal(0, importResult.Created);
        Assert.Equal(1, importResult.Failed);
        Assert.Empty(await context.JobPosts.ToListAsync());
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("پست‌های سازمانی");
        worksheet.Cell(1, 1).Value = "نوع پست";
        worksheet.Cell(1, 2).Value = "واحد سازمانی";
        worksheet.Cell(1, 3).Value = "کد نوع پست";
        worksheet.Cell(1, 4).Value = "عنوان پست";
        worksheet.Cell(1, 5).Value = "نوع مدیر حرفه‌ای";
        worksheet.Cell(1, 6).Value = "شکسته‌شده";
        return worksheet;
    }

    private static void AddRow(
        IXLWorksheet worksheet,
        int row,
        string jobPostType,
        string orgUnit,
        string typeCode,
        string title,
        string managerType,
        string breakOut)
    {
        worksheet.Cell(row, 1).Value = jobPostType;
        worksheet.Cell(row, 2).Value = orgUnit;
        worksheet.Cell(row, 3).Value = typeCode;
        worksheet.Cell(row, 4).Value = title;
        worksheet.Cell(row, 5).Value = managerType;
        worksheet.Cell(row, 6).Value = breakOut;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
