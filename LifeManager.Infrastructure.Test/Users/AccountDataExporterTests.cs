using System.Text.Json;
using LifeManager.Domain.RankingGroups;
using LifeManager.Domain.RankingGroups.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.Interfaces;
using LifeManager.Infrastructure.Postgres;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Users;

namespace LifeManager.Infrastructure.Test.Users
{
    /// <summary>The data export read back as JSON: every section, the user's data, and nothing secret or of anyone else.</summary>
    public class AccountDataExporterTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private static readonly DateOnly Today = new(2026, 11, 10);

        private static readonly string[] Sections =
        [
            "version", "exportedAt", "user", "preferences",
            "categories", "months", "transactions", "recurringTransactions", "budgetPlans", "savingsGoals", "accountHolders", "statementImports",
            "habits", "checkIns", "progressEntries", "challenges",
            "player", "ledger", "rewards", "redemptions", "gameItemPurchases", "achievements",
            "rankingGroups",
        ];

        [Fact]
        public async Task WriteAsync_ShouldWriteEverySectionWithTheUsersDataOnly()
        {
            User ana;
            await using (var seedContext = Fixture.CreateContext())
            {
                ana = await seedContext.AddUserWithEverythingAsync("Ana");
                var bia = await seedContext.AddUserWithEverythingAsync("Bia");
                await AddGroupAsync(seedContext, "Corrida", bia, ana);
            }

            var json = await ExportAsync(ana);
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            Assert.Equal(Sections, root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(IAccountDataExporter.FormatVersion, root.GetProperty("version").GetInt32());
            Assert.Equal("Ana", root.GetProperty("user").GetProperty("name").GetString());
            Assert.Equal("Dark", root.GetProperty("preferences").GetProperty("theme").GetString());
            Assert.Equal(["Mercado"], Names(root, "categories"));
            Assert.Equal(1, root.GetProperty("months").GetArrayLength());
            // The hand-typed one, the savings goal's investment, the recurrence's and the import's.
            Assert.Equal(4, root.GetProperty("transactions").GetArrayLength());
            Assert.Equal(["Viagem"], Names(root, "savingsGoals"));
            Assert.Single(root.GetProperty("transactions").EnumerateArray(), item => item.GetProperty("savingsGoalId").ValueKind == JsonValueKind.Number);
            Assert.Equal(["Academia"], root.GetProperty("recurringTransactions").EnumerateArray().Select(item => item.GetProperty("description").GetString()));
            Assert.Equal(1, root.GetProperty("budgetPlans")[0].GetProperty("slices").GetArrayLength());
            Assert.Equal(["Ana da Silva"], Names(root, "accountHolders"));
            Assert.Equal(2, root.GetProperty("statementImports").GetArrayLength());
            Assert.Equal(["Ler"], Names(root, "habits"));
            Assert.Equal(20, root.GetProperty("habits")[0].GetProperty("reminderHour").GetInt32());
            Assert.Equal(1, root.GetProperty("checkIns").GetArrayLength());
            Assert.Equal(10, root.GetProperty("player").GetProperty("coins").GetInt32());
            Assert.NotEqual(0, root.GetProperty("ledger").GetArrayLength());
            Assert.Equal(["Cinema"], Names(root, "rewards"));

            var group = Assert.Single(root.GetProperty("rankingGroups").EnumerateArray());
            Assert.Equal("Corrida", group.GetProperty("name").GetString());
            Assert.False(group.GetProperty("isOwner").GetBoolean());

            // Nothing of Bia's (her name, her account holder) and no secret, bank id or device of push notifications.
            Assert.DoesNotContain("Bia", json);
            foreach (var forbidden in new[] { "passwordHash", "PasswordHash", "tokenHash", "hash-Ana", "externalId", "ExternalId", "$2a$", "fcm.googleapis.com", "p256dh", TestData.PushP256dh })
                Assert.DoesNotContain(forbidden, json);
        }

        [Fact]
        public async Task WriteAsync_ShouldWriteEmptySections_WhenTheUserHasNoData()
        {
            User ana;
            await using (var seedContext = Fixture.CreateContext())
                ana = await seedContext.AddUserAsync("Ana");

            using var document = JsonDocument.Parse(await ExportAsync(ana));
            var root = document.RootElement;

            Assert.Equal(Sections, root.EnumerateObject().Select(property => property.Name));
            Assert.Equal(JsonValueKind.Null, root.GetProperty("player").ValueKind);
            Assert.Equal(0, root.GetProperty("transactions").GetArrayLength());
        }

        private async Task<string> ExportAsync(User user)
        {
            await using var context = Fixture.CreateContext();
            // Like Kestrel's response body: a synchronous write throws.
            using var output = new AsyncOnlyStream();
            await new AccountDataExporter(context).WriteAsync(user.Id!, output, TestData.Now, CancellationToken.None);
            return System.Text.Encoding.UTF8.GetString(output.ToArray());
        }

        /// <summary>A stream that, like the response body (AllowSynchronousIO off), refuses synchronous writes.</summary>
        private sealed class AsyncOnlyStream : MemoryStream
        {
            private bool _writingAsync;

            public override void Write(byte[] buffer, int offset, int count)
            {
                if (!_writingAsync) throw SyncWrite();
                base.Write(buffer, offset, count);
            }

            public override void Write(ReadOnlySpan<byte> buffer)
            {
                if (!_writingAsync) throw SyncWrite();
                base.Write(buffer);
            }

            public override void WriteByte(byte value) => throw SyncWrite();

            public override void Flush() => throw SyncWrite();

            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
                => WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

            public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
            {
                _writingAsync = true;
                try
                {
                    Write(buffer.Span);
                }
                finally
                {
                    _writingAsync = false;
                }

                return ValueTask.CompletedTask;
            }

            public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

            private static InvalidOperationException SyncWrite() => new("Synchronous operations are disallowed.");
        }

        private static IEnumerable<string?> Names(JsonElement root, string section)
            => root.GetProperty(section).EnumerateArray().Select(item => item.GetProperty("name").GetString());

        private static async Task AddGroupAsync(LifeManagerDbContext context, string name, User owner, User member)
        {
            var group = RankingGroup.Create(name, owner.Id!, InviteCode.Generate(), TestData.Now, Today).Unwrap();
            context.Add(group);
            await context.SaveChangesAsync();

            context.Add(RankingGroupMember.Create(group.Id!, owner.Id!, TestData.Now, Today));
            context.Add(RankingGroupMember.Create(group.Id!, member.Id!, TestData.Now, Today));
            await context.SaveChangesAsync();
        }
    }
}
