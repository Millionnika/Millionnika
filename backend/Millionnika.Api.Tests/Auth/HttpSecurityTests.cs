using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Millionnika.Api.Data;
using Millionnika.Api.Tests.Infrastructure;
using Xunit;

namespace Millionnika.Api.Tests.Auth;

[Collection("PostgreSQL integration")]
public sealed class HttpSecurityTests : IAsyncLifetime
{
    private Process? server;
    private Uri address = null!;
    private readonly List<Guid> users = [];
    private readonly List<HttpClient> clients = [];
    private Dictionary<string, string> settings = [];

    public Task InitializeAsync() => Task.CompletedTask;

    private async Task StartAsync(Dictionary<string, string>? overrides = null)
    {
        // Fail before starting the API: never inherit the application's database connection.
        var connection = TestDatabaseSettings.Validate(Environment.GetEnvironmentVariable(TestDatabaseSettings.EnvironmentVariable));
        if (overrides is not null) settings = overrides;
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        address = new Uri($"http://127.0.0.1:{port}");
        var start = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(typeof(GameDbContext).Assembly.Location);
        start.ArgumentList.Add("--urls"); start.ArgumentList.Add(address.ToString());
        start.ArgumentList.Add("--contentRoot"); start.ArgumentList.Add(AppContext.BaseDirectory);
        start.Environment["ConnectionStrings__GameDatabase"] = connection;
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        start.Environment["DOTNET_ENVIRONMENT"] = "Production";
        start.Environment["ASPNETCORE_FORWARDEDHEADERS_ENABLED"] = "false";
        start.Environment["Logging__LogLevel__Default"] = "None";
        start.Environment["AuthProtection__LoginIpLimit"] = "100";
        start.Environment["AuthProtection__LoginNameLimit"] = "100";
        start.Environment["AuthProtection__RegistrationIpLimit"] = "100";
        foreach (var (key, value) in settings) start.Environment[$"AuthProtection__{key}"] = value;
        server = Process.Start(start)!;
        // Discard process logs; never include HTTP headers, credentials or tokens in test output.
        server.OutputDataReceived += (_, _) => { };
        server.ErrorDataReceived += (_, _) => { };
        server.BeginOutputReadLine(); server.BeginErrorReadLine();
        using var readiness = new HttpClient();
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (server.HasExited) throw new InvalidOperationException("Isolated HTTP server failed to start.");
            try
            {
                if ((await readiness.GetAsync(new Uri(address, "/api/health"))).IsSuccessStatusCode) return;
            }
            catch (HttpRequestException) { }
            await Task.Delay(100);
        }
        throw new TimeoutException("Isolated HTTP server startup timed out.");
    }

    private HttpClient Browser(string? cookie = null)
    {
        var handler = new HttpClientHandler { UseCookies = cookie is null };
        var client = new HttpClient(handler) { BaseAddress = address };
        if (cookie is not null) client.DefaultRequestHeaders.Add("Cookie", cookie);
        clients.Add(client);
        return client;
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, string path,
        object? body = null, string method = "POST", string? forwarded = null)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        request.Headers.Add("X-CSRF-Token", token.GetProperty("token").GetString());
        if (forwarded is not null) request.Headers.Add("X-Forwarded-For", forwarded);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }

    private async Task<Account> RegisterAsync(HttpClient client)
    {
        var username = $"Http_{Guid.NewGuid():N}"[..22];
        var password = Guid.NewGuid().ToString("N") + "Aa9!";
        using var response = await SendAsync(client, "/api/auth/register", new { username, email = $"{username}@http.invalid", password });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        await using var db = TestDatabase.CreateDbContext();
        var id = await db.Users.Where(user => user.Username == username).Select(user => user.Id).SingleAsync();
        users.Add(id);
        return new Account(id, username, password, SessionCookie(response));
    }

    private static string SessionCookie(HttpResponseMessage response) =>
        response.Headers.GetValues("Set-Cookie").Single(value => value.StartsWith("millionnika_session=")).Split(';')[0];

    [Theory]
    [InlineData("/api/auth/register", "POST")]
    [InlineData("/api/auth/login", "POST")]
    [InlineData("/api/auth/logout", "POST")]
    [InlineData("/api/game/training/execute", "POST")]
    [InlineData("/api/game/contract/execute", "POST")]
    [InlineData("/api/game/shop/buy", "POST")]
    [InlineData("/api/game/inventory/use", "POST")]
    [InlineData("/api/friends/requests", "POST")]
    [InlineData("/api/friends/requests/00000000-0000-0000-0000-000000000001/accept", "POST")]
    [InlineData("/api/friends/requests/00000000-0000-0000-0000-000000000001/reject", "POST")]
    [InlineData("/api/friends/requests/00000000-0000-0000-0000-000000000001", "DELETE")]
    [InlineData("/api/friends/00000000-0000-0000-0000-000000000001", "DELETE")]
    [InlineData("/api/players/00000000-0000-0000-0000-000000000001/description", "PUT")]
    [InlineData("/api/admin/players/00000000-0000-0000-0000-000000000001/resources", "PUT")]
    [InlineData("/api/admin/players/00000000-0000-0000-0000-000000000001/block", "PUT")]
    [InlineData("/api/admin/contracts/00000000-0000-0000-0000-000000000001", "PUT")]
    [InlineData("/api/admin/items/00000000-0000-0000-0000-000000000001", "PUT")]
    public async Task AllUnsafeRoutes_RejectMissingAndInvalidCsrf(string path, string method)
    {
        await StartAsync();
        var client = Browser();
        await RegisterAsync(client);
        foreach (var invalid in new[] { false, true })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path);
            request.Headers.Add("Origin", "http://localhost:3999");
            if (invalid) request.Headers.Add("X-CSRF-Token", "invalid");
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("csrf_invalid", body.GetProperty("code").GetString());
        }
    }

    [Fact]
    public async Task AnonymousLoginAndRegisterRequireCsrf()
    {
        await StartAsync();
        var client = Browser();
        foreach (var path in new[] { "/api/auth/login", "/api/auth/register" })
        {
            using var missing = await client.PostAsJsonAsync(path, new { });
            Assert.Equal(HttpStatusCode.Forbidden, missing.StatusCode);
            using var valid = await SendAsync(client, path, new { });
            Assert.Equal(HttpStatusCode.BadRequest, valid.StatusCode);
        }
    }

    [Fact]
    public async Task CsrfTokenIsBoundToCookieAndIdentity()
    {
        await StartAsync();
        var first = Browser(); var other = Browser();
        var anonymousToken = (await first.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("token").GetString();
        await other.GetAsync("/api/auth/csrf");
        using var crossed = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login") { Content = JsonContent.Create(new { }) };
        crossed.Headers.Add("X-CSRF-Token", anonymousToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await other.SendAsync(crossed)).StatusCode);
        await RegisterAsync(first);
        using var stale = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        stale.Headers.Add("X-CSRF-Token", anonymousToken);
        Assert.Equal(HttpStatusCode.Forbidden, (await first.SendAsync(stale)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(first, "/api/auth/logout")).StatusCode);
    }

    [Fact]
    public async Task ExpiredSessionIsRejectedByServer()
    {
        await StartAsync();
        var client = Browser(); var account = await RegisterAsync(client);
        await using (var db = TestDatabase.CreateDbContext())
            await db.UserSessions.Where(session => session.UserId == account.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(session => session.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(account.Cookie).GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task ConcurrentLoginAndBlockCannotLeaveLiveSession()
    {
        await StartAsync();
        var account = await RegisterAsync(Browser());
        var adminClient = Browser(); var admin = await RegisterAsync(adminClient);
        await using (var db = TestDatabase.CreateDbContext())
            await db.Users.Where(user => user.Id == admin.Id).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.Role, "Admin"));
        var loginTask = SendAsync(Browser(), "/api/auth/login", new { login = account.Name, password = account.Password });
        var blockTask = SendAsync(adminClient, $"/api/admin/players/{account.Id}/block", new { isBlocked = true, reason = "Concurrent security test" }, "PUT");
        await Task.WhenAll(loginTask, blockTask);
        using var login = await loginTask;
        using var block = await blockTask;
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        Assert.Contains(login.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.Unauthorized });
        if (login.IsSuccessStatusCode)
            Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(SessionCookie(login)).GetAsync("/api/profile")).StatusCode);
        await using var verify = TestDatabase.CreateDbContext();
        Assert.False(await verify.UserSessions.AnyAsync(session => session.UserId == account.Id));
    }

    [Fact]
    public async Task ValidCsrf_AllGameActionsAndBodylessFriendActionWork()
    {
        await StartAsync();
        var client = Browser();
        var account = await RegisterAsync(client);
        var other = Browser(); var recipient = await RegisterAsync(other);
        foreach (var (path, body) in new (string, object)[]
        {
            ("/api/game/training/execute", new { requestId = Guid.NewGuid(), stat = "strength" }),
            ("/api/game/contract/execute", new { requestId = Guid.NewGuid() }),
            ("/api/game/shop/buy", new { requestId = Guid.NewGuid(), itemId = "11111111-1111-1111-1111-111111111001", quantity = 1 }),
            ("/api/game/inventory/use", new { requestId = Guid.NewGuid(), itemId = "11111111-1111-1111-1111-111111111001", quantity = 1 })
        })
        {
            using var response = await SendAsync(client, path, body);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var sent = await SendAsync(client, "/api/friends/requests", new { playerId = recipient.Id });
        var relation = (await sent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("relationId").GetGuid();
        using var accepted = await SendAsync(other, $"/api/friends/requests/{relation}/accept");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using var profile = await client.GetAsync("/api/profile");
        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
        var data = await profile.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(100, data.GetProperty("energy").GetProperty("current").GetInt32());
        Assert.Equal(2, data.GetProperty("strength").GetInt32());
        Assert.Equal(8, data.GetProperty("nerve").GetProperty("current").GetInt32());
    }

    [Fact]
    public async Task LogoutRevokesOnlyCurrentSession_AndSurvivesRestart()
    {
        await StartAsync();
        var first = Browser(); var account = await RegisterAsync(first);
        var second = Browser();
        using var login = await SendAsync(second, "/api/auth/login", new { login = account.Name, password = account.Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var secondCookie = SessionCookie(login);
        using var logout = await SendAsync(first, "/api/auth/logout");
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(account.Cookie).GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await second.GetAsync("/api/profile")).StatusCode);
        await StopAsync(); await StartAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(account.Cookie).GetAsync("/api/profile")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Browser(secondCookie).GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task BlockRevokesAllSessions_UnblockRequiresNewLogin()
    {
        await StartAsync();
        var client = Browser(); var account = await RegisterAsync(client);
        var second = Browser();
        using var login = await SendAsync(second, "/api/auth/login", new { login = account.Name, password = account.Password });
        var secondCookie = SessionCookie(login);
        var adminClient = Browser(); var admin = await RegisterAsync(adminClient);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/players")).StatusCode);
        await using (var db = TestDatabase.CreateDbContext())
            await db.Users.Where(user => user.Id == admin.Id).ExecuteUpdateAsync(setters => setters.SetProperty(user => user.Role, "Admin"));
        using var block = await SendAsync(adminClient, $"/api/admin/players/{account.Id}/block", new { isBlocked = true, reason = "HTTP security test" }, "PUT");
        Assert.Equal(HttpStatusCode.OK, block.StatusCode);
        foreach (var cookie in new[] { account.Cookie, secondCookie })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(cookie).GetAsync("/api/profile")).StatusCode);
        using var deniedLogin = await SendAsync(Browser(), "/api/auth/login", new { login = account.Name, password = account.Password });
        Assert.Equal(HttpStatusCode.Unauthorized, deniedLogin.StatusCode);
        using var unblock = await SendAsync(adminClient, $"/api/admin/players/{account.Id}/block", new { isBlocked = false, reason = "HTTP security test" }, "PUT");
        Assert.Equal(HttpStatusCode.OK, unblock.StatusCode);
        foreach (var cookie in new[] { account.Cookie, secondCookie })
            Assert.Equal(HttpStatusCode.Unauthorized, (await Browser(cookie).GetAsync("/api/profile")).StatusCode);
        using var fresh = await SendAsync(Browser(), "/api/auth/login", new { login = account.Name, password = account.Password });
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Browser(SessionCookie(fresh)).GetAsync("/api/profile")).StatusCode);
    }

    [Fact]
    public async Task LoginLimit_NormalizesNameAcrossBrowsers_AndExpires()
    {
        await StartAsync(new() { ["LoginNameLimit"] = "1", ["WindowSeconds"] = "3" });
        var client = Browser(); var account = await RegisterAsync(client);
        using var first = await SendAsync(client, "/api/auth/login", new { login = account.Name, password = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.Unauthorized, first.StatusCode);
        var other = Browser();
        using var blocked = await SendAsync(other, "/api/auth/login", new { login = " " + account.Name.ToUpperInvariant() + " ", password = account.Password });
        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.NotNull(blocked.Headers.RetryAfter?.Delta);
        var message = await blocked.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(message.GetProperty("retryAfterSeconds").GetInt32() > 0);
        await Task.Delay(blocked.Headers.RetryAfter!.Delta!.Value + TimeSpan.FromMilliseconds(150));
        using var retry = await SendAsync(other, "/api/auth/login", new { login = account.Name, password = account.Password });
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
    }

    [Fact]
    public async Task LoginIpLimit_IgnoresSpoofedForwardedHeaderAndTrailingSlash()
    {
        await StartAsync(new() { ["LoginIpLimit"] = "2" });
        var client = Browser();
        for (var i = 0; i < 3; i++)
        {
            using var response = await SendAsync(client, "/api/auth/login/", new { login = $"Missing{i}", password = Guid.NewGuid().ToString() }, forwarded: $"192.0.2.{i + 1}");
            Assert.Equal(i < 2 ? HttpStatusCode.Unauthorized : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    [Fact]
    public async Task RegistrationIpLimit_IsEnforced()
    {
        await StartAsync(new() { ["RegistrationIpLimit"] = "2" });
        var client = Browser();
        for (var i = 0; i < 3; i++)
        {
            using var response = await SendAsync(client, "/api/auth/register/", new { }, forwarded: $"192.0.2.{i + 1}");
            Assert.Equal(i < 2 ? HttpStatusCode.BadRequest : HttpStatusCode.TooManyRequests, response.StatusCode);
        }
    }

    private async Task StopAsync()
    {
        if (server is null) return;
        if (!server.HasExited) { server.Kill(entireProcessTree: true); await server.WaitForExitAsync(); }
        server.Dispose(); server = null;
    }

    public async Task DisposeAsync()
    {
        await StopAsync();
        foreach (var client in clients) client.Dispose();
        if (users.Count == 0) return;
        await using var db = TestDatabase.CreateDbContext();
        await db.FriendRelations.Where(value => users.Contains(value.UserLowId) || users.Contains(value.UserHighId)).ExecuteDeleteAsync();
        await db.AdminAuditLogs.Where(value => users.Contains(value.AdminUserId)).ExecuteDeleteAsync();
        await db.Users.Where(value => users.Contains(value.Id)).ExecuteDeleteAsync();
    }

    private sealed record Account(Guid Id, string Name, string Password, string Cookie);
}
