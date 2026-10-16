using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Models;

namespace RaceDay.Api.Data
{
    public class RaceDayDbContext : DbContext
    {
        public RaceDayDbContext(DbContextOptions<RaceDayDbContext> options) : base(options)
        {
        }

        public DbSet<Role> Roles => Set<Role>();
        public DbSet<User> Users => Set<User>();
        public DbSet<Event> Events => Set<Event>();
        public DbSet<Category> Categories => Set<Category>();
        public DbSet<Enrolment> Enrolments => Set<Enrolment>();
        public DbSet<Result> Results => Set<Result>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Role
            modelBuilder.Entity<Role>(entity =>
            {
                entity.ToTable("Role");
                entity.HasKey(r => r.RoleID);
                entity.Property(r => r.RoleName).IsRequired().HasMaxLength(50);
            });

            // User
            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("User");
                entity.HasKey(u => u.UserID);
                entity.HasIndex(u => u.Email).IsUnique();
                entity.Property(u => u.Name).IsRequired().HasMaxLength(50);
                entity.Property(u => u.Email).IsRequired().HasMaxLength(50);
                entity.Property(u => u.PasswordHash).IsRequired().HasMaxLength(255);

                entity.HasOne(u => u.Role)
                      .WithMany(r => r.Users)
                      .HasForeignKey(u => u.RoleID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Event
            modelBuilder.Entity<Event>(entity =>
            {
                entity.ToTable("Event");
                entity.HasKey(e => e.EventID);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Description).HasMaxLength(150);
                entity.Property(e => e.Date).IsRequired();
                entity.Property(e => e.Location).HasMaxLength(100);
                entity.Property(e => e.Distance).HasColumnType("decimal(5,2)");
                entity.Property(e => e.Type).HasMaxLength(20);

                entity.HasOne(e => e.Organiser)
                      .WithMany(u => u.OrganisedEvents)
                      .HasForeignKey(e => e.OrganiserID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Category
            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Category");
                entity.HasKey(c => c.CategoryID);
                entity.Property(c => c.Name).IsRequired().HasMaxLength(50);
                entity.Property(c => c.Description).HasMaxLength(150);

                entity.HasOne(c => c.Event)
                      .WithMany(e => e.Categories)
                      .HasForeignKey(c => c.EventID)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // Enrolment
            modelBuilder.Entity<Enrolment>(entity =>
            {
                entity.ToTable("Enrolment");
                entity.HasKey(en => en.EnrolmentID);
                entity.Property(en => en.Status).HasMaxLength(20).HasDefaultValue("Pending");

                entity.HasOne(en => en.Event)
                      .WithMany(e => e.Enrolments)
                      .HasForeignKey(en => en.EventID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(en => en.Category)
                      .WithMany(c => c.Enrolments)
                      .HasForeignKey(en => en.CategoryID)
                      .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(en => en.Participant)
                      .WithMany(u => u.Enrolments)
                      .HasForeignKey(en => en.ParticipantID)
                      .OnDelete(DeleteBehavior.Restrict);
            });

            // Result
            modelBuilder.Entity<Result>(entity =>
            {
                entity.ToTable("Result");
                entity.HasKey(r => r.ResultID);

                entity.HasOne(r => r.Enrolment)
                      .WithMany(en => en.Results)
                      .HasForeignKey(r => r.EnrolmentID)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
