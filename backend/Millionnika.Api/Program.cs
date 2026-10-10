using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Auth;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Health;
using Millionnika.Api.Game;
using Millionnika.Api.Inventory;
using Millionnika.Api.Admin;
using Millionnika.Api.Players;
using System.Security.Claims;
using Millionnika.Api.Resources;
using Millionnika.Api.Friends;
using Millionnika.Api.Security;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IDatabaseHealthService, PostgreSqlHealthService>();
builder.Services.AddOptions<CharacterDefaultsOptions>()
    .BindConfiguration(CharacterDefaultsOptions.SectionName)
    .Validate(GameNumericLimits.AreCharacterDefaultsValid,
        "CharacterDefaults contains values outside the supported numeric limits.")
    .ValidateOnStart();
builder.Services.AddOptions<TrainingOptions>()
    .BindConfiguration(TrainingOptions.SectionName)
    .Validate(GameNumericLimits.IsTrainingValid,
        "Training contains values outside the supported numeric limits.")
    .ValidateOnStart();
builder.Services.Configure<ResourceRecoveryOptions>(
    builder.Configuration.GetSection(ResourceRecoveryOptions.SectionName));
builder.Services.AddDbContext<GameDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("GameDatabase") ??
        throw new InvalidOperationException("ConnectionStrings:GameDatabase is not configured.");
    options.UseNpgsql(connectionString);
});
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<SessionService>();
builder.Services.AddOptions<AuthProtectionOptions>().BindConfiguration("AuthProtection")
    .Validate(options => options.LoginIpLimit > 0 && options.LoginNameLimit > 0 &&
        options.RegistrationIpLimit > 0 && options.WindowSeconds > 0 && options.WindowSeconds <= 86400 &&
        options.MaxBuckets > 0, "AuthProtection limits must be positive; window must be at most one day.")
    .ValidateOnStart();
builder.Services.AddSingleton<AuthRequestLimiter>();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-Token";
    options.Cookie.Name = "millionnika_csrf";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
});
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<IGameActionService, GameActionService>();
builder.Services.AddSingleton<IRandomSource, RandomSource>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IResourceRecoveryService, ResourceRecoveryService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IAdminService, AdminService>();
builder.Services.AddScoped<IPlayerDirectoryService, PlayerDirectoryService>();
builder.Services.AddScoped<IFriendService, FriendService>();
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.Name = "millionnika_session";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
        options.SlidingExpiration = false;
        options.Events.OnRedirectToLogin = context =>
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        };
        options.Events.OnRedirectToAccessDenied = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        };
        options.Events.OnValidatePrincipal = async context =>
        {
            var identifier = context.Principal?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(identifier, out var userId))
            {
                context.RejectPrincipal();
                return;
            }

            var db = context.HttpContext.RequestServices.GetRequiredService<GameDbContext>();
            var account = await db.Users.AsNoTracking()
                .Where(user => user.Id == userId)
                .Select(user => new { user.Username, user.Role, user.IsBlocked })
                .SingleOrDefaultAsync(context.HttpContext.RequestAborted);
            var now = context.HttpContext.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
            var sessionValid = Guid.TryParse(context.Principal?.FindFirstValue(SessionService.SessionClaim), out var sessionId) &&
                await db.UserSessions.AsNoTracking().AnyAsync(session => session.Id == sessionId &&
                    session.UserId == userId && session.ExpiresAtUtc > now, context.HttpContext.RequestAborted);
            if (account is null || account.IsBlocked || !sessionValid)
            {
                context.RejectPrincipal();
                await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return;
            }

            if (context.Principal?.FindFirstValue(ClaimTypes.Role) != account.Role)
            {
                var identity = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                    new Claim(ClaimTypes.Name, account.Username),
                    new Claim(ClaimTypes.Role, account.Role),
                    new Claim(SessionService.SessionClaim, sessionId.ToString())
                ], CookieAuthenticationDefaults.AuthenticationScheme);
                context.ReplacePrincipal(new ClaimsPrincipal(identity));
                context.ShouldRenew = true;
            }
        };
    });
builder.Services.AddAuthorization();
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:3000")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});

var app = builder.Build();

app.UseMiddleware<SafeExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseMiddleware<RequestSecurityMiddleware>();
app.UseAuthorization();

app.MapGet("/", () => Results.Ok(new
{
    name = "Миллионика API",
    status = "available"
}));

app.MapGet("/api/health", async (
    IDatabaseHealthService databaseHealthService,
    CancellationToken cancellationToken) =>
{
    var database = await databaseHealthService.CheckAsync(cancellationToken);

    return Results.Ok(new
    {
        serverAvailable = true,
        database
    });
});

app.MapAuthEndpoints();
app.MapGameEndpoints();
app.MapInventoryEndpoints();
app.MapAdminEndpoints();
app.MapPlayerDirectoryEndpoints();
app.MapFriendEndpoints();

try
{
    await using var scope = app.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    await dbContext.Database.MigrateAsync();
}
catch (Exception exception) when (exception is NpgsqlException or TimeoutException)
{
    app.Logger.LogError(
        "Database migrations could not be applied during startup ({ExceptionType})",
        exception.GetType().Name);
}

if (args.Contains("--create-admin", StringComparer.Ordinal))
{
    Environment.ExitCode = await AdminBootstrapper.RunAsync(app.Services, CancellationToken.None);
    return;
}

app.Run();
