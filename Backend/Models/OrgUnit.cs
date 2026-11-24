using System.ComponentModel.DataAnnotations;

namespace Backend.Models
{
    public enum OrgUnitType
    {
        [Display(Name = "اداره کل")]
        GeneralAdministration,
        [Display(Name = "اداره")]
        Administration,
        [Display(Name = "گروه")]
        Group,
        [Display(Name = "نمایندگی")]
        Representation,
        [Display(Name = "مجموعه ورزشی")]
        SportsComplex
    }

    public enum OrgUnitScope
    {
        [Display(Name = "استانی")]
        Provincial,
        [Display(Name = "شهرستانی")]
        City
    }

    public class OrgUnit
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public OrgUnitType Type { get; set; }
        public OrgUnitScope Scope { get; set; }

        public Guid? ParentOrgUnitId { get; set; }
        public OrgUnit? ParentOrgUnit { get; set; }
        public ICollection<OrgUnit> ChildOrgUnits { get; set; } = new List<OrgUnit>();
    }
}
