namespace Backend.Models
{
    public class FacilityLicense
    {
        public Guid Id { get; set; }
        public string LicenseNumber { get; set; } = string.Empty;
        public string HolderName { get; set; } = string.Empty;
        public DateOnly IssueDate { get; set; }
        public DateOnly? ExpiryDate { get; set; }
        public string? LicenseFilePath { get; set; }

        public Guid FacilityId { get; set; }
        public Facility Facility { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
