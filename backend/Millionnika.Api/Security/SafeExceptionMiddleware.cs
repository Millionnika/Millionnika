namespace Millionnika.Api.Security;

public sealed class SafeExceptionMiddleware(RequestDelegate next, ILogger<SafeExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception exception)
        {
            var errorId = Guid.NewGuid().ToString("N");
            logger.LogError(
                "Unhandled API error {ErrorId} ({ExceptionType})",
                errorId,
                exception.GetType().Name);

            if (context.Response.HasStarted)
            {
                context.Abort();
                return;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            await context.Response.WriteAsJsonAsync(new
            {
                message = "Внутренняя ошибка сервера.",
                errorId
            });
        }
    }
}
