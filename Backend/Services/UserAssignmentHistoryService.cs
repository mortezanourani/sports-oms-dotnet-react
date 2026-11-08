using Backend.Data;
using Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace Backend.Services
{
    public class UserAssignmentHistoryService
    {
        private readonly ApplicationDbContext _db;

        public UserAssignmentHistoryService(ApplicationDbContext db)
        {
            _db = db;
        }

        public UserAssignmentHistory AddInitialAssignment(
            ApplicationUser user,
            Guid? changedByUserId)
        {
            var history = CreateEntry(user, DateTimeOffset.UtcNow, changedByUserId);
            _db.UserAssignmentHistories.Add(history);
            return history;
        }

        public async Task RecordChangeAsync(
            ApplicationUser user,
            UserRole previousRole,
            ScopeLevel previousScopeLevel,
            Guid? previousProvinceId,
            Guid? previousCityId,
            Guid changedByUserId)
        {
            var changedAt = DateTimeOffset.UtcNow;
            var currentAssignment = await _db.UserAssignmentHistories
                .SingleOrDefaultAsync(history =>
                    history.UserId == user.Id && history.EndedAtUtc == null);

            if (currentAssignment == null)
            {
                _db.UserAssignmentHistories.Add(new UserAssignmentHistory
                {
                    Id = Guid.NewGuid(),
                    UserId = user.Id,
                    Role = previousRole,
                    ScopeLevel = previousScopeLevel,
                    ScopeProvinceId = previousProvinceId,
                    ScopeCityId = previousCityId,
                    EndedAtUtc = changedAt,
                    IsLegacy = true
                });
            }
            else
            {
                currentAssignment.EndedAtUtc = changedAt;
            }

            _db.UserAssignmentHistories.Add(CreateEntry(user, changedAt, changedByUserId));
        }

        private static UserAssignmentHistory CreateEntry(
            ApplicationUser user,
            DateTimeOffset startedAtUtc,
            Guid? changedByUserId) => new()
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Role = user.Role,
            ScopeLevel = user.ScopeLevel,
            ScopeProvinceId = user.ScopeProvinceId,
            ScopeCityId = user.ScopeCityId,
            StartedAtUtc = startedAtUtc,
            ChangedByUserId = changedByUserId
        };
    }
}
