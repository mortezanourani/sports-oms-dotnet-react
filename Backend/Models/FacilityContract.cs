namespace Backend.Models
{
    public class FacilityContract
    {
        public Guid Id { get; set; }
        public string? ContractNumber { get; set; }
        public string ThirdPartyName { get; set; } = string.Empty;
        public string? ThirdPartyContact { get; set; }
        public DateOnly StartDate { get; set; }
        public DateOnly? EndDate { get; set; }
        public string? ContractFilePath { get; set; }

        public Guid FacilityId { get; set; }
        public Facility Facility { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
