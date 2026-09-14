namespace Backend.Models
{
    public class City
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public Guid ProvinceId { get; set; }
        public Province Province { get; set; } = null!;
    }
}
