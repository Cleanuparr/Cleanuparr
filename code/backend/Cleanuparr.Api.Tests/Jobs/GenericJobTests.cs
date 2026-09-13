using Cleanuparr.Api.Hubs;
using Cleanuparr.Api.Jobs;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Jobs;

public sealed class GenericJobTests : IDisposable
{
    private readonly EventsContext _eventsContext;
    private readonly ServiceProvider _serviceProvider;
    private readonly Seeker _handler = new();

    public GenericJobTests()
    {
        _eventsContext = ConfigControllerTestDataFactory.CreateEventsContext();

        IJobManagementService jobManagementService = Substitute.For<IJobManagementService>();
        jobManagementService.GetJob(Arg.Any<JobType>()).Returns(new JobInfo { JobType = nameof(JobType.Seeker) });

        ServiceCollection services = new();
        services.AddSingleton(_eventsContext);
        services.AddSingleton(Substitute.For<IHubContext<AppHub>>());
        services.AddSingleton(jobManagementService);
        services.AddSingleton(_handler);
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _eventsContext.Dispose();
    }

    private GenericJob<Seeker> BuildJob() => new(
        Substitute.For<ILogger<GenericJob<Seeker>>>(),
        _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        TimeProvider.System);

    [Fact]
    public async Task Execute_StampsTheRunAsCompleted()
    {
        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        JobRun run = await _eventsContext.JobRuns.SingleAsync();
        run.Status.ShouldBe(JobRunStatus.Completed);
        run.CompletedAt.ShouldNotBeNull();
        run.CompletedAt!.Value.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Execute_StampsTheRunWhenTheHandlerThrows()
    {
        _handler.Throw = new InvalidOperationException("handler exploded");

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        JobRun run = await _eventsContext.JobRuns.SingleAsync();
        run.Status.ShouldBe(JobRunStatus.Failed);
        run.CompletedAt!.Value.ShouldBe(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// Named after a <see cref="JobType"/> member because the job parses its type name.
    /// </summary>
    public sealed class Seeker : IHandler
    {
        public Exception? Throw { get; set; }

        public Task ExecuteAsync(CancellationToken cancellationToken = default) =>
            Throw is null ? Task.CompletedTask : Task.FromException(Throw);
    }
}
