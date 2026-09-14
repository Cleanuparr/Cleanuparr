using System.Reflection;
using Cleanuparr.Infrastructure.Features.Auth;

namespace Cleanuparr.Infrastructure.Tests.Features.Auth;

/// <summary>
/// Empties the static dictionaries OidcAuthService keeps.
/// Residue from one spec otherwise counts toward another's capacity limit.
/// </summary>
internal static class OidcStaticState
{
    public static void Clear()
    {
        ClearDictionary("PendingFlows");
        ClearDictionary("OneTimeCodes");
    }

    private static void ClearDictionary(string fieldName)
    {
        object dictionary = typeof(OidcAuthService)
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static)!
            .GetValue(null)!;

        dictionary.GetType().GetMethod("Clear")!.Invoke(dictionary, null);
    }
}
