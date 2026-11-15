using System.Reflection;

namespace Cleanuparr.Infrastructure.QBittorrent.Tests.TestHelpers;

public static class Fixtures
{
    public static string Read(string fileName)
    {
        Assembly assembly = typeof(Fixtures).Assembly;
        using Stream stream = assembly.GetManifestResourceStream($"{assembly.GetName().Name}.data.{fileName}")!;
        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }
}
