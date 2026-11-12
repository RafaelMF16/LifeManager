using LifeManager.Domain.Push.Interfaces;
using LifeManager.Infrastructure.Push;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Security.Cryptography;

namespace LifeManager.Infrastructure.Test.Push
{
    /// <summary>
    /// The adapter over the <c>Lib.Net.Http.WebPush</c> package, against a fake push service (no network, no database): how each answer
    /// maps to <see cref="PushSendResult"/> and what the request carries.
    /// </summary>
    public class WebPushSenderTests
    {
        private const string Endpoint = "https://fcm.googleapis.com/fcm/send/abc";

        [Theory]
        [InlineData(HttpStatusCode.Created, PushSendResult.Sent)]
        [InlineData(HttpStatusCode.Gone, PushSendResult.Gone)]
        [InlineData(HttpStatusCode.NotFound, PushSendResult.Gone)]
        [InlineData(HttpStatusCode.InternalServerError, PushSendResult.Failed)]
        [InlineData(HttpStatusCode.TooManyRequests, PushSendResult.Failed)]
        public async Task SendAsync_ShouldMapThePushServiceAnswer(HttpStatusCode status, PushSendResult expected)
        {
            var handler = new FakeHandler(_ => new HttpResponseMessage(status));
            using var sender = CreateSender(handler);

            var result = await sender.SendAsync(NewTarget(), NewPayload(), CancellationToken.None);

            Assert.Equal(expected, result);
        }

        [Fact]
        public async Task SendAsync_ShouldLogOnlyTheStartOfTheAnswer_WhenThePushServiceSendsALongBody()
        {
            var body = new string('x', 5000);
            var handler = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest) { Content = new StringContent(body) });
            var logger = new ListLogger();
            using var sender = new WebPushSender(new HttpClient(handler), NewSettings(), logger);

            var result = await sender.SendAsync(NewTarget(), NewPayload(), CancellationToken.None);

            Assert.Equal(PushSendResult.Failed, result);
            var message = Assert.Single(logger.Messages);
            Assert.Contains(new string('x', WebPushSender.MaxLoggedBodyLength) + "…", message);
            Assert.DoesNotContain(new string('x', WebPushSender.MaxLoggedBodyLength + 1), message);
            Assert.DoesNotContain("/fcm/send/abc", message);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnFailed_WhenThePushServiceIsUnreachable()
        {
            using var sender = CreateSender(new FakeHandler(_ => throw new HttpRequestException("down")));

            var result = await sender.SendAsync(NewTarget(), NewPayload(), CancellationToken.None);

            Assert.Equal(PushSendResult.Failed, result);
        }

        [Fact]
        public async Task SendAsync_ShouldReturnFailed_WhenTheDeviceKeyIsNotAPointOnTheCurve()
        {
            var called = false;
            using var sender = CreateSender(new FakeHandler(_ => { called = true; return new HttpResponseMessage(HttpStatusCode.Created); }));
            var notAPoint = Base64Url([0x04, .. Enumerable.Range(1, 64).Select(value => (byte)value)]);

            var result = await sender.SendAsync(NewTarget() with { P256dh = notAPoint }, NewPayload(), CancellationToken.None);

            Assert.Equal(PushSendResult.Failed, result);
            Assert.False(called);
        }

        [Fact]
        public async Task SendAsync_ShouldPostAnEncryptedPayloadWithVapidAndAShortTtl()
        {
            HttpRequestMessage? sent = null;
            var handler = new FakeHandler(request => { sent = request; return new HttpResponseMessage(HttpStatusCode.Created); });

            using var sender = CreateSender(handler);
            await sender.SendAsync(NewTarget(), NewPayload(), CancellationToken.None);

            Assert.NotNull(sent);
            Assert.Equal(HttpMethod.Post, sent.Method);
            Assert.Equal(Endpoint, sent.RequestUri!.ToString());
            Assert.Equal("vapid", sent.Headers.Authorization?.Scheme);
            Assert.Contains("aes128gcm", sent.Content!.Headers.ContentEncoding);
            Assert.Equal(WebPushSender.TimeToLiveSeconds.ToString(), Assert.Single(sent.Headers.GetValues("TTL")));
            Assert.NotNull(sent.Content);
        }

        [Fact]
        public void PublicKey_ShouldBeTheConfiguredVapidPublicKey()
        {
            var settings = NewSettings();

            using var sender = new WebPushSender(new HttpClient(new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.Created))), settings, NullLogger<WebPushSender>.Instance);

            Assert.Equal(settings.PublicKey, sender.PublicKey);
        }

        private static WebPushSender CreateSender(HttpMessageHandler handler)
            => new(new HttpClient(handler), NewSettings(), NullLogger<WebPushSender>.Instance);

        private static VapidSettings NewSettings()
        {
            // A VAPID key pair: the P-256 public point (65 bytes) and the private scalar (32 bytes), base64url.
            using var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
            var parameters = key.ExportParameters(includePrivateParameters: true);

            return new VapidSettings(Base64Url(PublicPoint(parameters)), Base64Url(parameters.D!), "mailto:test@lifemanager.test");
        }

        /// <summary>A browser's subscription keys: a P-256 public point (65 bytes) and a 16-byte auth secret, base64url.</summary>
        private static PushTarget NewTarget()
        {
            using var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);

            return new PushTarget(Endpoint, Base64Url(PublicPoint(key.ExportParameters(includePrivateParameters: false))), Base64Url(RandomNumberGenerator.GetBytes(16)));
        }

        private static PushPayload NewPayload()
            => new("LifeManager", "Ler: ainda dá tempo hoje", "/habits/today", "habits-2026-11-11-20");

        private static byte[] PublicPoint(ECParameters parameters)
        {
            var point = new byte[65];
            point[0] = 0x04;
            parameters.Q.X!.CopyTo(point, 1);
            parameters.Q.Y!.CopyTo(point, 33);
            return point;
        }

        private static string Base64Url(byte[] bytes)
            => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private sealed class ListLogger : ILogger<WebPushSender>
        {
            public List<string> Messages { get; } = [];

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));
        }

        private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> answer) : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
                => Task.FromResult(answer(request));
        }
    }
}
