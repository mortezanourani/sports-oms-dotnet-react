using System.Security.Claims;
using Backend.Controllers;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Backend.Tests;

public class JobPostAssignmentsControllerTests
{
    [Fact]
    public async Task CreateTreatsNullDatesAsOpenAndRejectsConcurrentUserOrJobPostAssignments()
    {
        await using var context = CreateContext();
        var (user1, user2) = AddUsers(context);
        var (post1, post2) = AddPosts(context);
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var created = await controller.Create(new JobPostAssignmentCreateDto(user1.Id, post1.Id, null, null));
        var sameUser = await controller.Create(new JobPostAssignmentCreateDto(user1.Id, post2.Id, null, null));
        var samePost = await controller.Create(new JobPostAssignmentCreateDto(user2.Id, post1.Id, null, null));
        var duplicatePair = await controller.Create(new JobPostAssignmentCreateDto(user1.Id, post1.Id, null, null));

        Assert.IsType<CreatedAtActionResult>(created);
        Assert.IsType<ConflictObjectResult>(sameUser);
        Assert.IsType<ConflictObjectResult>(samePost);
        Assert.IsType<ConflictObjectResult>(duplicatePair);
        Assert.Single(await context.JobPostAssignments.ToListAsync());
    }

    [Fact]
    public async Task DateRangesAreInclusiveAndAllowNextDayHandoffs()
    {
        await using var context = CreateContext();
        var (user, _) = AddUsers(context);
        var (post1, post2) = AddPosts(context);
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var firstStart = new DateOnly(2024, 1, 1);
        var firstEnd = new DateOnly(2024, 1, 10);

        var first = await controller.Create(
            new JobPostAssignmentCreateDto(user.Id, post1.Id, firstStart, firstEnd));
        var sameDay = await controller.Create(
            new JobPostAssignmentCreateDto(user.Id, post2.Id, firstEnd, null));
        var nextDay = await controller.Create(
            new JobPostAssignmentCreateDto(user.Id, post2.Id, firstEnd.AddDays(1), null));

        Assert.IsType<CreatedAtActionResult>(first);
        Assert.IsType<ConflictObjectResult>(sameDay);
        Assert.IsType<CreatedAtActionResult>(nextDay);
    }

    [Fact]
    public async Task EndKeepsAssignmentHistoryAndAllowsNextDayReassignment()
    {
        await using var context = CreateContext();
        var (user, _) = AddUsers(context);
        var (post, _) = AddPosts(context);
        await context.SaveChangesAsync();
        var controller = CreateController(context);
        var created = Assert.IsType<CreatedAtActionResult>(
            await controller.Create(new JobPostAssignmentCreateDto(user.Id, post.Id, null, null)));
        var assignment = Assert.IsType<JobPostAssignmentOutDto>(created.Value);
        var endDate = new DateOnly(2024, 1, 10);

        var ended = await controller.End(assignment.Id, new JobPostAssignmentEndDto(endDate));
        var reassigned = await controller.Create(
            new JobPostAssignmentCreateDto(user.Id, post.Id, endDate.AddDays(1), null));

        Assert.IsType<OkObjectResult>(ended);
        Assert.IsType<CreatedAtActionResult>(reassigned);
        var history = await context.JobPostAssignments.OrderBy(item => item.StartDate).ToListAsync();
        Assert.Equal(2, history.Count);
        Assert.Equal(endDate, history[0].EndDate);
        Assert.Equal(endDate.AddDays(1), history[1].StartDate);
    }

    [Fact]
    public async Task ImportCreatesOpenAssignmentsAndReportsDuplicateCurrentAssignments()
    {
        await using var context = CreateContext();
        var (user1, user2) = AddUsers(context);
        var (post1, post2) = AddPosts(context);
        await context.SaveChangesAsync();
        using var workbookStream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            var worksheet = CreateWorksheet(workbook);
            AddRow(worksheet, 2, user1.UserName!, post1.JobPostUniqueId);
            AddRow(worksheet, 3, user2.UserName!, post2.JobPostUniqueId);
            AddRow(worksheet, 4, user1.UserName!, post1.JobPostUniqueId);
            workbook.SaveAs(workbookStream);
        }

        var bytes = workbookStream.ToArray();
        await using var uploadStream = new MemoryStream(bytes);
        var file = new FormFile(uploadStream, 0, bytes.Length, "file", "job-post-assignments.xlsx");
        var result = Assert.IsType<OkObjectResult>(await CreateController(context).Import(file));
        var importResult = Assert.IsType<JobPostAssignmentImportResultDto>(result.Value);
        var saved = await context.JobPostAssignments.OrderBy(item => item.UserId).ToListAsync();

        Assert.Equal(2, importResult.Created);
        Assert.Equal(1, importResult.Failed);
        Assert.Equal(4, Assert.Single(importResult.Errors).RowNumber);
        Assert.All(saved, assignment =>
        {
            Assert.Null(assignment.StartDate);
            Assert.Null(assignment.EndDate);
        });
    }

    [Fact]
    public async Task CreateForbidsAssignmentOutsideUserAndOrgUnitScope()
    {
        await using var context = CreateContext();
        var provinceId = Guid.NewGuid();
        var cityId = Guid.NewGuid();
        var targetCityId = Guid.NewGuid();
        var target = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "target",
            NormalizedUserName = "TARGET",
            Role = UserRole.SportsOfficer,
            ScopeLevel = ScopeLevel.City,
            ScopeProvinceId = provinceId,
            ScopeCityId = targetCityId
        };
        var post = CreatePost("post", provinceId, cityId);
        context.AddRange(target, post);
        await context.SaveChangesAsync();
        var controller = CreateController(context, "City", provinceId, cityId);

        var outsideUser = await controller.Create(
            new JobPostAssignmentCreateDto(target.Id, post.Id, null, null));

        Assert.IsType<ForbidResult>(outsideUser);
        Assert.Empty(await context.JobPostAssignments.ToListAsync());
    }

    private static (ApplicationUser First, ApplicationUser Second) AddUsers(ApplicationDbContext context)
    {
        var first = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "user.one",
            NormalizedUserName = "USER.ONE",
            FirstName = "کاربر",
            LastName = "یک",
            Role = UserRole.SportsOfficer,
            ScopeLevel = ScopeLevel.Mastery
        };
        var second = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            UserName = "user.two",
            NormalizedUserName = "USER.TWO",
            FirstName = "کاربر",
            LastName = "دو",
            Role = UserRole.SportsOfficer,
            ScopeLevel = ScopeLevel.Mastery
        };
        context.AddRange(first, second);
        return (first, second);
    }

    private static (JobPost First, JobPost Second) AddPosts(ApplicationDbContext context)
    {
        var type = new JobPostType { Id = Guid.NewGuid(), Title = "اداری" };
        var orgUnit = new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = "واحد مرکزی",
            Type = OrgUnitType.Administration,
            Scope = OrgUnitScope.Provincial,
            ProvinceId = Guid.NewGuid()
        };
        var first = CreatePost("post.one", orgUnit.ProvinceId, null, type, orgUnit);
        var second = CreatePost("post.two", orgUnit.ProvinceId, null, type, orgUnit);
        context.AddRange(type, orgUnit, first, second);
        return (first, second);
    }

    private static JobPost CreatePost(
        string uniqueId,
        Guid provinceId,
        Guid? cityId,
        JobPostType? type = null,
        OrgUnit? orgUnit = null)
    {
        type ??= new JobPostType { Id = Guid.NewGuid(), Title = $"type-{uniqueId}" };
        orgUnit ??= new OrgUnit
        {
            Id = Guid.NewGuid(),
            Title = $"unit-{uniqueId}",
            Type = OrgUnitType.Administration,
            Scope = cityId.HasValue ? OrgUnitScope.City : OrgUnitScope.Provincial,
            ProvinceId = provinceId,
            CityId = cityId
        };
        return new JobPost
        {
            Id = Guid.NewGuid(),
            JobPostTypeId = type.Id,
            JobPostType = type,
            OrgUnitId = orgUnit.Id,
            OrgUnit = orgUnit,
            TypeCode = "type",
            JobPostCode = $"code-{uniqueId}",
            JobPostUniqueId = uniqueId,
            Title = $"title-{uniqueId}",
            ManagerType = JobPostManagerType.Unspecified
        };
    }

    private static IXLWorksheet CreateWorksheet(XLWorkbook workbook)
    {
        var worksheet = workbook.Worksheets.Add("تخصیص پست");
        worksheet.Cell(1, 1).Value = "نام کاربری";
        worksheet.Cell(1, 2).Value = "شناسه یکتا پست";
        worksheet.Cell(1, 3).Value = "تاریخ شروع";
        worksheet.Cell(1, 4).Value = "تاریخ پایان";
        return worksheet;
    }

    private static void AddRow(IXLWorksheet worksheet, int row, string username, string uniqueId)
    {
        worksheet.Cell(row, 1).Value = username;
        worksheet.Cell(row, 2).Value = uniqueId;
    }

    private static JobPostAssignmentsController CreateController(
        ApplicationDbContext context,
        string scopeLevel = "Mastery",
        Guid? provinceId = null,
        Guid? cityId = null)
    {
        var claims = new List<Claim>
        {
            new(AuthClaimTypes.Role, nameof(UserRole.HumanResourceManager)),
            new(AuthClaimTypes.ScopeLevel, scopeLevel),
            new(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())
        };
        if (provinceId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeProvinceId, provinceId.Value.ToString()));
        if (cityId.HasValue)
            claims.Add(new Claim(AuthClaimTypes.ScopeCityId, cityId.Value.ToString()));

        return new JobPostAssignmentsController(context)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }
}
