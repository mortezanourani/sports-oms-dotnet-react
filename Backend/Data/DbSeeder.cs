using Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Hosting;

namespace Backend.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAdminAsync(IServiceProvider services, IConfiguration config, IHostEnvironment environment)
        {
            var adminUsername = config["SeedAdmin:Username"];
            var adminPassword = config["SeedAdmin:Password"];
            if (string.IsNullOrWhiteSpace(adminUsername) || string.IsNullOrWhiteSpace(adminPassword))
            {
                if (environment.IsDevelopment() &&
                    string.IsNullOrWhiteSpace(adminUsername) &&
                    string.IsNullOrWhiteSpace(adminPassword))
                {
                    services.GetRequiredService<ILoggerFactory>()
                        .CreateLogger(nameof(DbSeeder))
                        .LogWarning("Admin bootstrap skipped in Development because SeedAdmin credentials are not configured.");
                    return;
                }

                throw new InvalidOperationException(
                    "SeedAdmin:Username and SeedAdmin:Password must both be configured. Development skips seeding only when both are absent.");
            }

            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
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
