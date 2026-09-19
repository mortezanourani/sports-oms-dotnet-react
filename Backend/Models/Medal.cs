namespace Backend.Models
{
    public enum MedalType
    {
        Gold,
        Silver,
        Bronze
    }

    public enum AgeGroup
    {
        Under14,
        Under18,
        Under21,
        Adult,
        Senior
    }

    public enum TournamentLevel
    {
        Local,
        Provincial,
        National,
        International
    }

    public class Medal
    {
        public Guid Id { get; set; }
        public MedalType MedalType { get; set; }
        public AgeGroup AgeGroup { get; set; }
        public TournamentLevel Level { get; set; }
        public string CompetitionName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Count { get; set; } = 1;
        public string? Field { get; set; }

        public Guid ChampionId { get; set; }
        public Champion Champion { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
