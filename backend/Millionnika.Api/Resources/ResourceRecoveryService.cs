using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Entities;

namespace Millionnika.Api.Resources;

public sealed class ResourceRecoveryService(
    IOptions<ResourceRecoveryOptions> options,
    TimeProvider timeProvider) : IResourceRecoveryService
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();

    public RecoveryState Apply(GameCharacter character) =>
        ResourceRecoveryCalculator.Apply(character, UtcNow, options.Value);
}
