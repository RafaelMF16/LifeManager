using LifeManager.Application.AccountHolders.DTOs;
using LifeManager.Application.AccountHolders.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.AccountHolders.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class AccountHoldersController(AccountHolderService accountHolderService) : Controller
    {
        private readonly AccountHolderService _accountHolderService = accountHolderService;

        [HttpGet]
        public async Task<IActionResult> Get(CancellationToken cancellationToken)
            => Ok(await _accountHolderService.GetAsync(User.GetUserId(), cancellationToken));

        [HttpPut]
        public async Task<IActionResult> Replace([FromBody] AccountHoldersDto accountHoldersDto, CancellationToken cancellationToken)
            => (await _accountHolderService.ReplaceAsync(accountHoldersDto, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
