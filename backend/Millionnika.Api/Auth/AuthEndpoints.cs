using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Millionnika.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var auth = endpoints.MapGroup("/api/auth");

        auth.MapPost("/register", RegisterAsync);
        auth.MapPost("/login", LoginAsync);
        auth.MapPost("/logout", (Func<HttpContext, Task<IResult>>)LogoutAsync).RequireAuthorization();
        auth.MapGet("/session", Session).RequireAuthorization();

        endpoints.MapGet("/api/profile", ProfileAsync).RequireAuthorization();

        return endpoints;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IAuthService authService,
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

        await SignInAsync(httpContext, result.User!);
        return Results.Created("/api/profile", new AuthResponse(result.User!.Username, result.User.Role));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IAuthService authService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = AuthInputValidator.ValidateLogin(request);
        if (errors.Count > 0)
        {
            return Results.ValidationProblem(errors);
        }

        var result = await authService.LoginAsync(request, cancellationToken);
        if (!result.Succeeded)
        {
            return Results.ValidationProblem(result.Errors, statusCode: StatusCodes.Status401Unauthorized);
        }

        await SignInAsync(httpContext, result.User!);
        return Results.Ok(new AuthResponse(result.User!.Username, result.User.Role));
    }

    private static async Task<IResult> LogoutAsync(HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
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

    private static Task SignInAsync(HttpContext httpContext, Entities.User user)
    {
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role)
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);

        return httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IsPersistent = true,
                AllowRefresh = true
            });
    }
}
