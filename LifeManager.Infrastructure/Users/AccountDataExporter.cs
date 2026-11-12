using System.Buffers;
using System.Text.Json;
using System.Text.Json.Serialization;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Users
{
    /// <summary>
    /// The data export, written with a <see cref="Utf8JsonWriter"/> and streamed to the response: one array per table, read
    /// with <c>AsNoTracking</c> + <c>AsAsyncEnumerable</c> and sent as it grows. The writer fills a buffer in memory, never
    /// the response itself: <c>JsonSerializer.Serialize(writer, …)</c> flushes the writer synchronously, and Kestrel's
    /// body refuses synchronous writes. Only <see cref="SendAsync"/> touches the response, asynchronously. Each row is mapped field by field to
    /// what may leave: every section lists its columns here, so a new column is never exported by accident. Left out on
    /// purpose: the push devices (an endpoint and keys work like a credential) and the reminders' logs.
    /// </summary>
    public class AccountDataExporter(LifeManagerDbContext dbContext) : IAccountDataExporter
    {
        // Sent to the response once the buffer holds this much, so a long section never piles up in memory.
        private const int FlushThresholdBytes = 32 * 1024;

        private static readonly JsonSerializerOptions SerializerOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter() },
        };

        private readonly LifeManagerDbContext _dbContext = dbContext;
        // Set by WriteAsync for its run (the exporter is scoped: one export per request).
        private readonly ArrayBufferWriter<byte> _buffer = new();
        private Stream _output = Stream.Null;

        public async Task WriteAsync(UserId userId, Stream output, DateTimeOffset exportedAt, CancellationToken cancellationToken)
        {
            _output = output;
            _buffer.ResetWrittenCount();
            await using var writer = new Utf8JsonWriter(_buffer, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();
            writer.WriteNumber("version", IAccountDataExporter.FormatVersion);
            writer.WriteString("exportedAt", exportedAt);

            await WriteAccountAsync(writer, userId, cancellationToken);
            await WriteFinanceAsync(writer, userId, cancellationToken);
            await WriteHabitsAsync(writer, userId, cancellationToken);
            await WriteGameAsync(writer, userId, cancellationToken);
            await WriteRankingAsync(writer, userId, cancellationToken);

            writer.WriteEndObject();
            await SendAsync(writer, cancellationToken);
        }

        /// <summary>Moves what the writer holds to the response, asynchronously, and empties the buffer.</summary>
        private async Task SendAsync(Utf8JsonWriter writer, CancellationToken cancellationToken)
        {
            writer.Flush();
            await _output.WriteAsync(_buffer.WrittenMemory, cancellationToken);
            _buffer.ResetWrittenCount();
        }

        private async Task WriteAccountAsync(Utf8JsonWriter writer, UserId userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users.AsNoTracking().SingleOrDefaultAsync(stored => stored.Id == userId, cancellationToken);
            writer.WritePropertyName("user");
            JsonSerializer.Serialize(writer, user is null ? null : new
            {
                Name = user.Name.Value,
                Email = user.Email.Value,
                user.CreationDate,
                user.DeletionScheduledFor,
            }, SerializerOptions);

            var preferences = await _dbContext.UserPreferences.AsNoTracking().SingleOrDefaultAsync(stored => stored.UserId == userId, cancellationToken);
            writer.WritePropertyName("preferences");
            JsonSerializer.Serialize(writer, preferences is null ? null : new { preferences.Theme, preferences.Language }, SerializerOptions);
        }

        private async Task WriteFinanceAsync(Utf8JsonWriter writer, UserId userId, CancellationToken cancellationToken)
        {
            await WriteArrayAsync(writer, "categories",
                _dbContext.Categories.AsNoTracking().Where(category => category.UserId == userId).OrderBy(category => category.Id),
                category => new { Id = category.Id!.Value, Name = category.Name.Value },
                cancellationToken);

            await WriteArrayAsync(writer, "months",
                _dbContext.MonthlySummaries.AsNoTracking().Where(month => month.UserId == userId).OrderBy(month => month.Id),
                month => new
                {
                    Id = month.Id!.Value,
                    Year = month.Year.Value,
                    Month = month.Month.Value,
                    TotalIncome = month.TotalIncome.Value,
                    TotalExpense = month.TotalExpense.Value,
                    TotalInvestment = month.TotalInvestment.Value,
                    Balance = month.BalanceAmount,
                },
                cancellationToken);

            // Transactions have no user of their own: they're the user's through their month. ExternalId (the bank's line) stays out.
            await WriteArrayAsync(writer, "transactions",
                _dbContext.Transactions.AsNoTracking()
                    .Where(transaction => _dbContext.MonthlySummaries.Any(month => month.Id == transaction.MonthlySummaryId && month.UserId == userId))
                    .OrderBy(transaction => transaction.TransactionDate).ThenBy(transaction => transaction.Id),
                transaction => new
                {
                    Id = transaction.Id!.Value,
                    MonthId = transaction.MonthlySummaryId.Value,
                    transaction.Type,
                    CategoryId = transaction.CategoryId.Value,
                    Amount = transaction.Amount.Value,
                    Description = transaction.Description.Value,
                    Date = transaction.TransactionDate,
                    RecurringTransactionId = transaction.RecurringTransactionId?.Value,
                    StatementImportId = transaction.StatementImportId?.Value,
                    SavingsGoalId = transaction.SavingsGoalId?.Value,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "recurringTransactions",
                _dbContext.RecurringTransactions.AsNoTracking().Where(recurrence => recurrence.UserId == userId).OrderBy(recurrence => recurrence.Id),
                recurrence => new
                {
                    Id = recurrence.Id!.Value,
                    recurrence.Type,
                    CategoryId = recurrence.CategoryId.Value,
                    Amount = recurrence.Amount.Value,
                    Description = recurrence.Description.Value,
                    DayOfMonth = recurrence.DayOfMonth.Value,
                    StartMonth = recurrence.StartMonth.ToString(),
                    EndMonth = recurrence.EndMonth?.ToString(),
                    recurrence.IsActive,
                    NextOccurrence = recurrence.NextOccurrenceDate,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "budgetPlans",
                _dbContext.BudgetPlans.AsNoTracking().Where(plan => plan.UserId == userId).OrderBy(plan => plan.MonthStart),
                plan => new
                {
                    Month = plan.Month.ToString(),
                    Total = plan.Total.Value,
                    plan.CreatedAt,
                    plan.UpdatedAt,
                    Slices = plan.Slices.Select(slice => new { slice.Type, CategoryId = slice.CategoryId?.Value, Percent = slice.Percent.Value }),
                },
                cancellationToken);

            await WriteArrayAsync(writer, "savingsGoals",
                _dbContext.SavingsGoals.AsNoTracking().Where(goal => goal.UserId == userId).OrderBy(goal => goal.Id),
                goal => new
                {
                    Id = goal.Id!.Value,
                    Name = goal.Name.Value,
                    goal.TargetAmount,
                    TargetMonth = goal.TargetMonth == null ? null : goal.TargetMonth.ToString(),
                    goal.InitialAmount,
                    goal.Icon,
                    goal.CreatedAt,
                    goal.ArchivedAt,
                    goal.CompletedAt,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "accountHolders",
                _dbContext.AccountHolders.AsNoTracking().Where(holder => holder.UserId == userId).OrderBy(holder => holder.Position),
                holder => new { Name = holder.Name.Value },
                cancellationToken);

            // The history only: a draft's rows are a review in progress, not the user's data yet.
            await WriteArrayAsync(writer, "statementImports",
                _dbContext.StatementImports.AsNoTracking().Where(import => import.UserId == userId).OrderBy(import => import.Id),
                import => new
                {
                    Id = import.Id!.Value,
                    import.FileName,
                    import.Source,
                    import.Status,
                    import.RowCount,
                    import.FirstDate,
                    import.LastDate,
                    import.CreatedAt,
                    import.CommittedAt,
                    import.ImportedCount,
                    import.CreatedMonthCount,
                },
                cancellationToken);
        }

        private async Task WriteHabitsAsync(Utf8JsonWriter writer, UserId userId, CancellationToken cancellationToken)
        {
            await WriteArrayAsync(writer, "habits",
                _dbContext.Habits.AsNoTracking().Where(habit => habit.UserId == userId).OrderBy(habit => habit.Id),
                habit => new
                {
                    Id = habit.Id!.Value,
                    Name = habit.Name.Value,
                    Description = habit.Description?.Value,
                    Trigger = habit.Trigger?.Value,
                    habit.Kind,
                    habit.Difficulty,
                    habit.FrequencyType,
                    habit.WeekDays,
                    habit.TimesPerWeek,
                    habit.TargetValue,
                    habit.TargetUnit,
                    habit.TargetStep,
                    habit.RelapseCost,
                    habit.AutoTrigger,
                    habit.ReminderHour,
                    habit.StartDate,
                    habit.CreatedAt,
                    habit.ArchivedAt,
                    habit.CurrentStreak,
                    habit.LongestStreak,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "checkIns",
                _dbContext.HabitCheckIns.AsNoTracking().Where(checkIn => checkIn.UserId == userId).OrderBy(checkIn => checkIn.Date).ThenBy(checkIn => checkIn.Id),
                checkIn => new
                {
                    HabitId = checkIn.HabitId.Value,
                    checkIn.Date,
                    checkIn.Status,
                    checkIn.CoinsAwarded,
                    checkIn.XpAwarded,
                    checkIn.HpAwarded,
                    checkIn.FreezeAwarded,
                    checkIn.CreatedAt,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "progressEntries",
                _dbContext.HabitProgressEntries.AsNoTracking().Where(entry => entry.UserId == userId).OrderBy(entry => entry.Date).ThenBy(entry => entry.Id),
                entry => new { HabitId = entry.HabitId.Value, entry.Date, entry.Value, entry.CreatedAt },
                cancellationToken);

            await WriteArrayAsync(writer, "challenges",
                _dbContext.WeeklyChallenges.AsNoTracking().Where(challenge => challenge.UserId == userId).OrderBy(challenge => challenge.WeekStart).ThenBy(challenge => challenge.Id),
                challenge => new
                {
                    HabitId = challenge.HabitId.Value,
                    challenge.WeekStart,
                    challenge.Type,
                    challenge.Target,
                    challenge.Baseline,
                    challenge.Effort,
                    challenge.RewardCoins,
                    challenge.RewardXp,
                    challenge.Status,
                    challenge.FinalProgress,
                    challenge.CreatedAt,
                    challenge.CompletedAt,
                },
                cancellationToken);
        }

        private async Task WriteGameAsync(Utf8JsonWriter writer, UserId userId, CancellationToken cancellationToken)
        {
            var player = await _dbContext.PlayerProfiles.AsNoTracking().SingleOrDefaultAsync(profile => profile.UserId == userId, cancellationToken);
            writer.WritePropertyName("player");
            JsonSerializer.Serialize(writer, player is null ? null : new
            {
                player.Level,
                player.TotalXp,
                player.Hp,
                player.MaxHp,
                player.Coins,
                player.StreakFreezes,
                player.Avatar,
                player.Title,
            }, SerializerOptions);

            await WriteArrayAsync(writer, "ledger",
                _dbContext.GameLedgerEntries.AsNoTracking().Where(entry => entry.UserId == userId).OrderBy(entry => entry.CreatedAt).ThenBy(entry => entry.Id),
                entry => new
                {
                    entry.Kind,
                    entry.OccurredOn,
                    entry.CreatedAt,
                    entry.CoinsDelta,
                    entry.XpDelta,
                    entry.HpDelta,
                    entry.Description,
                    HabitId = entry.HabitId?.Value,
                    RewardId = entry.RewardId?.Value,
                    entry.ItemKind,
                    entry.AchievementCode,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "rewards",
                _dbContext.Rewards.AsNoTracking().Where(reward => reward.UserId == userId).OrderBy(reward => reward.Id),
                reward => new
                {
                    Id = reward.Id!.Value,
                    Name = reward.Name.Value,
                    reward.Cost,
                    reward.Icon,
                    ExpenseCategoryId = reward.ExpenseCategoryId?.Value,
                    reward.ExpenseAmount,
                    reward.CreatedAt,
                    reward.ArchivedAt,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "redemptions",
                _dbContext.RewardRedemptions.AsNoTracking().Where(redemption => redemption.UserId == userId).OrderBy(redemption => redemption.RedeemedAt).ThenBy(redemption => redemption.Id),
                redemption => new
                {
                    RewardId = redemption.RewardId.Value,
                    redemption.RewardName,
                    redemption.CostPaid,
                    redemption.RedeemedOn,
                    redemption.RedeemedAt,
                    redemption.UndoneAt,
                    TransactionId = redemption.TransactionId?.Value,
                },
                cancellationToken);

            await WriteArrayAsync(writer, "gameItemPurchases",
                _dbContext.GameItemPurchases.AsNoTracking().Where(purchase => purchase.UserId == userId).OrderBy(purchase => purchase.PurchasedAt).ThenBy(purchase => purchase.Id),
                purchase => new { purchase.Kind, purchase.CostPaid, purchase.HpRestored, purchase.PurchasedOn, purchase.PurchasedAt, purchase.UndoneAt },
                cancellationToken);

            await WriteArrayAsync(writer, "achievements",
                _dbContext.AchievementUnlocks.AsNoTracking().Where(unlock => unlock.UserId == userId).OrderBy(unlock => unlock.UnlockedAt).ThenBy(unlock => unlock.Id),
                unlock => new { unlock.Code, unlock.UnlockedOn, unlock.UnlockedAt },
                cancellationToken);
        }

        /// <summary>Only the user's own membership: each group's name and when they joined. Nothing about the other members.</summary>
        private async Task WriteRankingAsync(Utf8JsonWriter writer, UserId userId, CancellationToken cancellationToken)
            => await WriteArrayAsync(writer, "rankingGroups",
                _dbContext.RankingGroupMembers.AsNoTracking()
                    .Where(member => member.UserId == userId)
                    .Join(_dbContext.RankingGroups, member => member.GroupId, group => group.Id, (member, group) => new { group.Name, member.JoinedOn, group.OwnerUserId })
                    .OrderBy(membership => membership.JoinedOn),
                membership => new { Name = membership.Name.Value, membership.JoinedOn, IsOwner = membership.OwnerUserId == userId },
                cancellationToken);

        private async Task WriteArrayAsync<TRow, TOut>(
            Utf8JsonWriter writer,
            string name,
            IQueryable<TRow> query,
            Func<TRow, TOut> map,
            CancellationToken cancellationToken)
        {
            writer.WriteStartArray(name);

            await foreach (var row in query.AsAsyncEnumerable().WithCancellation(cancellationToken))
            {
                JsonSerializer.Serialize(writer, map(row), SerializerOptions);

                if (_buffer.WrittenCount + writer.BytesPending > FlushThresholdBytes)
                    await SendAsync(writer, cancellationToken);
            }

            writer.WriteEndArray();
            await SendAsync(writer, cancellationToken);
        }
    }
}
