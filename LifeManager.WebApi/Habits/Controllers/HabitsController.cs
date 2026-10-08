using LifeManager.Application.Habits.DTOs;
using LifeManager.Application.Habits.Services;
using LifeManager.WebApi.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LifeManager.WebApi.Habits.Controllers
{
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class HabitsController(HabitService habitService, HabitCheckInService habitCheckInService) : Controller
    {
        private readonly HabitService _habitService = habitService;
        private readonly HabitCheckInService _habitCheckInService = habitCheckInService;

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] HabitListQueryDto query, CancellationToken cancellationToken)
            => (await _habitService.GetPagedAsync(query, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>The day's checklist, plus yesterday's habits that can still be checked in.</summary>
        [HttpGet("Today")]
        public async Task<IActionResult> GetToday(CancellationToken cancellationToken)
            => (await _habitCheckInService.GetTodayAsync(User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/CheckIns")]
        public async Task<IActionResult> CheckIn(int id, [FromBody] HabitCheckInDto checkInDto, CancellationToken cancellationToken)
            => (await _habitCheckInService.CheckInAsync(id, checkInDto, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <param name="date">"yyyy-MM-dd": today or yesterday.</param>
        [HttpDelete("{id:int}/CheckIns/{date}")]
        public async Task<IActionResult> UndoCheckIn(int id, DateOnly date, CancellationToken cancellationToken)
            => (await _habitCheckInService.UndoCheckInAsync(id, date, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
            => (await _habitService.GetByIdAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] HabitDto habitDto, CancellationToken cancellationToken)
            => (await _habitService.CreateAsync(habitDto, User.GetUserId(), cancellationToken))
                .Match(habit => CreatedAtAction(nameof(GetById), new { id = habit.Id }, habit));

        [HttpPut("{id:int}")]
        public async Task<IActionResult> Update(int id, [FromBody] HabitUpdateDto habitDto, CancellationToken cancellationToken)
            => (await _habitService.UpdateAsync(id, habitDto, User.GetUserId(), cancellationToken)).Match(Ok);

        [HttpPost("{id:int}/Restore")]
        public async Task<IActionResult> Restore(int id, CancellationToken cancellationToken)
            => (await _habitService.RestoreAsync(id, User.GetUserId(), cancellationToken)).Match(Ok);

        /// <summary>Archives the habit: its history is kept and it can be restored.</summary>
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
            => (await _habitService.ArchiveAsync(id, User.GetUserId(), cancellationToken)).Match(NoContent);
    }
}
