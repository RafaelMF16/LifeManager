using LifeManager.Domain.Shared.Results;
using Microsoft.AspNetCore.Mvc;
using System.Net;

namespace LifeManager.WebApi.Extensions
{
    public static class ResultExtensions
    {
        public static IActionResult Match<T>(this Result<T> result, Func<T, IActionResult> onSuccess)
        {
            if (result.IsSuccess)
                return onSuccess(result.Value);

            return ToErrorResult(result.Error);
        }

        public static IActionResult Match(this Result result, Func<IActionResult> onSuccess)
        {
            if (result.IsSuccess)
                return onSuccess();

            return ToErrorResult(result.Error);
        }

        private static ObjectResult ToErrorResult(Error error)
        {
            return error.Type switch
            {
                ErrorType.Validation => new BadRequestObjectResult(error),
                ErrorType.Conflict => new ConflictObjectResult(error),
                ErrorType.Unauthorized => new UnauthorizedObjectResult(error),
                ErrorType.NotFound => new NotFoundObjectResult(error),
                _ => new ObjectResult(error) { StatusCode = (int)HttpStatusCode.InternalServerError }
            };
        }
    }
}
