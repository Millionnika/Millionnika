using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Millionnika.Api.Security;
using Xunit;

namespace Millionnika.Api.Tests.Security;

public sealed class SafeExceptionMiddlewareTests
{
    [Fact]
    public async Task UnhandledError_ReturnsSafeIdAndDoesNotLogSecretDetails()
    {
        const string secret = "cookie-secret-marker";
        var logger = new CaptureLogger<SafeExceptionMiddleware>();
        var middleware = new SafeExceptionMiddleware(
            _ => throw new InvalidOperationException($"{secret} C:\\private\\source.cs"), logger);
        var context = new DefaultHttpContext();
        context.Request.Headers.Cookie = $"session={secret}";
        context.Response.Body = new MemoryStream();

        await middleware.InvokeAsync(context);

        Assert.Equal(StatusCodes.Status500InternalServerError, context.Response.StatusCode);
        context.Response.Body.Position = 0;
        using var body = await JsonDocument.ParseAsync(context.Response.Body);
        Assert.Equal("Внутренняя ошибка сервера.", body.RootElement.GetProperty("message").GetString());
        Assert.Equal(32, body.RootElement.GetProperty("errorId").GetString()!.Length);
        var responseText = body.RootElement.GetRawText();
        Assert.DoesNotContain(secret, responseText);
        Assert.DoesNotContain("source.cs", responseText);
        Assert.DoesNotContain(secret, logger.Message);
        Assert.DoesNotContain("source.cs", logger.Message);
        Assert.DoesNotContain("Cookie", logger.Message);
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public string Message { get; private set; } = string.Empty;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter) =>
            Message = formatter(state, exception);
    }
}
