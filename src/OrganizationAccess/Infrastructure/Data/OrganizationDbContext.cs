using Microsoft.EntityFrameworkCore;
using SupportHub.OrganizationAccesss.Domain.Entities;

namespace SupportHub.OrganizationAccesss.Infrastructure.Data
{
    public class OrganizationDbContext : DbContext
    {
        public OrganizationDbContext(DbContextOptions<OrganizationDbContext> options) : base(options)
        {
            //Set a default command timeout for all database operations to prevent long-running queries from hanging indefinitely
            Database.SetCommandTimeout(TimeSpan.FromSeconds(30));
        }

        public DbSet<User> Users => Set<User>();
        public DbSet<Organization> Organizations => Set<Organization>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            //Configure enums as strings
            modelBuilder
                .Entity<User>()
                .Property(u => u.Role)
                .HasConversion<string>();

            modelBuilder
                .Entity<User>()
                .Property(u => u.InvitationStatus)
                .HasConversion<string>();

            modelBuilder
                .Entity<Organization>()
                .Property(o => o.Tier)
                .HasConversion<string>();

            modelBuilder
                .Entity<Organization>()
                .Property(o => o.CreationConfirmationStatus)
                .HasConversion<string>();

            modelBuilder
                .Entity<Organization>()
                .Property(o => o.Tier)
                .HasConversion<string>();

            //User Configuration
            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(us => us.Id);

                entity.HasIndex(us => us.OrganizationId);
            });

            //OrganizationSnapshot Configuration
            modelBuilder.Entity<Organization>(entity =>
            {
                entity.HasKey(os => os.Id);

                entity.HasMany(os => os.Users)
                      .WithOne(u => u.Organization)
                      .HasForeignKey(u => u.OrganizationId)
                      .OnDelete(DeleteBehavior.Restrict); //Prevent deleting an organization if it has users
            });
        }
    }
}