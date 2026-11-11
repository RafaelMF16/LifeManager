using LifeManager.Application.Test.Achievements.Mocks;
using LifeManager.Domain.Achievements.Interfaces;
using LifeManager.Application.Test.AccountHolders.Mocks;
using LifeManager.Domain.AccountHolders.Interfaces;
using LifeManager.Application.Auth.Emails;
using LifeManager.Application.Auth.Services;
using LifeManager.Application.DI;
using LifeManager.Application.Test.Auth.Mocks;
using LifeManager.Application.Test.Categories.Mocks;
using LifeManager.Application.Test.MonthlySummaries.Mocks;
using LifeManager.Application.Test.Transactions.Mocks;
using LifeManager.Application.Test.FinanceDashboard.Mocks;
using LifeManager.Application.Test.RecurringTransactions.Mocks;
using LifeManager.Application.Test.BudgetPlans.Mocks;
using LifeManager.Application.Test.Habits.Mocks;
using LifeManager.Application.Test.Rewards.Mocks;
using LifeManager.Application.Test.SavingsGoals.Mocks;
using LifeManager.Application.Test.GameItems.Mocks;
using LifeManager.Domain.GameItems.Interfaces;
using LifeManager.Application.Test.Ranking.Mocks;
using LifeManager.Domain.Ranking.Interfaces;
using LifeManager.Application.Test.RankingGroups.Mocks;
using LifeManager.Domain.RankingGroups.Interfaces;
using LifeManager.Application.Test.StatementImports.Mocks;
using LifeManager.Domain.StatementImports.Interfaces;
using LifeManager.Domain.Categorization.Interfaces;
using LifeManager.Domain.Rewards.Interfaces;
using LifeManager.Domain.SavingsGoals.Interfaces;
using LifeManager.Domain.BudgetPlans.Interfaces;
using LifeManager.Domain.Habits.Interfaces;
using LifeManager.Domain.RecurringTransactions.Interfaces;
using LifeManager.Domain.FinanceDashboard.Interfaces;
using LifeManager.Domain.Transactions.Interfaces;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Test.UsersPreferences.Mocks;
using LifeManager.Domain.Auth.Interfaces;
using LifeManager.Domain.Categories.Interfaces;
using LifeManager.Domain.MonthlySummaries.Interfaces;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.UsersPreferences.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace LifeManager.Application.Test.Configurations
{
    public static class InjectionModule
    {
        public const string TestFrontendBaseUrl = "https://frontend.test";

        // Low on purpose: BCrypt doubles its cost per step, and the production 12 would slow every hashing test down.
        public const int TestPasswordWorkFactor = 5;

        public static IServiceCollection AddServicesInScope(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(configuration);

            services.AddSingleton(new PasswordHashingOptions(TestPasswordWorkFactor));
            services.AddApplicationServices();
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

            services.AddScoped<IUserRepository, UserRepositoryMock>();
            services.AddScoped<IAccountDeletionRepository, AccountDeletionRepositoryMock>();
            services.AddScoped<IAccountDataExporter, AccountDataExporterMock>();
            services.AddScoped<IRefreshTokenRepository, RefreshTokenRepositoryMock>();
            services.AddScoped<IUserPreferencesRepository, UserPreferencesRepositoryMock>();
            services.AddScoped<ICategoryRepository, CategoryRepositoryMock>();
            services.AddScoped<IMonthlySummaryRepository, MonthlySummaryRepositoryMock>();
            services.AddScoped<ITransactionRepository, TransactionRepositoryMock>();
            services.AddScoped<IFinanceDashboardRepository, FinanceDashboardRepositoryMock>();
            services.AddScoped<IRecurringTransactionRepository, RecurringTransactionRepositoryMock>();
            services.AddScoped<IBudgetPlanRepository, BudgetPlanRepositoryMock>();
            services.AddScoped<IPlayerProfileRepository, PlayerProfileRepositoryMock>();
            services.AddScoped<IHabitRepository, HabitRepositoryMock>();
            services.AddScoped<IHabitCheckInRepository, HabitCheckInRepositoryMock>();
            services.AddScoped<IHabitProgressRepository, HabitProgressRepositoryMock>();
            services.AddScoped<IHabitEvaluationRepository, HabitEvaluationRepositoryMock>();
            services.AddScoped<IGameLedgerRepository, GameLedgerRepositoryMock>();
            services.AddScoped<IRewardRepository, RewardRepositoryMock>();
            services.AddScoped<IRewardRedemptionRepository, RewardRedemptionRepositoryMock>();
            services.AddScoped<ISavingsGoalRepository, SavingsGoalRepositoryMock>();
            services.AddScoped<IGameItemPurchaseRepository, GameItemPurchaseRepositoryMock>();
            services.AddScoped<IAchievementRepository, AchievementRepositoryMock>();
            services.AddScoped<IWeeklyScoreRepository, WeeklyScoreRepositoryMock>();
            services.AddScoped<IRankingGroupRepository, RankingGroupRepositoryMock>();
            services.AddScoped<IRankingWeekResultRepository, RankingWeekResultRepositoryMock>();
            services.AddScoped<IRankingReactionRepository, RankingReactionRepositoryMock>();
            services.AddScoped<IWeeklyChallengeRepository, WeeklyChallengeRepositoryMock>();
            services.AddScoped<IPlayerStatsRepository, PlayerStatsRepositoryMock>();
            services.AddScoped<IStatementImportRepository, StatementImportRepositoryMock>();
            services.AddScoped<ICategorizationRuleRepository, CategorizationRuleRepositoryMock>();
            services.AddScoped<IAccountHolderRepository, AccountHolderRepositoryMock>();

            services.AddScoped<IEmailSender, EmailSenderMock>();
            services.AddScoped<IEmailCooldownRepository, EmailCooldownRepositoryMock>();
            services.AddScoped<ILoginAttemptRepository, LoginAttemptRepositoryMock>();
            services.AddScoped<IPendingRegistrationRepository, PendingRegistrationRepositoryMock>();
            services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepositoryMock>();
            services.AddSingleton(new FrontendLinks(TestFrontendBaseUrl));

            services.AddSingleton<TimeProvider, FakeTimeProvider>();

            return services;
        }
    }
}