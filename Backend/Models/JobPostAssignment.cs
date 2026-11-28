namespace Backend.Models;

public class JobPostAssignment
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }
    public ApplicationUser User { get; set; } = null!;

    public Guid JobPostId { get; set; }
    public JobPost JobPost { get; set; } = null!;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid? CreatedByUserId { get; set; }
}
