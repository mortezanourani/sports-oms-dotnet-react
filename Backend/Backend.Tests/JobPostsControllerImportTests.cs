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
    public async Task CreateAllowsDuplicateTitlesAndUniquelyEnforcesJobPostIdentifiers()
    {
        await using var context = CreateContext();
        var type = new JobPostType { Id = Guid.NewGuid(), Title = "اداری" };
        var orgUnit = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "واحد مرکزی",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial
        };
        context.AddRange(type, orgUnit);
        await context.SaveChangesAsync();
        var controller = new JobPostsController(context);

        var missingIdentifier = await controller.Create(
            new JobPostUpsertDto(type.Id, orgUnit.Id, "نوع", "", "شناسه-۱", "عنوان", "Unspecified", null));
        Assert.IsType<BadRequestObjectResult>(missingIdentifier);

        var created = await controller.Create(
            new JobPostUpsertDto(type.Id, orgUnit.Id, "نوع", "شماره-۱", "شناسه-۱", "عنوان اول", "Unspecified", null));
        var duplicateTitle = await controller.Create(
            new JobPostUpsertDto(type.Id, orgUnit.Id, "نوع", "شماره-۲", "شناسه-۲", "عنوان اول", "Unspecified", null));

        var duplicateCode = await controller.Create(
            new JobPostUpsertDto(type.Id, orgUnit.Id, "نوع", "شماره-۱", "شناسه-۳", "عنوان دوم", "Unspecified", null));
        var duplicateUniqueId = await controller.Create(
            new JobPostUpsertDto(type.Id, orgUnit.Id, "نوع", "شماره-۳", "شناسه-۱", "عنوان سوم", "Unspecified", null));

        Assert.IsType<CreatedAtActionResult>(created);
        Assert.IsType<CreatedAtActionResult>(duplicateTitle);
        Assert.IsType<ConflictObjectResult>(duplicateCode);
        Assert.IsType<ConflictObjectResult>(duplicateUniqueId);
        Assert.Equal(2, await context.JobPosts.CountAsync());
    }

    [Fact]
    public async Task ImportAllowsDuplicateTitlesAndRejectsDuplicateIdentifiers()
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
        context.JobPosts.Add(new JobPost
        {
            Id = Guid.NewGuid(),
            JobPostTypeId = postType.Id,
            OrgUnitId = orgUnit.Id,
            TypeCode = "موجود",
            JobPostCode = "شماره-موجود",
            JobPostUniqueId = "شناسه-موجود",
            Title = "کارشناس اول",
            ManagerType = JobPostManagerType.Unspecified
        });
        await context.SaveChangesAsync();

        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            AddRow(worksheet, 2, "اداری", "واحد مرکزی", "001", "شماره-۱", "شناسه-۱", "کارشناس اول", "مدیر پایه", "بله");
            AddRow(worksheet, 3, "اداری", "واحد مرکزی", "001", "شماره-۲", "شناسه-۲", "کارشناس اول", "مدیر ارشد", "نامشخص");
            AddRow(worksheet, 4, "اداری", "واحد مرکزی", "002", "شماره-۳", "شناسه-۳", "کارشناس اول", "نامشخص", "خیر");
            AddRow(worksheet, 5, "اداری", "واحد مرکزی", "003", "شماره-۴", "شناسه-۴", "کارشناس اول", "نامشخص", "بله");
            AddRow(worksheet, 6, "اداری", "واحد ناموجود", "004", "شماره-۵", "شناسه-۵", "مدیر", "مدیر میانی", "بله");
            AddRow(worksheet, 7, "اداری", "واحد مرکزی", "005", "شماره-۱", "شناسه-۶", "کارشناس چهارم", "نامشخص", "خیر");
            AddRow(worksheet, 8, "اداری", "واحد مرکزی", "006", "شماره-۶", "شناسه-۱", "کارشناس پنجم", "نامشخص", "خیر");
            workbook.SaveAs(workbookStream);
        }

        var bytes = workbookStream.ToArray();
        await using var uploadStream = new MemoryStream(bytes);
        var file = new FormFile(uploadStream, 0, bytes.Length, "file", "job-posts.xlsx");
        var result = Assert.IsType<OkObjectResult>(
            await new JobPostsController(context).Import(file));
        var importResult = Assert.IsType<JobPostImportResultDto>(result.Value);
        var posts = await context.JobPosts.ToListAsync();
        var importedPosts = posts
            .Where(post => post.JobPostUniqueId != "شناسه-موجود")
            .OrderBy(post => post.JobPostUniqueId)
            .ToList();

        Assert.Equal(4, importResult.Created);
        Assert.Equal(0, importResult.SkippedExisting);
        Assert.Equal(3, importResult.Failed);
        Assert.Equal(new[] { 6, 7, 8 }, importResult.Errors.Select(error => error.RowNumber));
        Assert.Equal(5, posts.Count);
        Assert.Equal(5, posts.Count(post => post.Title == "کارشناس اول"));
        Assert.All(importedPosts, post =>
        {
            Assert.Equal(postType.Id, post.JobPostTypeId);
            Assert.Equal(orgUnit.Id, post.OrgUnitId);
        });
        Assert.Equal(new[] { "001", "001", "002", "003" }, importedPosts.Select(post => post.TypeCode));
        Assert.Equal(JobPostManagerType.BasicManager, importedPosts[0].ManagerType);
        Assert.True(importedPosts[0].BreakOut);
        Assert.Equal(JobPostManagerType.SeniorManager, importedPosts[1].ManagerType);
        Assert.Null(importedPosts[1].BreakOut);
        Assert.False(importedPosts[2].BreakOut);
        Assert.Equal(new[] { "شناسه-۱", "شناسه-۲", "شناسه-۳", "شناسه-۴" }, importedPosts.Select(post => post.JobPostUniqueId));
        Assert.Equal(new[] { "شماره-۱", "شماره-۲", "شماره-۳", "شماره-۴" }, importedPosts.Select(post => post.JobPostCode));
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
            AddRow(worksheet, 2, "اداری", "واحد مرکزی", "001", "شماره", "شناسه", "عنوان", "Unspecified", "sometimes");
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
        worksheet.Cell(1, 4).Value = "شماره پست";
        worksheet.Cell(1, 5).Value = "شناسه یکتا پست";
        worksheet.Cell(1, 6).Value = "عنوان پست";
        worksheet.Cell(1, 7).Value = "نوع مدیر حرفه‌ای";
        worksheet.Cell(1, 8).Value = "شکسته‌شده";
        return worksheet;
    }

    private static void AddRow(
        IXLWorksheet worksheet,
        int row,
        string jobPostType,
        string orgUnit,
        string typeCode,
        string jobPostCode,
        string jobPostUniqueId,
        string title,
        string managerType,
        string breakOut)
    {
        worksheet.Cell(row, 1).Value = jobPostType;
        worksheet.Cell(row, 2).Value = orgUnit;
        worksheet.Cell(row, 3).Value = typeCode;
        worksheet.Cell(row, 4).Value = jobPostCode;
        worksheet.Cell(row, 5).Value = jobPostUniqueId;
        worksheet.Cell(row, 6).Value = title;
        worksheet.Cell(row, 7).Value = managerType;
        worksheet.Cell(row, 8).Value = breakOut;
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
