using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services
{
    public class FederationPositionService
    {
        private readonly ApplicationDbContext _db;

        public FederationPositionService(ApplicationDbContext db) => _db = db;

        public async Task<(FederationPosition? Position, string? Error)> AssignAsync(
            ApplicationUser user,
            Federation federation,
            FederationPositionType position,
            DateOnly startDate,
            DateOnly? endDate,
            bool persistCalculatedEndDateWhenUnspecified = true)
        {
            DateOnly termEndDate;
            try
            {
                termEndDate = startDate.AddYears(1);
            }
            catch (ArgumentOutOfRangeException)
            {
                return (null, "تاریخ شروع سمت معتبر نیست");
            }

            if (endDate.HasValue && (endDate.Value < startDate || endDate.Value > termEndDate))
                return (null, "تاریخ پایان سمت باید بین تاریخ شروع و حداکثر یک سال پس از آن باشد");

            var assignments = await _db.FederationPositions
                .Where(p => p.UserId == user.Id && p.FederationId == federation.Id)
                .ToListAsync();

            var latestSamePosition = assignments
                .Where(p => p.Position == position)
                .OrderByDescending(p => p.StartDate)
                .FirstOrDefault();
            if (latestSamePosition != null)
            {
                DateOnly nextEligibleDate;
                try
                {
                    nextEligibleDate = latestSamePosition.StartDate.AddYears(1);
                }
                catch (ArgumentOutOfRangeException)
                {
                    nextEligibleDate = DateOnly.MaxValue;
                }

                if (startDate < nextEligibleDate)
                    return (null, "این کاربر در یک سال گذشته همین سمت را در این فدراسیون داشته است");
            }

            var newEndDate = endDate ?? termEndDate;
            foreach (var assignment in assignments.Where(p => p.Position != position))
            {
                var existingEndDate = assignment.EndDate ?? GetTermEndDate(assignment.StartDate);
                if (assignment.StartDate < newEndDate && existingEndDate > startDate)
                    return (null, "این کاربر در این بازه زمانی سمت دیگری در همین فدراسیون دارد");
            }

            var newPosition = new FederationPosition
            {
                UserId = user.Id,
                FederationId = federation.Id,
                Position = position,
                StartDate = startDate,
                EndDate = persistCalculatedEndDateWhenUnspecified || endDate.HasValue
                    ? newEndDate
                    : null
            };

            _db.FederationPositions.Add(newPosition);
            await _db.SaveChangesAsync();
            return (newPosition, null);
        }

        public static DateOnly GetTermEndDate(DateOnly startDate)
        {
            try
            {
                return startDate.AddYears(1);
            }
            catch (ArgumentOutOfRangeException)
            {
                return DateOnly.MaxValue;
            }
        }
    }
}
