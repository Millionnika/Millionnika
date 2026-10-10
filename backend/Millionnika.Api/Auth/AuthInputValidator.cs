using System.Net.Mail;
using System.Text.RegularExpressions;

namespace Millionnika.Api.Auth;

public static class AuthInputValidator
{
    private static readonly Regex UsernamePattern = new(
        @"^[\p{L}\p{Nd}_-]+$",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking);

    public static Dictionary<string, string[]> ValidateRegistration(RegisterRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var username = request.Username?.Trim() ?? string.Empty;
        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (username.Length is < 3 or > 24)
        {
            errors["username"] = ["Имя должно содержать от 3 до 24 символов."];
        }
        else if (!UsernamePattern.IsMatch(username))
        {
            errors["username"] = ["В имени разрешены буквы, цифры, дефис и подчёркивание."];
        }

        if (email.Length > 254 ||
            !MailAddress.TryCreate(email, out var address) ||
            !address.Address.Equals(email, StringComparison.OrdinalIgnoreCase))
        {
            errors["email"] = ["Введите корректный email."];
        }

        var passwordErrors = ValidatePassword(password);
        if (passwordErrors.Count > 0)
        {
            errors["password"] = [.. passwordErrors];
        }

        return errors;
    }

    public static Dictionary<string, string[]> ValidateLogin(LoginRequest request)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var login = request.Login?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (login.Length is < 3 or > 254)
        {
            errors["login"] = ["Введите имя игрока или email."];
        }

        if (password.Length is < 1 or > 128)
        {
            errors["password"] = ["Введите пароль."];
        }

        return errors;
    }

    private static List<string> ValidatePassword(string password)
    {
        var errors = new List<string>(4);

        if (password.Length is < 10 or > 128)
        {
            errors.Add("Пароль должен содержать от 10 до 128 символов.");
        }

        if (!password.Any(char.IsUpper))
        {
            errors.Add("Добавьте заглавную букву.");
        }

        if (!password.Any(char.IsLower))
        {
            errors.Add("Добавьте строчную букву.");
        }

        if (!password.Any(char.IsDigit))
        {
            errors.Add("Добавьте цифру.");
        }

        if (password.Any(char.IsWhiteSpace))
        {
            errors.Add("Пароль не должен содержать пробелы.");
        }

        return errors;
    }
}
