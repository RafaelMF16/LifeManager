using LifeManager.Application.Push.DTOs;
using LifeManager.Application.Push.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Configurations.SingletonLists;
using LifeManager.Application.Test.Push.Mocks;
using LifeManager.Domain.Push.Errors;
using LifeManager.Domain.Push;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Push
{
    [Collection("ApplicationServices")]
    public class PushServiceTest : BaseTest
    {
        private readonly PushService _pushService;

        public PushServiceTest()
        {
            _pushService = ServiceProvider.GetRequiredService<PushService>();

            PushSenderMock.Reset();
            PushSubscriptionSingleton.Instance.Clear();
        }

        private static readonly UserId FirstUserId = new(1);
        private static readonly UserId SecondUserId = new(2);
        private const string Endpoint = "https://fcm.googleapis.com/fcm/send/device-1";
        private const string P256dh = "BC9qRGdfGQdHJJN39NH4P0CdqV5A1RUiRyL7Rt1aKrbBIxmHB-9N1H9alTGDlSixhx3IO9M4g_si3qv0J6Xk1GY";
        private static readonly string Auth = Base64Url(Enumerable.Repeat((byte)7, 16).ToArray());

        [Fact]
        public void GetPublicKey_ShouldReturnTheVapidPublicKey_WhenPushIsConfigured()
        {
            var result = _pushService.GetPublicKey();

            Assert.True(result.IsSuccess);
            Assert.Equal(PushSenderMock.TestPublicKey, result.Value.PublicKey);
        }

        [Fact]
        public void GetPublicKey_ShouldReturnNotConfigured_WhenPushHasNoKeys()
        {
            PushSenderMock.Enabled = false;

            var result = _pushService.GetPublicKey();

            Assert.False(result.IsSuccess);
            Assert.Equal(PushErrors.NotConfigured, result.Error);
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldStoreTheDevice()
        {
            var result = await _pushService.SaveSubscriptionAsync(NewDto(Endpoint, "Chrome · Android"), FirstUserId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            var stored = Assert.Single(PushSubscriptionSingleton.Instance);
            Assert.Equal(FirstUserId, stored.UserId);
            Assert.Equal(Endpoint, stored.Endpoint);
            Assert.Equal(P256dh, stored.P256dh);
            Assert.Equal(Auth, stored.Auth);
            Assert.Equal("Chrome · Android", stored.Label);
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldUpdateTheSameDevice_InsteadOfAddingAnother()
        {
            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint, "Old"), FirstUserId, CancellationToken.None);

            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint, "New"), FirstUserId, CancellationToken.None);

            Assert.Equal("New", Assert.Single(PushSubscriptionSingleton.Instance).Label);
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldMoveTheDeviceToTheNewAccount_WhenAnotherUserSubscribesIt()
        {
            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint), FirstUserId, CancellationToken.None);

            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint), SecondUserId, CancellationToken.None);

            Assert.Equal(SecondUserId, Assert.Single(PushSubscriptionSingleton.Instance).UserId);
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldDropTheOldestDevice_WhenTheUserPassesTheLimit()
        {
            for (var device = 1; device <= PushSubscription.MaxPerUser; device++)
                await _pushService.SaveSubscriptionAsync(NewDto($"https://fcm.googleapis.com/fcm/send/{device}"), FirstUserId, CancellationToken.None);

            await _pushService.SaveSubscriptionAsync(NewDto("https://fcm.googleapis.com/fcm/send/new"), FirstUserId, CancellationToken.None);

            Assert.Equal(PushSubscription.MaxPerUser, PushSubscriptionSingleton.Instance.Count);
            Assert.DoesNotContain(PushSubscriptionSingleton.Instance, subscription => subscription.Endpoint.EndsWith("/1"));
            Assert.Contains(PushSubscriptionSingleton.Instance, subscription => subscription.Endpoint.EndsWith("/new"));
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldReturnInvalidEndpoint_WhenItIsNotAKnownPushService()
        {
            var result = await _pushService.SaveSubscriptionAsync(NewDto("https://attacker.example.com/hook"), FirstUserId, CancellationToken.None);

            Assert.Equal(PushErrors.InvalidEndpoint, result.Error);
            Assert.Empty(PushSubscriptionSingleton.Instance);
        }

        [Fact]
        public async Task SaveSubscriptionAsync_ShouldReturnInvalidKeys_WhenTheKeysAreMissing()
        {
            var result = await _pushService.SaveSubscriptionAsync(new PushSubscriptionDto(Endpoint, null, null), FirstUserId, CancellationToken.None);

            Assert.Equal(PushErrors.InvalidKeys, result.Error);
        }

        [Fact]
        public async Task DeleteSubscriptionAsync_ShouldRemoveTheUsersDevice()
        {
            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint), FirstUserId, CancellationToken.None);

            await _pushService.DeleteSubscriptionAsync(new PushSubscriptionDeleteDto(Endpoint), FirstUserId, CancellationToken.None);

            Assert.Empty(PushSubscriptionSingleton.Instance);
        }

        [Fact]
        public async Task DeleteSubscriptionAsync_ShouldKeepAnotherUsersDevice()
        {
            await _pushService.SaveSubscriptionAsync(NewDto(Endpoint), SecondUserId, CancellationToken.None);

            await _pushService.DeleteSubscriptionAsync(new PushSubscriptionDeleteDto(Endpoint), FirstUserId, CancellationToken.None);

            Assert.Single(PushSubscriptionSingleton.Instance);
        }

        [Fact]
        public async Task DeleteSubscriptionAsync_ShouldDoNothing_WhenTheDeviceIsUnknownOrEmpty()
        {
            await _pushService.DeleteSubscriptionAsync(new PushSubscriptionDeleteDto("https://fcm.googleapis.com/unknown"), FirstUserId, CancellationToken.None);
            await _pushService.DeleteSubscriptionAsync(new PushSubscriptionDeleteDto(null), FirstUserId, CancellationToken.None);

            Assert.Empty(PushSubscriptionSingleton.Instance);
        }

        private static PushSubscriptionDto NewDto(string endpoint, string? label = null)
            => new(endpoint, new PushSubscriptionKeysDto(P256dh, Auth), label);

        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
