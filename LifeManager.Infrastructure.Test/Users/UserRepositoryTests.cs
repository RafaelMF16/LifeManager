using LifeManager.Domain.Users.ValueObjects;
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
    }
}
