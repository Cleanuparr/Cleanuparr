using Cleanuparr.Infrastructure.Logging;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Logging;

public sealed class ArchiveHooksTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), $"cleanuparr-archive-{Guid.NewGuid():N}");

    public ArchiveHooksTests()
    {
        Directory.CreateDirectory(_folder);
    }

    public void Dispose()
    {
        if (Directory.Exists(_folder))
        {
            Directory.Delete(_folder, recursive: true);
        }
    }

    private string WriteLog(string name, DateTimeOffset lastWritten)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, "log line");
        File.SetLastWriteTimeUtc(path, lastWritten.UtcDateTime);

        return path;
    }

    [Fact]
    public void OnFileDeleting_DropsAnArchiveOlderThanTheTimeLimit()
    {
        string path = WriteLog("stale.log", DateTimeOffset.UtcNow.AddDays(-2));
        ArchiveHooks hooks = new(retainedFileCountLimit: 0, retainedFileTimeLimit: TimeSpan.FromHours(1), timeProvider: TimeProvider.System);

        hooks.OnFileDeleting(path);

        Directory.GetFiles(_folder, "*.gz").ShouldBeEmpty();
    }

    [Fact]
    public void OnFileDeleting_KeepsAnArchiveInsideTheTimeLimit()
    {
        string path = WriteLog("fresh.log", DateTimeOffset.UtcNow.AddMinutes(-5));
        ArchiveHooks hooks = new(retainedFileCountLimit: 0, retainedFileTimeLimit: TimeSpan.FromHours(1), timeProvider: TimeProvider.System);

        hooks.OnFileDeleting(path);

        Directory.GetFiles(_folder, "*.gz").Length.ShouldBe(1);
    }
}
