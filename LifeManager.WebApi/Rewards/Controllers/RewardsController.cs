using LifeManager.Application.Rewards.DTOs;
using LifeManager.Application.Rewards.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Rewards.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RewardsController(RewardService rewardService, RewardRedemptionService rewardRedemptionService) : Controller
    {
        private readonly RewardService _rewardService = rewardService;
        private readonly RewardRedemptionService _rewardRedemptionService = rewardRedemptionService;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] RewardListQueryDto query, CancellationToken cancellationToken)
            => (await _rewardService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The player's redemptions, newest first.</summary>
        [HttpGet("Redemptions")]
        public async Task<IActionResult> GetRedemptions([FromQuery] RewardRedemptionListQueryDto query, CancellationToken cancellationToken)
            => (await _rewardRedemptionService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Coins earned with habits per day lately, to price rewards in days.</summary>
        [HttpGet("EarningPace")]
        public async Task<IActionResult> GetEarningPace(CancellationToken cancellationToken)
            => Ok(await _rewardRedemptionService.GetEarningPaceAsync(User.GetUserId(), cancellationToken));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _rewardService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] RewardDto rewardDto, CancellationToken cancellationToken)
            => (await _rewardService.CreateAsync(rewardDto, User.GetUserId(), cancellationToken))
                .Match(reward => CreatedAtAction(nameof(GetById), new { id = reward.Id }, reward));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] RewardDto rewardDto, CancellationToken cancellationToken)
            => (await _rewardService.UpdateAsync(id, rewardDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Restore")]
        public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
            => (await _rewardService.RestoreAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Archives the reward: its redemptions are kept and it can be restored.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _rewardService.ArchiveAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);

        /// <summary>Spends the reward's price in coins; 409 Rewards.InsufficientCoins when the balance is short.</summary>
        [HttpPost("{id:int}/Redeem")]
        public async Task<IActionResult> Redeem(int id, CancellationToken cancellationToken)
            => (await _rewardRedemptionService.RedeemAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Gives a redemption's coins back; only on the day it was made.</summary>
        [HttpDelete("Redemptions/{redemptionId:int}")]
        public async Task<IActionResult> UndoRedemption(int redemptionId, CancellationToken cancellationToken)
            => (await _rewardRedemptionService.UndoAsync(redemptionId, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
