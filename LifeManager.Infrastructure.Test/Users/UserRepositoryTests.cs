using LifeManager.Domain.Auth;
using LifeManager.Domain.Auth.ValueObjects;
using LifeManager.Domain.Users;
using LifeManager.Domain.Users.ValueObjects;
using LifeManager.Infrastructure.Postgres;
using Microsoft.EntityFrameworkCore;
using LifeManager.Infrastructure.Test.Seed;
using LifeManager.Infrastructure.Users;

namespace LifeManager.Infrastructure.Test.Users
{
    public class UserRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        [Fact]
        public async Task UpdateNameAsync_ShouldChangeOnlyThatUsersName()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync("Ana");
            var other = await seedContext.AddUserAsync("Bia");

            await using (var context = Fixture.CreateContext())
                await new UserRepository(context).UpdateNameAsync(user.Id!, UserName.Create("Ana Maria").Unwrap(), CancellationToken.None);

            await using var readContext = Fixture.CreateContext();
            var repository = new UserRepository(readContext);
            Assert.Equal("Ana Maria", (await repository.GetByIdAsync(user.Id!, CancellationToken.None))!.Name.Value);
            Assert.Equal("Bia", (await repository.GetByIdAsync(other.Id!, CancellationToken.None))!.Name.Value);
        }

        [Fact]
        public async Task ChangePasswordAsync_ShouldStoreTheHashAndRevokeEveryOtherSessionOfTheUser()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync("Ana");
            var other = await seedContext.AddUserAsync("Bia");
            await AddSessionAsync(seedContext, user, "current");
            await AddSessionAsync(seedContext, user, "other");
            await AddSessionAsync(seedContext, other, "bia");
            var newHash = PasswordHash.Create("$2a$12$newnewnewnewnewnewnewnewnewnewnewnewnewnewnewnewnewne").Unwrap();

            await using (var context = Fixture.CreateContext())
                await new UserRepository(context).ChangePasswordAsync(user.Id!, newHash, RefreshTokenHash.Create("current").Unwrap(), CancellationToken.None);

            await using var readContext = Fixture.CreateContext();
            Assert.Equal(newHash, (await new UserRepository(readContext).GetByIdAsync(user.Id!, CancellationToken.None))!.PasswordHash);
            Assert.Equal(["bia", "current"], await ActiveSessionsAsync(readContext));
        }

        [Fact]
        public async Task ChangePasswordAsync_ShouldRevokeEverySessionOfTheUser_WhenThereIsNoSessionToKeep()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync("Ana");
            await AddSessionAsync(seedContext, user, "current");
            var newHash = PasswordHash.Create("$2a$12$newnewnewnewnewnewnewnewnewnewnewnewnewnewnewnewnewne").Unwrap();

            await using (var context = Fixture.CreateContext())
                await new UserRepository(context).ChangePasswordAsync(user.Id!, newHash, null, CancellationToken.None);

            await using var readContext = Fixture.CreateContext();
            Assert.Empty(await ActiveSessionsAsync(readContext));
        }

        [Fact]
        public async Task ScheduleDeletionAsync_ShouldMarkOnceAndEndEverySessionOfTheUser()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync("Ana");
            var other = await seedContext.AddUserAsync("Bia");
            await AddSessionAsync(seedContext, user, "ana");
            await AddSessionAsync(seedContext, other, "bia");
            var first = TestData.Now.AddDays(AccountDeletion.GraceDays);

            await using (var context = Fixture.CreateContext())
                Assert.True(await new UserRepository(context).ScheduleDeletionAsync(user.Id!, TestData.Now, first, CancellationToken.None));

            // Asking again keeps the first date.
            await using (var context = Fixture.CreateContext())
                Assert.False(await new UserRepository(context).ScheduleDeletionAsync(user.Id!, TestData.Now.AddDays(1), first.AddDays(1), CancellationToken.None));

            await using var readContext = Fixture.CreateContext();
            var stored = (await new UserRepository(readContext).GetByIdAsync(user.Id!, CancellationToken.None))!;
            Assert.Equal(TestData.Now, stored.DeletionRequestedAt);
            Assert.Equal(first, stored.DeletionScheduledFor);
            Assert.False((await new UserRepository(readContext).GetByIdAsync(other.Id!, CancellationToken.None))!.IsDeletionPending);
            Assert.Equal(["bia"], await ActiveSessionsAsync(readContext));
        }

        [Fact]
        public async Task CancelDeletionAsync_ShouldClearAPendingDeletionOnly()
        {
            await using var seedContext = Fixture.CreateContext();
            var user = await seedContext.AddUserAsync("Ana");
            var other = await seedContext.AddUserAsync("Bia");

            await using (var context = Fixture.CreateContext())
            {
                var repository = new UserRepository(context);
                await repository.ScheduleDeletionAsync(user.Id!, TestData.Now, TestData.Now.AddDays(AccountDeletion.GraceDays), CancellationToken.None);

                Assert.True(await repository.CancelDeletionAsync(user.Id!, CancellationToken.None));
                Assert.False(await repository.CancelDeletionAsync(user.Id!, CancellationToken.None));
                Assert.False(await repository.CancelDeletionAsync(other.Id!, CancellationToken.None));
            }

            await using var readContext = Fixture.CreateContext();
            var stored = (await new UserRepository(readContext).GetByIdAsync(user.Id!, CancellationToken.None))!;
            Assert.Null(stored.DeletionRequestedAt);
            Assert.Null(stored.DeletionScheduledFor);
        }

        private static async Task AddSessionAsync(LifeManagerDbContext context, User user, string hash)
        {
            context.Add(RefreshToken.Create(user.Id!.Value, hash, TestData.Now, TestData.Now.AddDays(7), TestData.Now.AddDays(30), false).Unwrap());
            await context.SaveChangesAsync();
        }

        private static async Task<List<string>> ActiveSessionsAsync(LifeManagerDbContext context)
            => (await context.RefreshTokens.AsNoTracking().Where(token => !token.IsRevoked).ToListAsync())
                .Select(token => token.TokenHash.Value)
                .Order(StringComparer.Ordinal)
                .ToList();
    }
}
