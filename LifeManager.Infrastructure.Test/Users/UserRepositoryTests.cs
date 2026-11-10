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
