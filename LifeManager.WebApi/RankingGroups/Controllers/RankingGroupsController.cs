using LifeManager.Application.Ranking.DTOs;
using LifeManager.Application.Ranking.Services;
using LifeManager.Application.RankingGroups.DTOs;
using LifeManager.Application.RankingGroups.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace LifeManager.WebApi.RankingGroups.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class RankingGroupsController(
        RankingGroupService rankingGroupService,
        GroupRankingService groupRankingService,
        RankingReactionService rankingReactionService) : Controller
    {
        private readonly RankingGroupService _rankingGroupService = rankingGroupService;
        private readonly GroupRankingService _groupRankingService = groupRankingService;
        private readonly RankingReactionService _rankingReactionService = rankingReactionService;

        /// <summary>The user's groups (at most 5, so not paged), by name, with their place in each this week.</summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
            => Ok(await _groupRankingService.GetGroupsAsync(User.GetUserId(), cancellationToken));

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _rankingGroupService.GetAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The group's ranking in a week (the current one by default).</summary>
        [HttpGet("{id:int}/Ranking")]
        public async Task<IActionResult> GetRanking(int id, [FromQuery] DateOnly? weekStart, CancellationToken cancellationToken)
            => (await _groupRankingService.GetAsync(id, User.GetUserId(), weekStart, cancellationToken)).Match(Ok);

        /// <summary>Creates a group with the user as its owner and first member.</summary>
        [HttpPost]
        public async Task<IActionResult> Create(RankingGroupRequestDto request, CancellationToken cancellationToken)
            => (await _rankingGroupService.CreateAsync(request, User.GetUserId(), cancellationToken))
                .Match(group => CreatedAtAction(nameof(GetById), new { id = group.Id }, group));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Rename(int id, RankingGroupRequestDto request, CancellationToken cancellationToken)
            => (await _rankingGroupService.RenameAsync(id, request, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>A new invite code for the group (owner only); the old one stops working.</summary>
        [HttpPost("{id:int}/InviteCode")]
        public async Task<IActionResult> RegenerateInviteCode(int id, CancellationToken cancellationToken)
            => (await _rankingGroupService.RegenerateInviteCodeAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Leave")]
        public async Task<IActionResult> Leave(int id, CancellationToken cancellationToken)
            => (await _rankingGroupService.LeaveAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);

        [HttpDelete("{id:int}/Members/{memberId:int}")]
        public async Task<IActionResult> RemoveMember(int id, int memberId, CancellationToken cancellationToken)
            => (await _rankingGroupService.RemoveMemberAsync(id, memberId, User.GetUserId(), cancellationToken)).Match(NoContent);

        /// <summary>Reacts to a member's week (👏/🔥), or changes the reaction; returns that member's reactions in the week.</summary>
        [HttpPut("{id:int}/Members/{memberId:int}/Reaction")]
        public async Task<IActionResult> React(int id, int memberId, RankingReactionRequestDto request, CancellationToken cancellationToken)
            => (await _rankingReactionService.SetAsync(id, memberId, request, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpDelete("{id:int}/Members/{memberId:int}/Reaction")]
        public async Task<IActionResult> Unreact(int id, int memberId, [FromQuery] DateOnly? weekStart, CancellationToken cancellationToken)
            => (await _rankingReactionService.RemoveAsync(id, memberId, weekStart, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The group behind an invite code, to confirm before joining.</summary>
        [HttpGet("Invites/{code}")]
        [EnableRateLimiting(RankingGroupRateLimits.Invites)]
        public async Task<IActionResult> PreviewInvite(string code, CancellationToken cancellationToken)
            => (await _rankingGroupService.PreviewAsync(code, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("Invites/{code}/Join")]
        [EnableRateLimiting(RankingGroupRateLimits.Invites)]
        public async Task<IActionResult> Join(string code, CancellationToken cancellationToken)
            => (await _rankingGroupService.JoinAsync(code, User.GetUserId(), cancellationToken)).Match(Ok);
    }
}
