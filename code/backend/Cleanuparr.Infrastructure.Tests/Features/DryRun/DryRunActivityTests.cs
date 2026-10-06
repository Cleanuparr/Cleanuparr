using Cleanuparr.Infrastructure.Features.DryRun;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.DryRun;

public sealed class DryRunActivityTests
{
    [Fact]
    public async Task EnterAsync_WhilePurgeHoldsTheGate_WaitsForThePurgeToFinish()
    {
        // Arrange
        DryRunActivity activity = new();
        TaskCompletionSource<bool> purgeGate = new();

        Task<bool> purgeTask = activity.RunIfIdleAsync(() => purgeGate.Task);

        // Act
        Task enterTask = activity.EnterAsync();

        // Assert - entry is blocked while the purge holds the gate
        await Task.Delay(50);
        enterTask.IsCompleted.ShouldBeFalse();

        purgeGate.SetResult(true);
        bool ran = await purgeTask;
        await enterTask;

        ran.ShouldBeTrue();
        activity.IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task RequestPurge_CalledTwice_BuffersOneRequest()
    {
        // Arrange
        DryRunActivity activity = new();

        // Act
        activity.RequestPurge();
        activity.RequestPurge();

        // Assert
        await activity.WaitForPurgeRequestAsync(CancellationToken.None);
        activity.WaitForPurgeRequestAsync(CancellationToken.None).IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Exit_LastDryRun_RequestsPurge()
    {
        // Arrange
        DryRunActivity activity = new();
        await activity.EnterAsync();

        // Act
        activity.Exit();

        // Assert
        activity.WaitForPurgeRequestAsync(CancellationToken.None).IsCompleted.ShouldBeTrue();
    }

    [Fact]
    public async Task Exit_OtherDryRunStillActive_RequestsNoPurge()
    {
        // Arrange
        DryRunActivity activity = new();
        await activity.EnterAsync();
        await activity.EnterAsync();

        // Act
        activity.Exit();

        // Assert
        activity.WaitForPurgeRequestAsync(CancellationToken.None).IsCompleted.ShouldBeFalse();
    }
}
