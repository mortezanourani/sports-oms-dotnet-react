namespace Backend.Models
{
    public enum FederationCategory { 
        Championships,
        Para,
        General 
    }

    public enum FederationLevel { 
        National,
        Provincial,
        City 
    }

    public class Federation
    {
        public Guid Id { get; set; }

        // Only meaningful/set for National-level federations. Provincial
        // and City federations have no name of their own — their display
        // name is always composed from the national federation's name
        // plus the province/city name, computed in the DTO, never stored.
        public string? Name { get; set; }

        public FederationCategory Category { get; set; }
        public FederationLevel Level { get; set; }

        public Guid? ParentFederationId { get; set; }
        public Federation? ParentFederation { get; set; }
        public ICollection<Federation> ChildFederations { get; set; } = new List<Federation>();

        public Guid? ProvinceId { get; set; }
        public Province? Province { get; set; }

        public Guid? CityId { get; set; }
        public City? City { get; set; }

        public ICollection<FederationPosition> Positions { get; set; } = new List<FederationPosition>();
        public ICollection<Champion> Champions { get; set; } = new List<Champion>();
    }
}