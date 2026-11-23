namespace Backend.Models
{
    public class OrgUnit
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;

        public Guid? ParentOrgUnitId { get; set; }
        public OrgUnit? ParentOrgUnit { get; set; }
        public ICollection<OrgUnit> ChildOrgUnits { get; set; } = new List<OrgUnit>();
    }
}
