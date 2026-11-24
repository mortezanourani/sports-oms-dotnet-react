using Backend.Data;
using Backend.DTOs;
using Backend.Models;
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

        public JobPostsController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var posts = await _db.JobPosts
                .OrderBy(post => post.Title)
                .Select(post => new JobPostOutDto(
                    post.Id,
                    post.JobPostTypeId,
                    post.OrgUnitId,
                    post.TypeCode,
                    post.Title,
                    post.ManagerType.ToString(),
                    post.BreakOut))
                .ToListAsync();
            return Ok(posts);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> Get(Guid id)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });
            return Ok(ToOutDto(post));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create(JobPostUpsertDto dto)
        {
            if (!TryParseJobPost(dto, out var typeCode, out var title, out var managerType, out var error))
                return BadRequest(new { detail = error });

            if (!await ReferencesExistAsync(dto.JobPostTypeId, dto.OrgUnitId))
                return BadRequest(new { detail = "نوع پست یا واحد سازمانی انتخاب‌شده معتبر نیست" });

            if (await _db.JobPosts.AnyAsync(post => post.Title == title))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });

            var post = new JobPost
            {
                Id = Guid.NewGuid(),
                JobPostTypeId = dto.JobPostTypeId,
                OrgUnitId = dto.OrgUnitId,
                TypeCode = typeCode,
                Title = title,
                ManagerType = managerType,
                BreakOut = dto.BreakOut
            };
            _db.JobPosts.Add(post);
            await _db.SaveChangesAsync();

            return CreatedAtAction(nameof(Get), new { id = post.Id }, ToOutDto(post));
        }

        [HttpPut("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Update(Guid id, JobPostUpsertDto dto)
        {
            var post = await _db.JobPosts.FindAsync(id);
            if (post == null) return NotFound(new { detail = "پست شغلی یافت نشد" });

            if (!TryParseJobPost(dto, out var typeCode, out var title, out var managerType, out var error))
                return BadRequest(new { detail = error });

            if (!await ReferencesExistAsync(dto.JobPostTypeId, dto.OrgUnitId))
                return BadRequest(new { detail = "نوع پست یا واحد سازمانی انتخاب‌شده معتبر نیست" });

            if (await _db.JobPosts.AnyAsync(candidate => candidate.Id != id && candidate.Title == title))
                return Conflict(new { detail = "این عنوان پست قبلا ثبت شده است" });

            post.JobPostTypeId = dto.JobPostTypeId;
            post.OrgUnitId = dto.OrgUnitId;
            post.TypeCode = typeCode;
            post.Title = title;
            post.ManagerType = managerType;
            post.BreakOut = dto.BreakOut;
            await _db.SaveChangesAsync();

            return Ok(ToOutDto(post));
        }

        private async Task<bool> ReferencesExistAsync(Guid jobPostTypeId, Guid orgUnitId) =>
            await _db.JobPostTypes.AnyAsync(type => type.Id == jobPostTypeId) &&
            await _db.OrgUnits.AnyAsync(unit => unit.Id == orgUnitId);

        private static bool TryParseJobPost(
            JobPostUpsertDto dto,
            out string typeCode,
            out string title,
            out JobPostManagerType managerType,
            out string error)
        {
            typeCode = dto.TypeCode?.Trim() ?? string.Empty;
            title = dto.Title?.Trim() ?? string.Empty;

            if (typeCode.Length == 0)
            {
                managerType = default;
                error = "کد نوع پست الزامی است";
                return false;
            }

            if (title.Length is 0 or > 160)
            {
                managerType = default;
                error = "عنوان پست الزامی است و باید حداکثر ۱۶۰ نویسه باشد";
                return false;
            }

            if (!Enum.TryParse(dto.ManagerType, out managerType) || !Enum.IsDefined(managerType))
            {
                error = "نوع مدیر حرفه‌ای معتبر نیست";
                return false;
            }

            error = string.Empty;
            return true;
        }

        private static JobPostOutDto ToOutDto(JobPost post) =>
            new(
                post.Id,
                post.JobPostTypeId,
                post.OrgUnitId,
                post.TypeCode,
                post.Title,
                post.ManagerType.ToString(),
                post.BreakOut);
    }
}
