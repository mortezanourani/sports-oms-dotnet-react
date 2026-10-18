namespace Backend.Models
{
    public class Champion
    {
        public Guid Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public DateTime? BirthDate { get; set; }
        public Gender Gender { get; set; }
        public string? PhotoPath { get; set; }

        public Guid FederationId { get; set; }
        public Federation Federation { get; set; } = null!;

        public Guid? CityId { get; set; }
        public City? City { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<Medal> Medals { get; set; } = new List<Medal>();
    }
}
