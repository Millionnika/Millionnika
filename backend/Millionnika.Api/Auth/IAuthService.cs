using Millionnika.Api.Entities;

namespace Millionnika.Api.Auth;

public interface IAuthService
{
    Task<AuthServiceResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<AuthServiceResult> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<ProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken);
}

public sealed record AuthServiceResult(User? User, Dictionary<string, string[]> Errors)
{
    public bool Succeeded => User is not null;

    public static AuthServiceResult Success(User user) => new(user, []);
    public static AuthServiceResult Failure(string field, string message) =>
        new(null, new Dictionary<string, string[]> { [field] = [message] });
}
