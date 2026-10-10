using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;

namespace Millionnika.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");
        auth.MapGet("/csrf", (HttpContext context, IAntiforgery antiforgery) =>
        {
            context.Response.Headers.CacheControl = "no-store";
            return Results.Ok(new { token = antiforgery.GetAndStoreTokens(context).RequestToken });
        });

        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", Session).RequireAuthorization();

        endpoints.MapGet("/api/profile", ProfileAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
        SessionService sessions,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = AuthInputValidator.ValidateRegistration(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await authService.RegisterAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors, statusCode: StatusCodes.Status409Conflict);
        }

        if (!await sessions.SignInAsync(httpContext, result.User!.Id)) return Results.Unauthorized();
        return Results.Created("/api/profile", new AuthResponse(result.User!.Username, result.User.Role));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        SessionService sessions,
        AuthRequestLimiter limiter,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = AuthInputValidator.ValidateLogin(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var retry = limiter.CheckLogin(request.Login!);
        if (retry > 0) return AuthRequestLimiter.Rejected(httpContext, retry);
        var result = await authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors, statusCode: StatusCodes.Status401Unauthorized);
        }

        if (!await sessions.SignInAsync(httpContext, result.User!.Id)) return Results.Unauthorized();
        return Results.Ok(new AuthResponse(result.User!.Username, result.User.Role));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext, SessionService sessions)
    {
        await sessions.SignOutAsync(httpContext);
        return Results.NoContent();
    }

    private static IResult Session(ClaimsPrincipal principal)
    {
        return Results.Ok(new AuthResponse(
            principal.Identity!.Name!,
            principal.FindFirstValue(ClaimTypes.Role) ?? "Player"));
    }

    private static async Task<IResult> ProfileAsync(
        ClaimsPrincipal principal,
        IAuthService authService,
        CancellationToken cancellationToken)
    {
        var identifier = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(identifier, out var userId))
        {
            return Results.Unauthorized();
        }

        var profile = await authService.GetProfileAsync(userId, cancellationToken);
        return profile is null ? Results.Unauthorized() : Results.Ok(profile);
    }

}
