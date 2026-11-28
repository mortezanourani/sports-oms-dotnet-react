using System.Security.Claims;
using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers;

[ApiController]
[Route("api/job-posts/assignments")]
[Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer")]
public sealed class JobPostAssignmentsController : ControllerBase
{
    private readonly ApplicationDbContext _db;

    public JobPostAssignmentsController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> List(Guid? userId, Guid? jobPostId)
    {
        var query = VisibleAssignments();
        if (userId.HasValue)
            query = query.Where(assignment => assignment.UserId == userId.Value);
        if (jobPostId.HasValue)
            query = query.Where(assignment => assignment.JobPostId == jobPostId.Value);

        var assignments = await query
            .OrderByDescending(assignment => assignment.StartDate)
            .ThenByDescending(assignment => assignment.CreatedAtUtc)
            .Select(assignment => new JobPostAssignmentOutDto(
                assignment.Id,
                assignment.UserId,
                assignment.User.UserName!,
                assignment.User.FirstName ?? string.Empty,
                assignment.User.LastName ?? string.Empty,
                assignment.JobPostId,
                assignment.JobPost.Title,
                assignment.JobPost.JobPostUniqueId,
                assignment.StartDate,
                assignment.EndDate,
                assignment.CreatedAtUtc))
            .ToListAsync();
        return Ok(assignments);
    }

    [HttpGet("job-posts")]
    public async Task<IActionResult> ListAssignableJobPosts()
    {
        var posts = await JobPostsInUserScope()
            .OrderBy(post => post.Title)
            .Select(post => new JobPostAssignmentPostDto(
                post.Id,
                post.Title,
                post.JobPostUniqueId))
            .ToListAsync();
        return Ok(posts);
    }

    [HttpPost]
    public async Task<IActionResult> Create(JobPostAssignmentCreateDto dto)
    {
        if (!await IsValidTargetAsync(dto.UserId, dto.JobPostId))
            return BadRequest(new { detail = "کاربر یا پست انتخاب‌شده معتبر نیست" });

        var user = await _db.Users.SingleAsync(candidate => candidate.Id == dto.UserId);
        var jobPost = await _db.JobPosts
            .Include(post => post.OrgUnit)
            .SingleAsync(post => post.Id == dto.JobPostId);
        if (!CanManageTargetUser(user) ||
            !CanManageJobPost(jobPost.OrgUnit.ProvinceId, jobPost.OrgUnit.CityId))
            return Forbid();

        if (!IsValidDateRange(dto.StartDate, dto.EndDate))
            return BadRequest(new { detail = "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد" });
        if (await HasOverlappingAssignmentAsync(dto.UserId, dto.JobPostId, dto.StartDate, dto.EndDate))
            return Conflict(new { detail = "در این بازه، کاربر یا پست از قبل تخصیص فعال دارد" });

        var assignment = new JobPostAssignment
        {
            Id = Guid.NewGuid(),
            UserId = dto.UserId,
            JobPostId = dto.JobPostId,
            StartDate = dto.StartDate,
            EndDate = dto.EndDate,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = CurrentUserId()
        };
        _db.JobPostAssignments.Add(assignment);
        await _db.SaveChangesAsync();
        return CreatedAtAction(
            nameof(List),
            new { userId = assignment.UserId, jobPostId = assignment.JobPostId },
            ToOutDto(assignment, user, jobPost));
    }

    [HttpPost("{id:guid}/end")]
    public async Task<IActionResult> End(Guid id, JobPostAssignmentEndDto dto)
    {
        var assignment = await _db.JobPostAssignments
            .Include(candidate => candidate.User)
            .Include(candidate => candidate.JobPost)
                .ThenInclude(post => post.OrgUnit)
            .SingleOrDefaultAsync(candidate => candidate.Id == id);
        if (assignment == null)
            return NotFound(new { detail = "سابقه تخصیص یافت نشد" });
        if (!CanManageTargetUser(assignment.User) ||
            !CanManageJobPost(assignment.JobPost.OrgUnit.ProvinceId, assignment.JobPost.OrgUnit.CityId))
            return Forbid();
        if (assignment.EndDate.HasValue)
            return Conflict(new { detail = "این تخصیص قبلا پایان یافته است" });
        if (assignment.StartDate.HasValue && dto.EndDate < assignment.StartDate.Value)
            return BadRequest(new { detail = "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد" });
        if (await HasOverlappingAssignmentAsync(
                assignment.UserId,
                assignment.JobPostId,
                assignment.StartDate,
                dto.EndDate,
                assignment.Id))
            return Conflict(new { detail = "تاریخ پایان باعث هم‌پوشانی با تخصیص دیگری می‌شود" });

        assignment.EndDate = dto.EndDate;
        await _db.SaveChangesAsync();
        return Ok(ToOutDto(assignment, assignment.User, assignment.JobPost));
    }

    [HttpGet("import/template")]
    public IActionResult DownloadImportTemplate() =>
        File(
            JobPostAssignmentImportWorkbook.CreateTemplate(),
            JobPostAssignmentImportWorkbook.ContentType,
            "job-post-assignments-import-template.xlsx");

    [HttpPost("import")]
    [RequestSizeLimit(10_000_000)]
    public async Task<IActionResult> Import([FromForm] IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

        IReadOnlyList<JobPostAssignmentImportRow> rows;
        try
        {
            await using var stream = file.OpenReadStream();
            rows = JobPostAssignmentImportWorkbook.Read(stream);
        }
        catch (InvalidDataException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (ArgumentException)
        {
            return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
        }
        catch (IOException)
        {
            return BadRequest(new { detail = "خواندن فایل اکسل با خطا مواجه شد" });
        }
        catch (OpenXmlPackageException)
        {
            return BadRequest(new { detail = "فایل اکسل معتبر نیست" });
        }

        var users = await _db.Users
            .AsNoTracking()
            .Where(user => user.Role != UserRole.FederationRep)
            .Select(user => new
            {
                User = user,
                user.NormalizedUserName
            })
            .ToListAsync();
        var usersByUsername = users
            .Where(item => !string.IsNullOrWhiteSpace(item.NormalizedUserName))
            .ToDictionary(item => item.NormalizedUserName!, item => item.User, StringComparer.Ordinal);
        var posts = await _db.JobPosts
            .AsNoTracking()
            .Include(post => post.OrgUnit)
            .ToListAsync();
        var postsByUniqueId = posts.ToDictionary(post => post.JobPostUniqueId, StringComparer.Ordinal);
        var allAssignments = await _db.JobPostAssignments
            .Select(assignment => new AssignmentRange(
                assignment.UserId,
                assignment.JobPostId,
                assignment.StartDate,
                assignment.EndDate))
            .ToListAsync();
        var createdAssignments = new List<JobPostAssignment>();
        var errors = new List<JobPostAssignmentImportErrorDto>();

        foreach (var row in rows)
        {
            var username = row.Username?.Trim() ?? string.Empty;
            var uniqueId = row.JobPostUniqueId?.Trim() ?? string.Empty;
            if (username.Length == 0 || uniqueId.Length == 0)
            {
                errors.Add(new JobPostAssignmentImportErrorDto(
                    row.RowNumber,
                    "نام کاربری و شناسه یکتا پست الزامی هستند."));
                continue;
            }
            if (!usersByUsername.TryGetValue(username.ToUpperInvariant(), out var user))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(row.RowNumber, "کاربری با این نام کاربری یافت نشد."));
                continue;
            }
            if (!postsByUniqueId.TryGetValue(uniqueId, out var post))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(row.RowNumber, "پستی با این شناسه یکتا یافت نشد."));
                continue;
            }
            if (!CanManageTargetUser(user) ||
                !CanManageJobPost(post.OrgUnit.ProvinceId, post.OrgUnit.CityId))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(
                    row.RowNumber,
                    "کاربر یا پست خارج از محدوده دسترسی شما است."));
                continue;
            }
            if (!JobPostAssignmentImportWorkbook.TryParseDate(row.StartDate, out var startDate) ||
                !JobPostAssignmentImportWorkbook.TryParseDate(row.EndDate, out var endDate))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(
                    row.RowNumber,
                    "تاریخ باید شمسی به شکل سال/ماه/روز یا یک تاریخ استاندارد Excel باشد."));
                continue;
            }
            if (!IsValidDateRange(startDate, endDate))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(
                    row.RowNumber,
                    "تاریخ پایان نمی‌تواند قبل از تاریخ شروع باشد."));
                continue;
            }

            var ranges = allAssignments.Concat(createdAssignments.Select(assignment =>
                new AssignmentRange(
                    assignment.UserId,
                    assignment.JobPostId,
                    assignment.StartDate,
                    assignment.EndDate)));
            if (ranges.Any(range =>
                    (range.UserId == user.Id || range.JobPostId == post.Id) &&
                    Overlaps(range.StartDate, range.EndDate, startDate, endDate)))
            {
                errors.Add(new JobPostAssignmentImportErrorDto(
                    row.RowNumber,
                    "در این بازه، کاربر یا پست از قبل تخصیص فعال دارد."));
                continue;
            }

            createdAssignments.Add(new JobPostAssignment
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                JobPostId = post.Id,
                StartDate = startDate,
                EndDate = endDate,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedByUserId = CurrentUserId()
            });
        }

        _db.JobPostAssignments.AddRange(createdAssignments);
        await _db.SaveChangesAsync();
        return Ok(new JobPostAssignmentImportResultDto(
            createdAssignments.Count,
            errors.Count,
            errors));
    }

    private IQueryable<JobPostAssignment> VisibleAssignments()
    {
        var query = _db.JobPostAssignments
            .Where(assignment => assignment.User.Role != UserRole.FederationRep);
        var provinceId = User.ScopeProvinceId();
        var cityId = User.ScopeCityId();
        query = User.ScopeLevel() switch
        {
            "Mastery" => query,
            "Province" => query
                .Where(assignment => assignment.User.ScopeProvinceId == provinceId)
                .Where(assignment => assignment.JobPost.OrgUnit.ProvinceId == provinceId),
            "City" => query
                .Where(assignment => assignment.User.ScopeCityId == cityId)
                .Where(assignment => assignment.JobPost.OrgUnit.CityId == cityId),
            _ => query.Where(_ => false)
        };

        if (User.Role() == nameof(UserRole.HumanResourceManager))
            query = query.Where(assignment =>
                assignment.User.Role != UserRole.Admin &&
                assignment.User.Role != UserRole.Manager);
        else if (User.Role() == nameof(UserRole.HumanResourceOfficer))
            query = query.Where(assignment =>
                assignment.User.Role != UserRole.Admin &&
                assignment.User.Role != UserRole.Manager &&
                assignment.User.Role != UserRole.HumanResourceManager);

        return query;
    }

    private IQueryable<JobPost> JobPostsInUserScope()
    {
        var query = _db.JobPosts.AsQueryable();
        var provinceId = User.ScopeProvinceId();
        var cityId = User.ScopeCityId();
        return User.ScopeLevel() switch
        {
            "Mastery" => query,
            "Province" => query.Where(post => post.OrgUnit.ProvinceId == provinceId),
            "City" => query.Where(post => post.OrgUnit.CityId == cityId),
            _ => query.Where(_ => false)
        };
    }

    private bool CanManageJobPost(Guid provinceId, Guid? cityId) =>
        User.ScopeLevel() switch
        {
            "Mastery" => true,
            "Province" => User.ScopeProvinceId() == provinceId,
            "City" => User.ScopeCityId() == cityId,
            _ => false
        };

    private bool CanManageTargetUser(ApplicationUser target)
    {
        var currentRole = User.Role();
        if (target.Role == UserRole.FederationRep)
            return false;
        if (currentRole == nameof(UserRole.HumanResourceManager) &&
            target.Role is UserRole.Admin or UserRole.Manager)
            return false;
        if (currentRole == nameof(UserRole.HumanResourceOfficer) &&
            target.Role is UserRole.Admin or UserRole.Manager or UserRole.HumanResourceManager)
            return false;

        if (currentRole == nameof(UserRole.Admin) && User.ScopeLevel() == "City")
            return target.ScopeLevel == ScopeLevel.City &&
                   target.ScopeCityId == User.ScopeCityId();

        return IsTargetWithinScope(target) &&
               (User.ScopeLevel() != "City" || target.ScopeLevel == ScopeLevel.City);
    }

    private bool IsTargetWithinScope(ApplicationUser target) =>
        User.ScopeLevel() switch
        {
            "Mastery" => true,
            "Province" => target.ScopeProvinceId == User.ScopeProvinceId(),
            "City" => target.ScopeCityId == User.ScopeCityId(),
            _ => false
        };

    private async Task<bool> IsValidTargetAsync(Guid userId, Guid jobPostId) =>
        await _db.Users.AnyAsync(user => user.Id == userId && user.Role != UserRole.FederationRep) &&
        await _db.JobPosts.AnyAsync(post => post.Id == jobPostId);

    private async Task<bool> HasOverlappingAssignmentAsync(
        Guid userId,
        Guid jobPostId,
        DateOnly? startDate,
        DateOnly? endDate,
        Guid? excludeId = null)
    {
        var query = _db.JobPostAssignments
            .Where(assignment =>
                (assignment.UserId == userId || assignment.JobPostId == jobPostId) &&
                (!excludeId.HasValue || assignment.Id != excludeId.Value));
        if (startDate.HasValue)
            query = query.Where(assignment => !assignment.EndDate.HasValue || assignment.EndDate >= startDate);
        if (endDate.HasValue)
            query = query.Where(assignment => !assignment.StartDate.HasValue || assignment.StartDate <= endDate);
        return await query.AnyAsync();
    }

    private static bool IsValidDateRange(DateOnly? startDate, DateOnly? endDate) =>
        !startDate.HasValue || !endDate.HasValue || endDate.Value >= startDate.Value;

    private static bool Overlaps(
        DateOnly? firstStart,
        DateOnly? firstEnd,
        DateOnly? secondStart,
        DateOnly? secondEnd) =>
        (!firstEnd.HasValue || !secondStart.HasValue || firstEnd.Value >= secondStart.Value) &&
        (!secondEnd.HasValue || !firstStart.HasValue || secondEnd.Value >= firstStart.Value);

    private Guid? CurrentUserId()
    {
        var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(value, out var id) ? id : null;
    }

    private static JobPostAssignmentOutDto ToOutDto(
        JobPostAssignment assignment,
        ApplicationUser user,
        JobPost jobPost) =>
        new(
            assignment.Id,
            assignment.UserId,
            user.UserName ?? string.Empty,
            user.FirstName ?? string.Empty,
            user.LastName ?? string.Empty,
            assignment.JobPostId,
            jobPost.Title,
            jobPost.JobPostUniqueId,
            assignment.StartDate,
            assignment.EndDate,
            assignment.CreatedAtUtc);

    private sealed record AssignmentRange(
        Guid UserId,
        Guid JobPostId,
        DateOnly? StartDate,
        DateOnly? EndDate);
}
