using BatalhaNaval.Domain.Entities;
using BatalhaNaval.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace BatalhaNaval.Infrastructure.Persistence;

public class BatalhaNavalDbContext : DbContext
{
    public BatalhaNavalDbContext(DbContextOptions<BatalhaNavalDbContext> options) : base(options)
    {
    }

    public DbSet<Match> Matches { get; set; }
    public DbSet<PlayerProfile> PlayerProfiles { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Medal> Medals { get; set; }
    public DbSet<UserMedal> UserMedals { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Aplica as configurações separadas (Mapeamento)
        modelBuilder.ApplyConfiguration(new MatchConfiguration());
        modelBuilder.ApplyConfiguration(new PlayerProfileConfiguration());
        modelBuilder.Entity<UserMedal>().HasKey(um => new { um.UserId, um.MedalId });

        base.OnModelCreating(modelBuilder);
    }
}