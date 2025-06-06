using Microsoft.EntityFrameworkCore;
using MatchingApp.Models;

namespace MatchingApp.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Description> Descriptions { get; set; } = null!;
        public DbSet<Evaluation> Evaluations { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Description>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Content).IsRequired();
            });

            modelBuilder.Entity<Evaluation>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.SessionId).IsRequired();
                entity.Property(e => e.IsMatch).IsRequired();
                entity.Property(e => e.CreatedAt).IsRequired();

                entity.HasOne(e => e.Description1)
                    .WithMany()
                    .HasForeignKey(e => e.Description1Id)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(e => e.Description2)
                    .WithMany()
                    .HasForeignKey(e => e.Description2Id)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            base.OnModelCreating(modelBuilder);
        }
    }
}