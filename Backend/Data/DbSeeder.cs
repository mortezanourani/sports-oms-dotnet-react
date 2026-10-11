using Backend.Models;
using Microsoft.AspNetCore.Identity;

namespace Backend.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config)
        {
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

            var adminUsername = config["SeedAdmin:Username"] ?? "admin";
            var adminPassword = config["SeedAdmin:Password"] ?? "ChangeMe123!";

            var existing = await userManager.FindByNameAsync(adminUsername);
            if (existing != null) return; // already seeded, do nothing

            var admin = new ApplicationUser
            {
                UserName = adminUsername,
                Email = $"{adminUsername}@local",
                LastName = "مدیر سیستم",
                Role = UserRole.Admin,
                ScopeLevel = ScopeLevel.Mastery,
                EmailConfirmed = true
            };

            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                throw new Exception($"Failed to seed admin user: {errors}");
            }
        }
    }
}
