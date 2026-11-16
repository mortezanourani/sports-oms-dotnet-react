namespace Backend.Models
{
    public class JobPostAssignment
    {
        public Guid Id { get; set; }

        public Guid JobPostId { get; set; }
        public JobPost JobPost { get; set; } = null!;
        public string JobPostName { get; set; } = string.Empty;

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public Guid ProvinceId { get; set; }
        public Province Province { get; set; } = null!;

        public Guid? CityId { get; set; }
        public City? City { get; set; }

        public DateOnly StartedOn { get; set; }
        public DateOnly? EndedOn { get; set; }
        public Guid? ChangedByUserId { get; set; }
    }
}
