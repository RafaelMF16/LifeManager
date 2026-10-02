using LifeManager.Domain.Auth;
using LifeManager.Domain.Categories;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.Transactions;
using LifeManager.Domain.Users;
using LifeManager.Domain.UsersPreferences;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Postgres
{
    public class LifeManagerDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }
        public DbSet<UserPreferences> UserPreferences { get; set; }
        public DbSet<Category> Categories { get; set; }
        public DbSet<MonthlySummary> MonthlySummaries { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeManagerDbContext).Assembly);
        }
    }
}