using LifeManager.Domain.Shared.Results;
using System.Net;
using System.Text.Json;

namespace LifeManager.WebApi
{
    public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        private static readonly JsonSerializerOptions SerializerOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public async Task InvokeAsync(HttpContext httpContext)
        {
            try
            {
                await next(httpContext);
            }
            catch (Exception ex)
            {
                HandleException(httpContext, ex);
                await httpContext.Response.WriteAsync(JsonSerializer.Serialize(GetError(ex), SerializerOptions));
            }
        }

        private void HandleException(HttpContext context, Exception exception)
        {
            logger.LogError(exception, "Unhandled exception");

            context.Response.StatusCode = exception is BadHttpRequestException
                ? (int)HttpStatusCode.BadRequest
                : (int)HttpStatusCode.InternalServerError;

            context.Response.ContentType = "application/json";
        }

        private static Error GetError(Exception exception) => exception switch
        {
            BadHttpRequestException => Error.Validation("Request.Malformed", "The request body is malformed"),
            _ => Error.Failure("Server.UnexpectedError", "An unexpected error occurred")
        };
    }
}
