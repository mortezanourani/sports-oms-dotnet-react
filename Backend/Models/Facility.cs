namespace Backend.Models
{
    public enum FacilityType
    {
        Msy,
        Private,
        Gov
    }

    public class Facility
    {
        public Guid Id { get; set; }
        public FacilityType FacilityType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string SportType { get; set; } = string.Empty;
        public string? OwnerName { get; set; }
        public string? OwnerContact { get; set; }

        public Guid CityId { get; set; }
        public City City { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<FacilityContract> Contracts { get; set; } = new List<FacilityContract>();
        public ICollection<FacilityLicense> Licenses { get; set; } = new List<FacilityLicense>();
    }
}
