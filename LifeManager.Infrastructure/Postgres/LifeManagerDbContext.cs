using LifeManager.Domain.Auth;
using LifeManager.Domain.Budgets;
using LifeManager.Domain.Categories;
using LifeManager.Domain.Habits;
using LifeManager.Domain.Rewards;
using LifeManager.Domain.MonthlySummaries;
using LifeManager.Domain.RecurringTransactions;
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
        public DbSet<RecurringTransaction> RecurringTransactions { get; set; }
        public DbSet<Budget> Budgets { get; set; }
        public DbSet<PlayerProfile> PlayerProfiles { get; set; }
        public DbSet<GameLedgerEntry> GameLedgerEntries { get; set; }
        public DbSet<Habit> Habits { get; set; }
        public DbSet<HabitCheckIn> HabitCheckIns { get; set; }
        public DbSet<Reward> Rewards { get; set; }
        public DbSet<RewardRedemption> RewardRedemptions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");

            modelBuilder.ApplyConfigurationsFromAssembly(typeof(LifeManagerDbContext).Assembly);
        }
    }
}