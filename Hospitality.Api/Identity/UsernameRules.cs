using System.Text.RegularExpressions;

namespace Hospitality.Api.Identity;

public static partial class UsernameRules
{
    public const int MinLength = 3;
    public const int MaxLength = 32;

    [GeneratedRegex("^[a-zA-Z0-9_]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AllowedPattern();

    public static bool IsValid(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return false;
        }

        return username.Length is >= MinLength and <= MaxLength && AllowedPattern().IsMatch(username);
    }
}
