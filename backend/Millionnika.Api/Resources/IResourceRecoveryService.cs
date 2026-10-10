using Millionnika.Api.Entities;

namespace Millionnika.Api.Resources;

public interface IResourceRecoveryService
{
    RecoveryState Apply(GameCharacter character);
    DateTimeOffset UtcNow { get; }
}
