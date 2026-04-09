using System.Text.RegularExpressions;

namespace Cleanuparr.Infrastructure.Services;

public static partial class UriService
{
    [GeneratedRegex(@"^(?:\w+:\/\/)?([^\/\?:]+)", RegexOptions.IgnoreCase)]
    private static partial Regex DomainFallbackRegex();

    public static string? GetDomain(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return null;
        }

        // add "http://" if scheme is missing to help Uri.TryCreate
        if (!input.Contains("://"))
        {
            input = "http://" + input;
        }

        if (Uri.TryCreate(input, UriKind.Absolute, out var uri))
        {
            return uri.Host;
        }

        // url might be malformed
        var match = DomainFallbackRegex().Match(input);

        if (match.Success)
        {
            return match.Groups[1].Value;
        }

        // could not extract
        return null;
    }
}