using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services
{
    public class JobPostAssignmentService
    {
        private readonly ApplicationDbContext _db;

        public JobPostAssignmentService(ApplicationDbContext db) => _db = db;

        public async Task<(JobPostAssignment? Assignment, string? Error)> AssignAsync(
            JobPost jobPost,
            ApplicationUser user,
            Guid provinceId,
            Guid? cityId,
            DateOnly startedOn,
            Guid changedByUserId)
        {
            if (!jobPost.IsActive)
                return (null, "پست انتخاب‌شده غیرفعال است");

            var isCityAssignment = cityId.HasValue;
            if ((jobPost.PlacementScope == JobPostPlacementScope.Province && isCityAssignment) ||
                (jobPost.PlacementScope == JobPostPlacementScope.City && !isCityAssignment))
                return (null, "این پست در این سطح جغرافیایی قابل تخصیص نیست");

            if (startedOn > DateOnly.FromDateTime(TehranDateTime.TodayUtc))
                return (null, "تاریخ شروع نمی‌تواند در آینده باشد");

            var previousAssignments = await _db.JobPostAssignments
                .Where(assignment => assignment.UserId == user.Id)
                .OrderByDescending(assignment => assignment.StartedOn)
                .ToListAsync();
            var currentAssignment = previousAssignments.SingleOrDefault(assignment => !assignment.EndedOn.HasValue);

            if (currentAssignment != null)
            {
                if (startedOn <= currentAssignment.StartedOn)
                    return (null, "تاریخ شروع پست جدید باید بعد از تاریخ شروع پست فعلی باشد");

                currentAssignment.EndedOn = startedOn.AddDays(-1);
            }

            if (previousAssignments.Any(assignment =>
                    assignment != currentAssignment &&
                    assignment.StartedOn <= startedOn &&
                    (!assignment.EndedOn.HasValue || assignment.EndedOn.Value >= startedOn)))
                return (null, "بازه تاریخ این تخصیص با سابقه قبلی کاربر تداخل دارد");

            var newAssignment = new JobPostAssignment
            {
                Id = Guid.NewGuid(),
                JobPostId = jobPost.Id,
                JobPostName = jobPost.Name,
                UserId = user.Id,
                ProvinceId = provinceId,
                CityId = cityId,
                StartedOn = startedOn,
                ChangedByUserId = changedByUserId
            };

            _db.JobPostAssignments.Add(newAssignment);
            await _db.SaveChangesAsync();
            return (newAssignment, null);
        }

        public async Task<string?> EndAsync(JobPostAssignment assignment, DateOnly endedOn)
        {
            if (assignment.EndedOn.HasValue)
                return "این تخصیص قبلا پایان یافته است";

            if (endedOn < assignment.StartedOn || endedOn > DateOnly.FromDateTime(TehranDateTime.TodayUtc))
                return "تاریخ پایان باید بین تاریخ شروع و امروز باشد";

            assignment.EndedOn = endedOn;
            await _db.SaveChangesAsync();
            return null;
        }
    }
}
