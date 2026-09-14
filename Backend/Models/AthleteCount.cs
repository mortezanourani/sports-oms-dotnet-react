namespace Backend.Models
{
    public enum Gender
    {
        Male,
        Female
    }

    public class AthleteCount
    {
        public Guid Id { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public Gender Gender { get; set; }
        public int Count { get; set; }

        public Guid FederationId { get; set; }
        public Federation Federation { get; set; } = null!;

        public Guid CityId { get; set; }
        public City City { get; set; } = null!;
    }
}
