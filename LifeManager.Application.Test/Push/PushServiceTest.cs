using LifeManager.Application.Push.Services;
using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Push.Mocks;
using LifeManager.Domain.Push.Errors;
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
        }

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
    }
}
