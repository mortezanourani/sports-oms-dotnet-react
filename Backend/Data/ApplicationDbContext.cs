using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Backend.Models;

namespace Backend.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Province> Provinces => Set<Province>();
        public DbSet<City> Cities => Set<City>();
        public DbSet<Federation> Federations => Set<Federation>();
        public DbSet<FederationPosition> FederationPositions => Set<FederationPosition>();
        public DbSet<Facility> Facilities => Set<Facility>();
        public DbSet<FacilityContract> FacilityContracts => Set<FacilityContract>();
        public DbSet<FacilityLicense> FacilityLicenses => Set<FacilityLicense>();
        public DbSet<AthleteCount> AthleteCounts => Set<AthleteCount>();
        public DbSet<Champion> Champions => Set<Champion>();
        public DbSet<Medal> Medals => Set<Medal>();
        public DbSet<Message> Messages => Set<Message>();
        public DbSet<MessageRecipient> MessageRecipients => Set<MessageRecipient>();
        public DbSet<MessageAttachment> MessageAttachments => Set<MessageAttachment>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            // Enum -> string conversions (readable in DB, matches the Django app's approach)
            builder.Entity<ApplicationUser>().Property(u => u.Role).HasConversion<string>();
            builder.Entity<ApplicationUser>().Property(u => u.ScopeLevel).HasConversion<string>();
            builder.Entity<FederationPosition>().Property(p => p.Position).HasConversion<string>();
            builder.Entity<Facility>().Property(f => f.FacilityType).HasConversion<string>();
            builder.Entity<AthleteCount>().Property(a => a.Gender).HasConversion<string>();
            builder.Entity<Champion>().Property(c => c.Gender).HasConversion<string>();
            builder.Entity<Medal>().Property(m => m.MedalType).HasConversion<string>();

            // Prevent double-messaging the same recipient
            builder.Entity<MessageRecipient>()
                .HasIndex(mr => new { mr.MessageId, mr.RecipientId })
                .IsUnique();

            // Prevent duplicate city names within the same province
            builder.Entity<City>()
                .HasIndex(c => new { c.ProvinceId, c.Name })
                .IsUnique();

            // Match the Django app's on_delete=PROTECT behavior (Restrict = block delete if children exist)
            builder.Entity<City>()
                .HasOne(c => c.Province)
                .WithMany(p => p.Cities)
                .HasForeignKey(c => c.ProvinceId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Champion>()
                .HasOne(c => c.Federation)
                .WithMany(f => f.Champions)
                .HasForeignKey(c => c.FederationId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Facility>()
                .HasOne(f => f.City)
                .WithMany()
                .HasForeignKey(f => f.CityId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<AthleteCount>()
                .HasOne(a => a.City)
                .WithMany()
                .HasForeignKey(a => a.CityId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
