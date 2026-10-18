namespace Backend.Models
{
    public enum FederationPositionType
    {
        President,
        Miss,
        Vice
    }

    public class FederationPosition
    {
        public Guid Id { get; set; }
        public FederationPositionType Position { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; } // null = currently active

        public Guid UserId { get; set; }
        public ApplicationUser User { get; set; } = null!;

        public Guid FederationId { get; set; }
        public Federation Federation { get; set; } = null!;
    }
}
