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

            return result.Error.Type switch
            {
                ErrorType.Validation => new BadRequestObjectResult(result.Error),
                ErrorType.Conflict => new ConflictObjectResult(result.Error),
                ErrorType.Unauthorized => new UnauthorizedObjectResult(result.Error),
                ErrorType.NotFound => new NotFoundObjectResult(result.Error),
                _ => new ObjectResult(result.Error) { StatusCode = (int)HttpStatusCode.InternalServerError }
            };
        }
    }
}