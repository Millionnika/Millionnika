using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Millionnika.Api.Auth;
using Millionnika.Api.Configuration;
using Millionnika.Api.Data;
using Millionnika.Api.Entities;
using Millionnika.Api.Resources;

namespace Millionnika.Api.Admin;

public static class AdminBootstrapper
{
    public static async Task<int> RunAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        Console.Write("Имя администратора: ");
        var username = Console.ReadLine()?.Trim();
        Console.Write("Email администратора: ");
        var email = Console.ReadLine()?.Trim();
        Console.Write("Пароль администратора: ");
        var password = ReadPassword();
        Console.WriteLine();

        var request = new RegisterRequest(username, email, password);
        var errors = AuthInputValidator.ValidateRegistration(request);
        if (errors.Count > 0)
        {
            Console.Error.WriteLine(string.Join(" ", errors.Values.SelectMany(value => value)));
            return 1;
        }

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
        var normalizedUsername = username!.ToUpperInvariant();
        var normalizedEmail = email!.ToUpperInvariant();
        if (await db.Users.AnyAsync(user =>
                user.NormalizedUsername == normalizedUsername || user.NormalizedEmail == normalizedEmail,
                cancellationToken))
        {
            Console.Error.WriteLine("Пользователь с таким именем или email уже существует.");
            return 1;
        }

        var defaults = scope.ServiceProvider.GetRequiredService<IOptions<CharacterDefaultsOptions>>().Value;
        var time = scope.ServiceProvider.GetRequiredService<IResourceRecoveryService>().UtcNow;
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        var user = new User
        {
            Id = Guid.NewGuid(), Username = username, NormalizedUsername = normalizedUsername,
            Email = email, NormalizedEmail = normalizedEmail, Role = "Admin", CreatedAtUtc = time
        };
        user.PasswordHash = hasher.HashPassword(user, password!);
        user.Character = new GameCharacter
        {
            Id = Guid.NewGuid(), UserId = user.Id, Level = defaults.Level, Credits = defaults.Credits,
            Energy = defaults.Energy, MaxEnergy = defaults.MaxEnergy, LastEnergyRecoveryAtUtc = time,
            Nerve = defaults.Nerve, MaxNerve = defaults.MaxNerve, LastNerveRecoveryAtUtc = time,
            Health = defaults.Health, MaxHealth = defaults.MaxHealth, Strength = defaults.Strength,
            Speed = defaults.Speed, Defense = defaults.Defense, Dexterity = defaults.Dexterity,
            Experience = defaults.Experience
        };
        db.Users.Add(user);
        db.AdminAuditLogs.Add(AdminAuditFactory.AdminCreated(user.Id, time));
        await db.SaveChangesAsync(cancellationToken);
        Console.WriteLine("Администратор создан. Пароль нигде не сохранён в открытом виде.");
        return 0;
    }

    private static string ReadPassword()
    {
        var password = new List<char>();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter) return new string([.. password]);
            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Count > 0) password.RemoveAt(password.Count - 1);
                continue;
            }
            if (!char.IsControl(key.KeyChar)) password.Add(key.KeyChar);
        }
    }
}
