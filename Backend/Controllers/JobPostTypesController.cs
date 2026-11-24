using Backend.Data;
using Backend.DTOs;
using Backend.Models;
using Backend.Services;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Backend.Controllers
{
    [ApiController]
    [Route("api/job-post-types")]
    [Authorize(Roles = "Admin")]
    public class JobPostTypesController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public JobPostTypesController(ApplicationDbContext db) => _db = db;

        [HttpGet]
        public async Task<IActionResult> List()
        {
            var types = await _db.JobPostTypes
                .OrderBy(type => type.Title)
                .Select(type => new JobPostTypeOutDto(type.Id, type.Title))
                .ToListAsync();
            return Ok(types);
        }

        [HttpPost]
        public async Task<IActionResult> Create(JobPostTypeUpsertDto dto)
        {
            var title = NormalizeTitle(dto.Title);
            if (title.Length == 0)
                return BadRequest(new { detail = "عنوان نوع پست الزامی است" });
            if (await _db.JobPostTypes.AnyAsync(type => type.Title == title))
                return Conflict(new { detail = "نوع پستی با این عنوان قبلا ثبت شده است" });

            var type = new JobPostType { Id = Guid.NewGuid(), Title = title };
            _db.JobPostTypes.Add(type);
            await _db.SaveChangesAsync();
            return CreatedAtAction(nameof(List), new JobPostTypeOutDto(type.Id, type.Title));
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, JobPostTypeUpsertDto dto)
        {
            var type = await _db.JobPostTypes.FindAsync(id);
            if (type == null)
                return NotFound(new { detail = "نوع پست یافت نشد" });

            var title = NormalizeTitle(dto.Title);
            if (title.Length == 0)
                return BadRequest(new { detail = "عنوان نوع پست الزامی است" });
            if (await _db.JobPostTypes.AnyAsync(candidate => candidate.Id != id && candidate.Title == title))
                return Conflict(new { detail = "نوع پستی با این عنوان قبلا ثبت شده است" });

            type.Title = title;
            await _db.SaveChangesAsync();
            return Ok(new JobPostTypeOutDto(type.Id, type.Title));
        }

        [HttpGet("import/template")]
        public IActionResult DownloadImportTemplate() =>
            File(
                JobPostTypeImportWorkbook.CreateTemplate(),
                JobPostTypeImportWorkbook.ContentType,
                "job-post-types-import-template.xlsx");

        [HttpPost("import")]
        [RequestSizeLimit(10_000_000)]
        public async Task<IActionResult> Import([FromForm] IFormFile? file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { detail = "لطفا فایل اکسل را انتخاب کنید" });
            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { detail = "فرمت فایل باید Excel (.xlsx) باشد" });

            IReadOnlyList<JobPostTypeImportRow> rows;
            try
            {
                await using var stream = file.OpenReadStream();
                rows = JobPostTypeImportWorkbook.Read(stream);
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

            var existingTitles = await _db.JobPostTypes
                .Select(type => type.Title)
                .ToListAsync();
            var knownTitles = new HashSet<string>(existingTitles, StringComparer.Ordinal);
            var errors = new List<JobPostTypeImportErrorDto>();
            var created = 0;
            var skippedExisting = 0;

            foreach (var row in rows)
            {
                if (string.IsNullOrWhiteSpace(row.Title))
                {
                    errors.Add(new JobPostTypeImportErrorDto(
                        row.RowNumber,
                        "عنوان نوع پست الزامی است."));
                    continue;
                }

                var title = NormalizeTitle(row.Title);
                if (!knownTitles.Add(title))
                {
                    skippedExisting++;
                    continue;
                }

                _db.JobPostTypes.Add(new JobPostType { Id = Guid.NewGuid(), Title = title });
                created++;
            }

            await _db.SaveChangesAsync();
            return Ok(new JobPostTypeImportResultDto(created, skippedExisting, errors.Count, errors));
        }

        private static string NormalizeTitle(string? title) => title?.Trim() ?? string.Empty;
    }
}
