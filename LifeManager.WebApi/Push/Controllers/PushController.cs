using LifeManager.Application.Push.DTOs;
using LifeManager.Application.Push.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Push.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class PushController(PushService pushService) : Controller
    {
        private readonly PushService _pushService = pushService;

        [HttpGet("PublicKey")]
        public IActionResult GetPublicKey()
            => _pushService.GetPublicKey().Match(Ok);

        /// <summary>Subscribes this device (upsert by endpoint; a device of another account changes owner).</summary>
        [HttpPut("Subscriptions")]
        public async Task<IActionResult> SaveSubscription([FromBody] PushSubscriptionDto dto, CancellationToken cancellationToken)
            => (await _pushService.SaveSubscriptionAsync(dto, User.GetUserId(), cancellationToken)).Match(NoContent);

        /// <summary>Unsubscribes this device; always 204.</summary>
        [HttpDelete("Subscriptions")]
        public async Task<IActionResult> DeleteSubscription([FromBody] PushSubscriptionDeleteDto dto, CancellationToken cancellationToken)
        {
            await _pushService.DeleteSubscriptionAsync(dto, User.GetUserId(), cancellationToken);
            return NoContent();
        }
    }
}
