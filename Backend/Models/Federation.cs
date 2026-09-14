namespace Backend.Models
{
    public class Federation
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string SportType { get; set; } = string.Empty;

        public ICollection<FederationPosition> Positions { get; set; } = new List<FederationPosition>();
        public ICollection<Champion> Champions { get; set; } = new List<Champion>();
    }
}
