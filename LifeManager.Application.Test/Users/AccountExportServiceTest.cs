using LifeManager.Application.Test.Configurations;
using LifeManager.Application.Test.Users.Mocks;
using LifeManager.Application.Users.Services;
using LifeManager.Domain.Users.ValueObjects;
using Microsoft.Extensions.DependencyInjection;

namespace LifeManager.Application.Test.Users
{
    [Collection("ApplicationServices")]
    public class AccountExportServiceTest : BaseTest
    {
        private readonly AccountExportService _service;
        private readonly FakeTimeProvider _clock;

        public AccountExportServiceTest()
        {
            _service = ServiceProvider.GetRequiredService<AccountExportService>();
            _clock = (FakeTimeProvider)ServiceProvider.GetRequiredService<TimeProvider>();
            AccountDataExporterMock.LastExport = null;
        }

        [Fact]
        public void FileName_ShouldCarryTodayInTheBusinessTimeZone()
        {
            // 01:30 UTC on 1 December is still 30 November in São Paulo.
            _clock.UtcNow = new DateTimeOffset(2026, 12, 1, 1, 30, 0, TimeSpan.Zero);

            Assert.Equal("lifemanager-2026-11-30.json", _service.FileName());
        }

        [Fact]
        public async Task WriteAsync_ShouldExportTheGivenUserAtTheCurrentTime()
        {
            _clock.UtcNow = new DateTimeOffset(2026, 11, 10, 15, 0, 0, TimeSpan.Zero);
            using var output = new MemoryStream();

            await _service.WriteAsync(new UserId(7), output, CancellationToken.None);

            Assert.Equal((new UserId(7), _clock.UtcNow), AccountDataExporterMock.LastExport);
            Assert.NotEqual(0, output.Length);
        }
    }
}
