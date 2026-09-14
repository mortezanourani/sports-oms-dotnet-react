using System.Security.Claims;

namespace Backend.Extensions
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? ScopeLevel(this ClaimsPrincipal user) =>
            user.FindFirst("scope_level")?.Value;

        public static Guid? ScopeProvinceId(this ClaimsPrincipal user)
        {
            var val = user.FindFirst("scope_province_id")?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }

        public static Guid? ScopeCityId(this ClaimsPrincipal user)
        {
            var val = user.FindFirst("scope_city_id")?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }
    }
}
