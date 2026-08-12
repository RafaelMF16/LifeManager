using LifeManager.Domain.Auth;
using LifeManager.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Postgres
{
    public class LifeManagerDbContext(DbContextOptions options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<RefreshToken> RefreshTokens { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeManagerDbContext).Assembly);
        }
    }
}