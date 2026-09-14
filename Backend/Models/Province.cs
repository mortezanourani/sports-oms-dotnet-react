namespace Backend.Models
{
    public class Province
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;

        public ICollection<City> Cities { get; set; } = new List<City>();
    }
}
