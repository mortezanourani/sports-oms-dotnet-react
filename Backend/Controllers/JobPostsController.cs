using Backend.Data;
using Backend.DTOs;
using Backend.Extensions;
using Backend.Models;
using Backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/job-posts")]
    [Authorize]
    public class JobPostsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly JobPostAssignmentService _assignmentService;

        public JobPostsController(ApplicationDbContext db, JobPostAssignmentService assignmentService)
        {
            _db = db;
            _assignmentService = assignmentService;
        }

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var posts = await _db.JobPosts
                .OrderBy(post => post.Name)
                .Select(post => new JobPostOutDto(
                    post.Id, post.Name, post.PlacementScope.ToString(), post.IsActive))
                .ToListAsync();
            return Ok(posts);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });
            return Ok(new JobPostOutDto(
                post.Id, post.Name, post.PlacementScope.ToString(), post.IsActive));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(JobPostUpsertDto dto)
        {
            if (!TryParseJobPost(dto, out var name, out var placementScope, out var error))
                return BadRequest(new { detail = error });

            if (await _db.JobPosts.AnyAsync(post => post.Name == name))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });

            var post = new JobPost
            {
                Id = Guid.NewGuid(),
                Name = name,
                PlacementScope = placementScope,
                IsActive = dto.IsActive
            };
            _db.JobPosts.Add(post);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = post.Id }, new JobPostOutDto(
                post.Id, post.Name, post.PlacementScope.ToString(), post.IsActive));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, JobPostUpsertDto dto)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });

            if (!TryParseJobPost(dto, out var name, out var placementScope, out var error))
                return BadRequest(new { detail = error });

            if (await _db.JobPosts.AnyAsync(candidate => candidate.Id != id && candidate.Name == name))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });

            var hasIncompatibleAssignments = placementScope switch
            {
                JobPostPlacementScope.Province => await _db.JobPostAssignments
                    .AnyAsync(assignment => assignment.JobPostId == id && assignment.CityId.HasValue),
                JobPostPlacementScope.City => await _db.JobPostAssignments
                    .AnyAsync(assignment => assignment.JobPostId == id && !assignment.CityId.HasValue),
                _ => false
            };
            if (hasIncompatibleAssignments)
                return Conflict(new { detail = "به دلیل وجود سوابق تخصیص در سطح جغرافیایی دیگر، امکان تغییر دامنه این پست وجود ندارد" });

            post.Name = name;
            post.PlacementScope = placementScope;
            post.IsActive = dto.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new JobPostOutDto(post.Id, post.Name, post.PlacementScope.ToString(), post.IsActive));
        }

        [HttpGet("assignments")]
        [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer")]
        public async Task<IActionResult> ListAssignments(
            Guid? jobPostId,
            Guid? provinceId,
            Guid? cityId)
        {
            if (cityId.HasValue)
            {
                var city = await _db.Cities.FindAsync(cityId.Value);
                if (city == null) return BadRequest(new { detail = "شهر انتخاب‌شده معتبر نیست" });
                if (provinceId.HasValue && provinceId != city.ProvinceId)
                    return BadRequest(new { detail = "شهر انتخاب‌شده متعلق به استان مشخص‌شده نیست" });
                if (!CanManageCity(city.Id, city.ProvinceId)) return Forbid();
                provinceId = city.ProvinceId;
            }
            else if (provinceId.HasValue)
            {
                if (!await _db.Provinces.AnyAsync(province => province.Id == provinceId.Value))
                    return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });
                if (!CanManageProvince(provinceId.Value)) return Forbid();
            }

            var query = _db.JobPostAssignments.AsQueryable();
            if (jobPostId.HasValue) query = query.Where(assignment => assignment.JobPostId == jobPostId.Value);
            if (provinceId.HasValue) query = query.Where(assignment => assignment.ProvinceId == provinceId.Value);
            if (cityId.HasValue) query = query.Where(assignment => assignment.CityId == cityId.Value);

            switch (User.ScopeLevel())
            {
                case "Mastery":
                    break;
                case "Province" when User.ScopeProvinceId().HasValue:
                    query = query.Where(assignment => assignment.ProvinceId == User.ScopeProvinceId());
                    break;
                case "City" when User.ScopeCityId().HasValue:
                    query = query.Where(assignment => assignment.CityId == User.ScopeCityId());
                    break;
                default:
                    return Forbid();
            }

            var assignments = await query
                .OrderByDescending(assignment => assignment.StartedOn)
                .Select(assignment => new JobPostAssignmentOutDto(
                    assignment.Id,
                    assignment.JobPostId,
                    assignment.JobPostName,
                    assignment.UserId,
                    assignment.User.UserName ?? "",
                    assignment.User.FirstName ?? "",
                    assignment.User.LastName ?? "",
                    assignment.ProvinceId,
                    assignment.Province.Name,
                    assignment.CityId,
                    assignment.City != null ? assignment.City.Name : null,
                    assignment.StartedOn,
                    assignment.EndedOn,
                    _db.Users                    .Where(actor => actor.Id == assignment.ChangedByUserId)
                    .Select(actor => actor.UserName)
                    .FirstOrDefault()))
                .ToListAsync();
            return Ok(assignments);
        }

        [HttpPost("assignments")]
        [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer")]
        public async Task<IActionResult> Assign(JobPostAssignmentCreateDto dto)
        {
            var post = await _db.JobPosts.FindAsync(dto.JobPostId);
            if (post == null) return BadRequest(new { detail = "پست انتخاب‌شده معتبر نیست" });

            var isProvinceAssignment = !dto.CityId.HasValue;
            if (post.PlacementScope == JobPostPlacementScope.Province && !isProvinceAssignment)
                return BadRequest(new { detail = "پست استانی فقط در سطح استان قابل تخصیص است" });
            if (post.PlacementScope == JobPostPlacementScope.City && isProvinceAssignment)
                return BadRequest(new { detail = "پست شهری فقط در سطح شهرستان قابل تخصیص است" });

            var user = await _db.Users.FirstOrDefaultAsync(candidate =>
                candidate.Id == dto.UserId && candidate.Role != UserRole.FederationRep);
            if (user == null) return BadRequest(new { detail = "کاربر انتخاب‌شده معتبر نیست" });
            if (!CanManageTargetUser(user)) return Forbid();

            if (!dto.ProvinceId.HasValue)
                return BadRequest(new { detail = "انتخاب استان الزامی است" });

            if (dto.CityId.HasValue)
            {
                var city = await _db.Cities.FindAsync(dto.CityId.Value);
                if (city == null || city.ProvinceId != dto.ProvinceId.Value)
                    return BadRequest(new { detail = "شهر انتخاب‌شده متعلق به استان نیست" });
                if (!CanManageCity(city.Id, city.ProvinceId)) return Forbid();
            }
            else
            {
                if (!await _db.Provinces.AnyAsync(province => province.Id == dto.ProvinceId.Value))
                    return BadRequest(new { detail = "استان انتخاب‌شده معتبر نیست" });
                if (!CanManageProvince(dto.ProvinceId.Value)) return Forbid();
            }

            var (assignment, error) = await _assignmentService.AssignAsync(
                post, user, dto.ProvinceId.Value, dto.CityId, dto.StartedOn, CurrentUserId);
            if (error != null) return Conflict(new { detail = error });

            return Ok(new { success = true, assignmentId = assignment!.Id });
        }

        [HttpPost("assignments/{id:guid}/end")]
        [Authorize(Roles = "Admin,Manager,HumanResourceManager,HumanResourceOfficer")]
        public async Task<IActionResult> EndAssignment(Guid id, JobPostAssignmentEndDto dto)
        {
            var assignment = await _db.JobPostAssignments
                .Include(candidate => candidate.User)
                .FirstOrDefaultAsync(candidate => candidate.Id == id);
            if (assignment == null) return NotFound(new { detail = "سوابق تخصیص یافت نشد" });
            if (!CanManageTargetUser(assignment.User)) return Forbid();
            if (assignment.CityId.HasValue)
            {
                if (!CanManageCity(assignment.CityId.Value, assignment.ProvinceId)) return Forbid();
            }
            else if (!CanManageProvince(assignment.ProvinceId))
            {
                return Forbid();
            }

            var error = await _assignmentService.EndAsync(assignment, dto.EndedOn);
            if (error != null) return Conflict(new { detail = error });
            return Ok(new { success = true });
        }

        private static bool TryParseJobPost(
            JobPostUpsertDto dto,
            out string name,
            out JobPostPlacementScope placementScope,
            out string error)
        {
            name = dto.Name?.Trim() ?? string.Empty;
            if (name.Length is 0 or > 160)
            {
                placementScope = default;
                error = "عنوان پست الزامی است و باید حداکثر ۱۶۰ نویسه باشد";
                return false;
            }

            if (!Enum.TryParse(dto.PlacementScope, out placementScope) || !Enum.IsDefined(placementScope))
            {
                error = "دامنه جغرافیایی پست معتبر نیست";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private Guid CurrentUserId =>
            Guid.TryParse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value, out var id)
                ? id
                : throw new UnauthorizedAccessException("شناسه کاربر واردشده معتبر نیست");

        private bool CanManageProvince(Guid provinceId) =>
            User.ScopeLevel() == "Mastery" ||
            User.ScopeLevel() == "Province" && User.ScopeProvinceId() == provinceId;

        private bool CanManageCity(Guid cityId, Guid provinceId) =>
            User.ScopeLevel() == "Mastery" ||
            User.ScopeLevel() == "Province" && User.ScopeProvinceId() == provinceId ||
            User.ScopeLevel() == "City" && User.ScopeCityId() == cityId;

        private bool CanManageTargetUser(ApplicationUser user)
        {
            var role = User.Role();
            var restrictedTargetRole =
                (role == "HumanResourceOfficer" &&
                 user.Role is (UserRole.Admin or UserRole.Manager or UserRole.HumanResourceManager)) ||
                (role == "HumanResourceManager" &&
                 user.Role is (UserRole.Admin or UserRole.Manager));
            if (user.Role == UserRole.FederationRep ||
                restrictedTargetRole)
                return false;

            return User.ScopeLevel() switch
            {
                "Mastery" => true,
                "Province" => user.ScopeProvinceId == User.ScopeProvinceId() &&
                              user.ScopeLevel is (ScopeLevel.Province or ScopeLevel.City),
                "City" => user.ScopeLevel == ScopeLevel.City && user.ScopeCityId == User.ScopeCityId(),
                _ => false
            };
        }
    }
}
