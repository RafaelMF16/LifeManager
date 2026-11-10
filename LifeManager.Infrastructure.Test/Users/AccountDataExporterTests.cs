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
            "categories", "months", "transactions", "recurringTransactions", "budgetPlans", "accountHolders", "statementImports",
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
            // The hand-typed one, the recurrence's and the import's.
            Assert.Equal(3, root.GetProperty("transactions").GetArrayLength());
            Assert.Equal(["Academia"], root.GetProperty("recurringTransactions").EnumerateArray().Select(item => item.GetProperty("description").GetString()));
            Assert.Equal(1, root.GetProperty("budgetPlans")[0].GetProperty("slices").GetArrayLength());
            Assert.Equal(["Ana da Silva"], Names(root, "accountHolders"));
            Assert.Equal(2, root.GetProperty("statementImports").GetArrayLength());
            Assert.Equal(["Ler"], Names(root, "habits"));
            Assert.Equal(1, root.GetProperty("checkIns").GetArrayLength());
            Assert.Equal(10, root.GetProperty("player").GetProperty("coins").GetInt32());
            Assert.NotEqual(0, root.GetProperty("ledger").GetArrayLength());
            Assert.Equal(["Cinema"], Names(root, "rewards"));

            var group = Assert.Single(root.GetProperty("rankingGroups").EnumerateArray());
            Assert.Equal("Corrida", group.GetProperty("name").GetString());
            Assert.False(group.GetProperty("isOwner").GetBoolean());

            // Nothing of Bia's (her name, her account holder) and no secret or bank id.
            Assert.DoesNotContain("Bia", json);
            foreach (var forbidden in new[] { "passwordHash", "PasswordHash", "tokenHash", "hash-Ana", "externalId", "ExternalId", "$2a$" })
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
            using var output = new MemoryStream();
            await new AccountDataExporter(context).WriteAsync(user.Id!, output, TestData.Now, CancellationToken.None);
            return System.Text.Encoding.UTF8.GetString(output.ToArray());
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
