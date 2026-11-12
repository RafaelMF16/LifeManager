using LifeManager.Domain.Push;
using LifeManager.Domain.Push.Errors;

namespace LifeManager.Domain.Test.Push
{
    public class PushSubscriptionTests
    {
        private static readonly DateTimeOffset Now = new(2026, 11, 11, 15, 0, 0, TimeSpan.Zero);

        // A P-256 public point (65 bytes) and a 16-byte secret, base64url without padding.
        private static readonly string P256dh = Base64Url(Enumerable.Repeat((byte)4, 65).ToArray());
        private static readonly string Auth = Base64Url(Enumerable.Repeat((byte)7, 16).ToArray());

        [Theory]
        [InlineData("https://fcm.googleapis.com/fcm/send/abc")]
        [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc")]
        [InlineData("https://web.push.apple.com/QGuQyavXutnMH")]
        [InlineData("https://api.push.apple.com/3/device/abc")]
        [InlineData("https://wns2-par02p.notify.windows.com/w/?token=abc")]
        public void Create_ShouldAcceptTheKnownPushServices(string endpoint)
        {
            var result = PushSubscription.Create(1, endpoint, P256dh, Auth, "Chrome · Android", Now);

            Assert.True(result.IsSuccess);
            Assert.Equal(endpoint, result.Value!.Endpoint);
            Assert.Equal(1, result.Value.UserId.Value);
            Assert.Equal(Now, result.Value.CreatedAt);
            Assert.Null(result.Value.LastSuccessAt);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not a url")]
        [InlineData("http://fcm.googleapis.com/fcm/send/abc")]
        [InlineData("https://evil.example.com/fcm/send/abc")]
        [InlineData("https://fcm.googleapis.com.evil.com/abc")]
        [InlineData("https://localhost/abc")]
        [InlineData("https://169.254.169.254/latest/meta-data")]
        [InlineData("https://notify.windows.com.attacker.net/abc")]
        [InlineData("https://user:pass@fcm.googleapis.com/abc")]
        [InlineData("https://fcm.googleapis.com:8443/abc")]
        public void Create_ShouldReturnInvalidEndpoint_WhenItIsNotAKnownPushService(string? endpoint)
        {
            var result = PushSubscription.Create(1, endpoint, P256dh, Auth, null, Now);

            Assert.Equal(PushErrors.InvalidEndpoint, result.Error);
        }

        [Fact]
        public void Create_ShouldReturnInvalidEndpoint_WhenItIsTooLong()
        {
            var endpoint = "https://fcm.googleapis.com/" + new string('a', PushSubscription.EndpointMaxLength);

            var result = PushSubscription.Create(1, endpoint, P256dh, Auth, null, Now);

            Assert.Equal(PushErrors.InvalidEndpoint, result.Error);
        }

        [Theory]
        [InlineData(null, "auth")]
        [InlineData("not+base64url!", "auth")]
        [InlineData("short", "auth")]
        [InlineData("p256dh", null)]
        [InlineData("p256dh", "AAAA")]
        public void Create_ShouldReturnInvalidKeys_WhenAKeyIsMissingOrHasTheWrongSize(string? p256dh, string? auth)
        {
            var result = PushSubscription.Create(1, "https://fcm.googleapis.com/fcm/send/abc",
                p256dh == "p256dh" ? P256dh : p256dh, auth == "auth" ? Auth : auth, null, Now);

            Assert.Equal(PushErrors.InvalidKeys, result.Error);
        }

        [Fact]
        public void Create_ShouldAcceptPaddedBase64UrlKeys()
        {
            var result = PushSubscription.Create(1, "https://fcm.googleapis.com/fcm/send/abc", P256dh + "=", Auth + "==", null, Now);

            Assert.True(result.IsSuccess);
        }

        [Fact]
        public void Create_ShouldTrimTheLabel_AndDropAnEmptyOne()
        {
            Assert.Equal("Firefox · Windows", PushSubscription.Create(1, "https://fcm.googleapis.com/a", P256dh, Auth, "  Firefox · Windows ", Now).Value!.Label);
            Assert.Null(PushSubscription.Create(1, "https://fcm.googleapis.com/a", P256dh, Auth, "   ", Now).Value!.Label);
        }

        [Fact]
        public void Create_ShouldReturnLabelTooLong_WhenTheLabelHasMoreThan60Characters()
        {
            var result = PushSubscription.Create(1, "https://fcm.googleapis.com/a", P256dh, Auth, new string('a', 61), Now);

            Assert.Equal(PushErrors.LabelTooLong, result.Error);
        }

        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
