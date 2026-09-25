using LifeManager.Domain.Users.ValueObjects;
using System.Security.Claims;

namespace LifeManager.WebApi.Extensions
{
    public static class ClaimsExtensions
    {
        public static UserId GetUserId(this ClaimsPrincipal user)
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);

            if (!int.TryParse(userIdClaim, out var userId))
                throw new InvalidOperationException("Authenticated user has no valid UserId claim");

            return new(userId);
        }
    }
}