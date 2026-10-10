using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Auth;

public sealed class SessionService(GameDbContext db, TimeProvider clock)
{
    public const string SessionClaim = "session_id";

    public async Task<bool> SignInAsync(HttpContext context, Guid userId)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(context.RequestAborted);
        // Same lock as administrative blocking; read fresh data even after LoginAsync tracked User.
        var user = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM users WHERE \"Id\" = {userId} FOR UPDATE")
            .AsNoTracking().SingleOrDefaultAsync(context.RequestAborted);
        if (user is null || user.IsBlocked) return false;

        var session = new UserSession
        {
            Id = Guid.NewGuid(), UserId = userId, ExpiresAtUtc = clock.GetUtcNow().AddDays(7)
        };
        db.UserSessions.Add(session);
        await db.SaveChangesAsync(context.RequestAborted);
        await transaction.CommitAsync(context.RequestAborted);
        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(SessionClaim, session.Id.ToString())
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        await context.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties
            {
                IsPersistent = true, AllowRefresh = false, ExpiresUtc = session.ExpiresAtUtc
            });
        return true;
    }

    public async Task SignOutAsync(HttpContext context)
    {
        if (Guid.TryParse(context.User.FindFirstValue(SessionClaim), out var id) &&
            Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            await db.UserSessions.Where(session => session.Id == id && session.UserId == userId)
                .ExecuteDeleteAsync(context.RequestAborted);
        await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
