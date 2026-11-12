using LifeManager.Domain.Push;
using LifeManager.Infrastructure.Push;
using LifeManager.Infrastructure.Test.Seed;
using Microsoft.EntityFrameworkCore;

namespace LifeManager.Infrastructure.Test.Push
{
    public class PushSubscriptionRepositoryTests(PostgresFixture fixture) : PostgresTest(fixture)
    {
        private const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-1";

        [Fact]
        public async Task SaveAsync_ShouldAddTheDevice_AndRefreshItOnTheNextSave()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();

            await new PushSubscriptionRepository(Fixture.CreateContext()).SaveAsync(TestData.NewPushSubscription(user, Endpoint, label: "Old"), default);
            await new PushSubscriptionRepository(Fixture.CreateContext()).SaveAsync(TestData.NewPushSubscription(user, Endpoint, TestData.Now.AddDays(1), "New"), default);

            var stored = Assert.Single(await Fixture.CreateContext().PushSubscriptions.AsNoTracking().ToListAsync());
            Assert.Equal(user.Id, stored.UserId);
            Assert.Equal("New", stored.Label);
            Assert.Equal(TestData.Now, stored.CreatedAt);
        }

        [Fact]
        public async Task SaveAsync_ShouldMoveTheDeviceToTheOtherAccount_AndForgetItsLastSuccess()
        {
            await using var context = Fixture.CreateContext();
            var first = await context.AddUserAsync("Ana");
            var second = await context.AddUserAsync("Bia");
            await new PushSubscriptionRepository(Fixture.CreateContext()).SaveAsync(TestData.NewPushSubscription(first, Endpoint), default);
            await context.Database.ExecuteSqlAsync($"""UPDATE "PushSubscriptions" SET "LastSuccessAt" = {TestData.Now}""");

            await new PushSubscriptionRepository(Fixture.CreateContext()).SaveAsync(TestData.NewPushSubscription(second, Endpoint, TestData.Now.AddDays(1)), default);

            var stored = Assert.Single(await Fixture.CreateContext().PushSubscriptions.AsNoTracking().ToListAsync());
            Assert.Equal(second.Id, stored.UserId);
            Assert.Null(stored.LastSuccessAt);
            Assert.Equal(TestData.Now.AddDays(1), stored.CreatedAt);
        }

        [Fact]
        public async Task SaveAsync_ShouldKeepOnlyTheMostRecentlyUsedDevices_AndAlwaysTheNewOne()
        {
            await using var context = Fixture.CreateContext();
            var user = await context.AddUserAsync();
            var other = await context.AddUserAsync("Bia");
            for (var device = 1; device <= PushSubscription.MaxPerUser; device++)
                await new PushSubscriptionRepository(Fixture.CreateContext())
                    .SaveAsync(TestData.NewPushSubscription(user, $"https://fcm.googleapis.com/fcm/send/{device}", TestData.Now.AddMinutes(device)), default);
            await new PushSubscriptionRepository(Fixture.CreateContext())
                .SaveAsync(TestData.NewPushSubscription(other, "https://fcm.googleapis.com/fcm/send/other"), default);

            // Older than every other device by CreatedAt, but the one being saved stays.
            await new PushSubscriptionRepository(Fixture.CreateContext())
                .SaveAsync(TestData.NewPushSubscription(user, "https://fcm.googleapis.com/fcm/send/new", TestData.Now.AddDays(-1)), default);

            var endpoints = await Fixture.CreateContext().PushSubscriptions.AsNoTracking()
                .Where(subscription => subscription.UserId == user.Id!).Select(subscription => subscription.Endpoint).ToListAsync();
            Assert.Equal(PushSubscription.MaxPerUser, endpoints.Count);
            Assert.Contains("https://fcm.googleapis.com/fcm/send/new", endpoints);
            Assert.DoesNotContain("https://fcm.googleapis.com/fcm/send/1", endpoints);
            Assert.Equal(1, await Fixture.CreateContext().PushSubscriptions.CountAsync(subscription => subscription.UserId == other.Id!));
        }

        [Fact]
        public async Task DeleteAsync_ShouldDeleteOnlyTheUsersDevice()
        {
            await using var context = Fixture.CreateContext();
            var owner = await context.AddUserAsync("Ana");
            var stranger = await context.AddUserAsync("Bia");
            await new PushSubscriptionRepository(Fixture.CreateContext()).SaveAsync(TestData.NewPushSubscription(owner, Endpoint), default);

            await new PushSubscriptionRepository(Fixture.CreateContext()).DeleteAsync(stranger.Id!, Endpoint, default);
            Assert.Equal(1, await Fixture.CreateContext().PushSubscriptions.CountAsync());

            await new PushSubscriptionRepository(Fixture.CreateContext()).DeleteAsync(owner.Id!, Endpoint, default);
            Assert.Equal(0, await Fixture.CreateContext().PushSubscriptions.CountAsync());
        }
    }
}
