using System.Threading.Channels;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.Messaging;
using Cleanuparr.Infrastructure.Tests.TestHelpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Features.Messaging;

public class QueueWorkerTests : IAsyncDisposable
{
    private const string ContextKey = "QueueWorkerTests.Value";

    private readonly Channel<TestMessage> _channel = Channel.CreateUnbounded<TestMessage>();
    private readonly Recorder _recorder = new();
    private readonly ServiceProvider _provider;
    private readonly ILogger<QueueWorker<TestMessage>> _logger;
    private readonly QueueWorker<TestMessage> _worker;

    public QueueWorkerTests()
    {
        ServiceCollection services = new();
        services.AddSingleton(_recorder);
        services.AddScoped<ScopedMarker>();
        services.AddScoped<IMessageHandler<TestMessage>, TestHandler>();
        _provider = services.BuildServiceProvider();

        _logger = Substitute.For<ILogger<QueueWorker<TestMessage>>>();
        _worker = new QueueWorker<TestMessage>(_logger, _provider.GetRequiredService<IServiceScopeFactory>(), _channel);
    }

    public async ValueTask DisposeAsync()
    {
        await _worker.StopAsync(CancellationToken.None);
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task ProcessesMessages_InFifoOrder()
    {
        await _worker.StartAsync(CancellationToken.None);

        await _channel.Writer.WriteAsync(new TestMessage(1));
        await _channel.Writer.WriteAsync(new TestMessage(2));
        await _channel.Writer.WriteAsync(new TestMessage(3));

        await WaitUntilAsync(() => _recorder.HandledOrder.Count == 3);

        _recorder.HandledOrder.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task NeverRunsTwoHandlersAtOnce()
    {
        _recorder.Gate = new TaskCompletionSource();
        await _worker.StartAsync(CancellationToken.None);

        await _channel.Writer.WriteAsync(new TestMessage(1));
        await _channel.Writer.WriteAsync(new TestMessage(2));

        await WaitUntilAsync(() => Volatile.Read(ref _recorder.Concurrent) == 1);
        // The second message must still be waiting behind the first.
        _recorder.HandledOrder.ShouldBeEmpty();

        _recorder.Gate.SetResult();
        await WaitUntilAsync(() => _recorder.HandledOrder.Count == 2);

        _recorder.MaxConcurrent.ShouldBe(1);
    }

    [Fact]
    public async Task KeepsRunning_AfterAHandlerThrows_AndLogsTheError()
    {
        _recorder.ThrowIds.Add(1);
        await _worker.StartAsync(CancellationToken.None);

        await _channel.Writer.WriteAsync(new TestMessage(1));
        await _channel.Writer.WriteAsync(new TestMessage(2));

        await WaitUntilAsync(() => _recorder.HandledOrder.Count == 2);

        _logger.HasLogContainingAtLeastOnce(LogLevel.Error, "failed to handle queued message").ShouldBeTrue();
    }

    [Fact]
    public async Task CreatesANewScopePerMessage()
    {
        await _worker.StartAsync(CancellationToken.None);

        await _channel.Writer.WriteAsync(new TestMessage(1));
        await _channel.Writer.WriteAsync(new TestMessage(2));

        await WaitUntilAsync(() => _recorder.ScopeIds.Count == 2);

        _recorder.ScopeIds[0].ShouldNotBe(_recorder.ScopeIds[1]);
    }

    [Fact]
    public async Task DoesNotLeak_ContextProviderValues_BetweenMessages()
    {
        await _worker.StartAsync(CancellationToken.None);

        await _channel.Writer.WriteAsync(new TestMessage(1));
        await _channel.Writer.WriteAsync(new TestMessage(2));

        await WaitUntilAsync(() => _recorder.HandledOrder.Count == 2);

        // Neither message should observe the value the previous message set.
        _recorder.ObservedContextAtStart.ShouldAllBe(value => value == null);
    }

    [Fact]
    public async Task Stops_WhenTheStoppingTokenCancels()
    {
        await _worker.StartAsync(CancellationToken.None);

        await Should.NotThrowAsync(() => _worker.StopAsync(CancellationToken.None));
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(5);

        while (!condition() && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        condition().ShouldBeTrue("condition was not met within the timeout");
    }

    public sealed record TestMessage(int Id);

    private sealed class ScopedMarker
    {
        public readonly Guid Id = Guid.NewGuid();
    }

    private sealed class Recorder
    {
        public readonly List<int> HandledOrder = [];
        public readonly List<object?> ObservedContextAtStart = [];
        public readonly List<Guid> ScopeIds = [];
        public readonly HashSet<int> ThrowIds = [];
        public TaskCompletionSource? Gate;
        public int Concurrent;
        public int MaxConcurrent;
    }

    private sealed class TestHandler : IMessageHandler<TestMessage>
    {
        private readonly Recorder _recorder;
        private readonly ScopedMarker _marker;

        public TestHandler(Recorder recorder, ScopedMarker marker)
        {
            _recorder = recorder;
            _marker = marker;
        }

        public async Task HandleAsync(TestMessage message)
        {
            _recorder.ObservedContextAtStart.Add(ContextProvider.Get(ContextKey));
            ContextProvider.Set(ContextKey, message.Id);

            int concurrent = Interlocked.Increment(ref _recorder.Concurrent);
            _recorder.MaxConcurrent = Math.Max(_recorder.MaxConcurrent, concurrent);

            if (_recorder.Gate is not null)
            {
                await _recorder.Gate.Task;
            }

            _recorder.ScopeIds.Add(_marker.Id);
            _recorder.HandledOrder.Add(message.Id);

            Interlocked.Decrement(ref _recorder.Concurrent);

            if (_recorder.ThrowIds.Contains(message.Id))
            {
                throw new InvalidOperationException($"boom {message.Id}");
            }
        }
    }
}
