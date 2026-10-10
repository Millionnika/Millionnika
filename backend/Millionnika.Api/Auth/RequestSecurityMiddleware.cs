using Microsoft.AspNetCore.Antiforgery;

namespace Millionnika.Api.Auth;

public sealed class RequestSecurityMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAntiforgery antiforgery, AuthRequestLimiter limiter)
    {
        if (!HttpMethods.IsGet(context.Request.Method) && !HttpMethods.IsHead(context.Request.Method) &&
            !HttpMethods.IsOptions(context.Request.Method))
        {
            var path = context.Request.Path.Value?.TrimEnd('/');
            if (string.Equals(path, "/api/auth/login", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(path, "/api/auth/register", StringComparison.OrdinalIgnoreCase))
            {
                var retry = limiter.CheckIp(context,
                    string.Equals(path, "/api/auth/register", StringComparison.OrdinalIgnoreCase));
                if (retry > 0) { await AuthRequestLimiter.Rejected(context, retry).ExecuteAsync(context); return; }
            }
            try { await antiforgery.ValidateRequestAsync(context); }
            catch (AntiforgeryValidationException)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { message = "Проверка безопасности не пройдена. Обновите страницу и повторите действие.", code = "csrf_invalid" });
                return;
            }
        }
        await next(context);
    }
}
