using System.Security.Claims;

namespace Backend.Extensions
{
    public static class AuthClaimTypes
    {
        public const string Role = "role";
        public const string ScopeLevel = "scope_level";
        public const string ScopeProvinceId = "scope_province_id";
        public const string ScopeCityId = "scope_city_id";
        public const string FullName = "full_name";
    }

    public static class ClaimsPrincipalExtensions
    {
        public static string? ScopeLevel(this ClaimsPrincipal user) =>
            user.FindFirst(AuthClaimTypes.ScopeLevel)?.Value;

        public static Guid? ScopeProvinceId(this ClaimsPrincipal user)
        {
            var val = user.FindFirst(AuthClaimTypes.ScopeProvinceId)?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }

        public static Guid? ScopeCityId(this ClaimsPrincipal user)
        {
            var val = user.FindFirst(AuthClaimTypes.ScopeCityId)?.Value;
            return Guid.TryParse(val, out var id) ? id : null;
        }

        public static string? Role(this ClaimsPrincipal user) =>
            user.FindFirst(AuthClaimTypes.Role)?.Value;
    }
}
