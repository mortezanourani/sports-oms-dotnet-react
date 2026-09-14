using Microsoft.AspNetCore.Identity;

namespace Backend.Models
{
    public enum UserRole
    {
        Admin,
        Manager,
        M88Officer,
        M5Officer,
        InsuranceOfficer,
        SportOfficer,
        FederationRep
    }

    public enum ScopeLevel
    {
        National,
        Province,
        City
    }

    public class ApplicationUser : IdentityUser<Guid>
    {
        public UserRole Role { get; set; }
        public ScopeLevel ScopeLevel { get; set; } = ScopeLevel.National;

        public Guid? ScopeProvinceId { get; set; }
        public Province? ScopeProvince { get; set; }

        public Guid? ScopeCityId { get; set; }
        public City? ScopeCity { get; set; }

        public string? FullName { get; set; }
    }
}
