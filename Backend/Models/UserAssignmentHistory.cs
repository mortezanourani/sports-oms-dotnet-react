namespace Backend.Models
{
    public class UserAssignmentHistory
    {
        public Guid Id { get; set; }

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public UserRole Role { get; set; }
        public ScopeLevel ScopeLevel { get; set; }
        public Guid? ScopeProvinceId { get; set; }
        public Province? ScopeProvince { get; set; }
        public Guid? ScopeCityId { get; set; }
        public City? ScopeCity { get; set; }

        public DateTimeOffset? StartedAtUtc { get; set; }
        public DateTimeOffset? EndedAtUtc { get; set; }
        public bool IsLegacy { get; set; }
        public Guid? ChangedByUserId { get; set; }
    }
}
