namespace Backend.Models
{
    public enum MedalType
    {
        Gold,
        Silver,
        Bronze
    }

    public class Medal
    {
        public Guid Id { get; set; }
        public MedalType MedalType { get; set; }
        public string CompetitionName { get; set; } = string.Empty;
        public int Year { get; set; }

        public Guid ChampionId { get; set; }
        public Champion Champion { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
