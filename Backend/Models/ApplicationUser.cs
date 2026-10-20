using Microsoft.AspNetCore.Identity;

namespace Backend.Models
{
    public enum UserRole
    {
        Admin,
        Manager,
        HumanResourceManager,
        HumanResourceOfficer,
        SportsManager,
        SportsOfficer,
        M88Manager,
        M88Officer,
        M5Manager,
        M5Officer,
        InsuranceOfficer,
        UnAssigned,
        FederationRep
    }

    public enum ScopeLevel
    {
        Mastery,
        Province,
        City
    }

    public class ApplicationUser : IdentityUser<Guid>
    {
        public UserRole Role { get; set; }
        public ScopeLevel ScopeLevel { get; set; } = ScopeLevel.Mastery;

        public Guid? ScopeProvinceId { get; set; }
        public Province? ScopeProvince { get; set; }

        public Guid? ScopeCityId { get; set; }
        public City? ScopeCity { get; set; }

        public string? FirstName { get; set; }
        
        public string? LastName { get; set; }
        
        public string? FatherName { get; set; }

        public Gender? Gender { get; set; }
    }
}
