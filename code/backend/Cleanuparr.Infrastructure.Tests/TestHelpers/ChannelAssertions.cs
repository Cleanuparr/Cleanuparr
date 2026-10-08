using System.Threading.Channels;
using Shouldly;

namespace Cleanuparr.Infrastructure.Tests.TestHelpers;

/// <summary>
/// Assertions for channels used as in-process queues in tests.
/// </summary>
internal static class ChannelAssertions
{
    /// <summary>
    /// Asserts the channel holds exactly one message and returns it.
    /// </summary>
    internal static T ShouldHaveSingle<T>(this Channel<T> channel)
    {
        channel.Reader.Count.ShouldBe(1);
        channel.Reader.TryRead(out T? item).ShouldBeTrue();
        return item!;
    }
}
