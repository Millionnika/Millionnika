using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;
using Millionnika.Api.Game;
using Npgsql;

namespace Millionnika.Api.Auth;

public sealed class AuthService(
    GameDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    IOptions<CharacterDefaultsOptions> characterDefaults,
    IResourceRecoveryService resourceRecovery,
    IProgressionService progression) : IAuthService
{
    public async Task<AuthServiceResult> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var username = request.Username!.Trim();
        var email = request.Email!.Trim();
        var normalizedUsername = Normalize(username);
        var normalizedEmail = Normalize(email);

        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedUsername == normalizedUsername,
                cancellationToken))
        {
            return AuthServiceResult.Failure("username", "Это имя уже занято.");
        }

        if (await dbContext.Users.AnyAsync(
                user => user.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            return AuthServiceResult.Failure("email", "Этот email уже используется.");
        }

        var defaults = characterDefaults.Value;
        var utcNow = resourceRecovery.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            NormalizedUsername = normalizedUsername,
            Email = email,
            NormalizedEmail = normalizedEmail,
            CreatedAtUtc = utcNow
        };

        user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
        user.Character = new GameCharacter
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Level = defaults.Level,
            Credits = defaults.Credits,
            Energy = defaults.Energy,
            MaxEnergy = defaults.MaxEnergy,
            LastEnergyRecoveryAtUtc = utcNow,
            Nerve = defaults.Nerve,
            MaxNerve = defaults.MaxNerve,
            LastNerveRecoveryAtUtc = utcNow,
            Health = defaults.Health,
            MaxHealth = defaults.MaxHealth,
            Strength = defaults.Strength,
            Speed = defaults.Speed,
            Defense = defaults.Defense,
            Dexterity = defaults.Dexterity,
            Experience = defaults.Experience,
            HackingLevel = 1,
            HackingExperience = 0,
            HuntingLevel = 1,
            HuntingExperience = 0
        };

        dbContext.Users.Add(user);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return AuthServiceResult.Success(user);
        }
        catch (DbUpdateException exception)
            when (exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return AuthServiceResult.Failure("account", "Имя или email уже используются.");
        }
    }

    public async Task<AuthServiceResult> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var normalizedLogin = Normalize(request.Login!.Trim());
        var user = await dbContext.Users.SingleOrDefaultAsync(
            candidate => candidate.NormalizedUsername == normalizedLogin ||
                         candidate.NormalizedEmail == normalizedLogin,
            cancellationToken);

        if (user is null)
        {
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password!);
        if (verification == PasswordVerificationResult.Failed)
        {
            return InvalidCredentials();
        }

        if (user.IsBlocked)
        {
            return AuthServiceResult.Failure("account", "Учётная запись заблокирована.");
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password!);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return AuthServiceResult.Success(user);
    }

    public async Task<ProfileResponse?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var account = await dbContext.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new { user.Username, user.Description, user.Role })
            .SingleOrDefaultAsync(cancellationToken);

        if (account is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        var character = await dbContext.Characters
            .FromSqlInterpolated($"SELECT * FROM characters WHERE \"UserId\" = {userId} FOR UPDATE")
            .SingleAsync(cancellationToken);
        var recovery = resourceRecovery.Apply(character);
        progression.Apply(character);
        var equipment = await dbContext.EquipmentInstances.AsNoTracking()
            .Where(instance => instance.CharacterId == character.Id && instance.EquippedSlot != null)
            .OrderBy(instance => instance.EquippedSlot)
            .Select(instance => new EquippedItemResponse(
                instance.Id, instance.EquippedSlot!, instance.Item.Name, instance.Item.Type,
                instance.Damage, instance.Accuracy, instance.Armor, instance.Item.ArmorCoverage,
                instance.Item.Caliber, instance.Item.ClipSize, instance.Item.RateOfFireMin, instance.Item.RateOfFireMax))
            .ToListAsync(cancellationToken);

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ProfileResponse(
            account.Username,
            account.Description,
            account.Role,
            character.Level,
            character.Credits,
            new ResourceValue(character.Energy, character.MaxEnergy, recovery.NextEnergyRecoveryAtUtc),
            new ResourceValue(character.Nerve, character.MaxNerve, recovery.NextNerveRecoveryAtUtc),
            new ResourceValue(character.Health, character.MaxHealth, null),
            character.Strength,
            character.Speed,
            character.Defense,
            character.Dexterity,
            character.Experience,
            progression.GetGeneral(character.Experience),
            progression.GetHacking(character.HackingExperience),
            progression.GetHunting(character.HuntingExperience),
            equipment);
    }

    private static string Normalize(string value) => value.ToUpperInvariant();

    private static AuthServiceResult InvalidCredentials() =>
        AuthServiceResult.Failure("credentials", "Неверное имя, email или пароль.");
}
