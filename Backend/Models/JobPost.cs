namespace Backend.Models
{
    public enum JobPostPlacementScope
    {
        Province,
        City,
        Both
    }

    public class JobPost
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public JobPostPlacementScope PlacementScope { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
