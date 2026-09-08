using System.Text.RegularExpressions;
using Cleanuparr.Domain.Enums;

namespace Cleanuparr.Infrastructure.Services;

/// <summary>
/// Classifies tracker error messages reported by download clients.
/// Patterns come from qbit_manage and autobrr/qui.
/// </summary>
public static partial class TrackerMessageClassifier
{
    [GeneratedRegex(@"https?://\S+", RegexOptions.IgnoreCase)]
    private static partial Regex UrlRegex();

    private static readonly string[] Inconclusive =
    [
        "could not parse bencoded data",
        "expected value (list, dict, int or string) in bencoded string",
        "missing info_hash",
        "passkey",
        "torrent has been postponed",
        "you have reached the client limit for this torrent",
    ];

    private static readonly string[] TrackerDown =
    [
        "(unknown http error)",
        "announce is currently unavailable",
        "bad gateway",
        "bad request",
        "cannot connect",
        "connection failed",
        "down",
        "forbidden",
        "gateway timeout",
        "host not found",
        "internal server error",
        "it may be down",
        "maintenance",
        "no connection",
        "no data",
        "not implemented",
        "not responding",
        "not working",
        "offline",
        "refused",
        "service unavailable",
        "ssl error",
        "stream truncated",
        "temporarily disabled",
        "timed out",
        "timeout",
        "tracker is down",
        "tracker unavailable",
        "truncated",
        "unable to process your request",
        "unauthorized",
        "unreachable",
        "unresolvable",
        "your request could not be processed, please try again later",
    ];

    private static readonly string[] UnregisteredReasons =
    [
        "i'm sorry dave, i can't do that",
        "infohash not found",
        "nem található",
        "não registrado",
        "not exist",
        "not registered",
        "torrent banned",
        "torrent deleted",
        "torrent does not exist",
        "torrent existiert nicht",
        "torrent has been deleted",
        "torrent has been nuked",
        "torrent has been rejected",
        "torrent introuvable",
        "torrent is not authorized for use on this tracker",
        "torrent is not found",
        "torrent nicht gefunden",
        "torrent not found",
        "unknown torrent",
        "unregistered",
    ];

    private static readonly string[] UnregisteredReasonCodes =
    [
        "complete season uploaded",
        "dead",
        "dupe",
        "grab internal",
        "internal available",
        "nuked",
        "other",
        "pack is available",
        "packs are available",
        "problem with description",
        "problem with file",
        "problem with pack",
        "repack available",
        "retitled",
        "season pack",
        "season pack out",
        "season pack uploaded",
        "specifically banned",
        "trump",
        "trumped",
        "upgraded",
        "uploaded",
    ];

    /// <summary>
    /// Decides whether a tracker message says the torrent is gone or only that the tracker is struggling.
    /// </summary>
    /// <param name="message">Tracker message reported by the download client</param>
    /// <returns><see cref="TrackerHealth.Unregistered"/> or <see cref="TrackerHealth.Inconclusive"/></returns>
    public static TrackerHealth Classify(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return TrackerHealth.Inconclusive;
        }

        string normalized = UrlRegex().Replace(message, string.Empty).ToLowerInvariant().Trim();
        HashSet<string> tokens = Tokenize(normalized);

        // an outage can phrase itself as "not registered", so down wins over unregistered
        if (Matches(Inconclusive, normalized, tokens) || Matches(TrackerDown, normalized, tokens))
        {
            return TrackerHealth.Inconclusive;
        }

        if (Matches(UnregisteredReasons, normalized, tokens))
        {
            return TrackerHealth.Unregistered;
        }

        // a reason code is the whole message or its first ":" segment, so prose like "tracker is dead" is not one
        if (UnregisteredReasonCodes.Contains(LeadingSegment(normalized), StringComparer.Ordinal))
        {
            return TrackerHealth.Unregistered;
        }

        return TrackerHealth.Inconclusive;
    }

    private static string LeadingSegment(string normalized)
    {
        int separator = normalized.IndexOf(':', StringComparison.Ordinal);

        return separator < 0 ? normalized : normalized[..separator].Trim();
    }

    private static bool Matches(string[] patterns, string normalized, HashSet<string> tokens)
    {
        foreach (string pattern in patterns)
        {
            if (pattern.Contains(' '))
            {
                if (normalized.Contains(pattern, StringComparison.Ordinal))
                {
                    return true;
                }

                continue;
            }

            if (tokens.Contains(pattern))
            {
                return true;
            }
        }

        return false;
    }

    private static HashSet<string> Tokenize(string normalized)
    {
        HashSet<string> tokens = new(StringComparer.Ordinal);
        int start = -1;

        for (int i = 0; i < normalized.Length; i++)
        {
            if (char.IsLetterOrDigit(normalized[i]))
            {
                if (start < 0)
                {
                    start = i;
                }

                continue;
            }

            if (start >= 0)
            {
                tokens.Add(normalized[start..i]);
                start = -1;
            }
        }

        if (start >= 0)
        {
            tokens.Add(normalized[start..]);
        }

        return tokens;
    }
}
