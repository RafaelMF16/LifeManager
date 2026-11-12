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
    }
}
