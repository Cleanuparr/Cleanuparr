using Cleanuparr.Api.Hubs;
using Cleanuparr.Api.Jobs;
using Cleanuparr.Api.Tests.TestHelpers;
using Cleanuparr.Domain.Enums;
using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Features.DryRun;
using Cleanuparr.Infrastructure.Features.Jobs;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Models;
using Cleanuparr.Infrastructure.Services.Interfaces;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Cleanuparr.Persistence.Models.State;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Quartz;
using Shouldly;
using Xunit;

namespace Cleanuparr.Api.Tests.Jobs;

public sealed class GenericJobTests : IDisposable
{
    private readonly EventsContext _eventsContext;
    private readonly DataContext _dataContext;
    private readonly IDryRunPurger _dryRunPurger;
    private readonly ServiceProvider _serviceProvider;
    private readonly Seeker _handler = new();

    public GenericJobTests()
    {
        _eventsContext = ConfigControllerTestDataFactory.CreateEventsContext();
        _dataContext = ConfigControllerTestDataFactory.CreateDataContext();
        _dryRunPurger = Substitute.For<IDryRunPurger>();

        IJobManagementService jobManagementService = Substitute.For<IJobManagementService>();
        jobManagementService.GetJob(Arg.Any<JobType>()).Returns(new JobInfo { JobType = nameof(JobType.Seeker) });

        ServiceCollection services = new();
        services.AddSingleton(_eventsContext);
        services.AddSingleton(Substitute.For<IHubContext<AppHub>>());
        services.AddSingleton(jobManagementService);
        services.AddSingleton(_handler);
        services.AddSingleton(_dryRunPurger);
        services.AddScoped<IDryRunInterceptor>(_ =>
            new DryRunInterceptor(Substitute.For<ILogger<DryRunInterceptor>>(), _dataContext));
        _serviceProvider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
        _eventsContext.Dispose();
        _dataContext.Dispose();
    }

    private static readonly DateTimeOffset Now = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    private GenericJob<Seeker> BuildJob() => new(
        Substitute.For<ILogger<GenericJob<Seeker>>>(),
        _serviceProvider.GetRequiredService<IServiceScopeFactory>(),
        new FakeTimeProvider(Now));

    [Fact]
    public async Task Execute_StampsTheRunAsCompleted()
    {
        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        JobRun run = await _eventsContext.JobRuns.SingleAsync();
        run.Status.ShouldBe(JobRunStatus.Completed);
        run.CompletedAt.ShouldNotBeNull();
        run.CompletedAt!.Value.ShouldBe(Now);
    }

    [Fact]
    public async Task Execute_StampsTheRunWhenTheHandlerThrows()
    {
        _handler.Throw = new InvalidOperationException("handler exploded");

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        JobRun run = await _eventsContext.JobRuns.SingleAsync();
        run.Status.ShouldBe(JobRunStatus.Failed);
        run.CompletedAt!.Value.ShouldBe(Now);
    }

    [Fact]
    public async Task Execute_CapturesDryRunOnceForTheWholeRun()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = true;
        await _dataContext.SaveChangesAsync();

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        _handler.ObservedDryRun.ShouldBe(true);
    }

    [Fact]
    public async Task Execute_DoesNotStickWhenTheRunStartedLive()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = false;
        await _dataContext.SaveChangesAsync();

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        _handler.ObservedDryRun.ShouldBe(false);
    }

    [Fact]
    public async Task Execute_WhenRunStartedDry_PurgesAfterFinishing()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = true;
        await _dataContext.SaveChangesAsync();

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        await _dryRunPurger.Received(1).PurgeIfDryRunOffAsync();
    }

    [Fact]
    public async Task Execute_WhenRunStartedLive_DoesNotPurge()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = false;
        await _dataContext.SaveChangesAsync();

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        await _dryRunPurger.DidNotReceive().PurgeIfDryRunOffAsync();
    }

    [Fact]
    public async Task Execute_WhenPurgeThrows_StillStampsTheRunAsCompleted()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = true;
        await _dataContext.SaveChangesAsync();
        _dryRunPurger.PurgeIfDryRunOffAsync().ThrowsAsync(new Exception("purge failed"));

        await BuildJob().Execute(Substitute.For<IJobExecutionContext>());

        JobRun run = await _eventsContext.JobRuns.SingleAsync();
        run.Status.ShouldBe(JobRunStatus.Completed);
    }

    /// <summary>
    /// Named after a <see cref="JobType"/> member because the job parses its type name.
    /// </summary>
    public sealed class Seeker : IHandler
    {
        public Exception? Throw { get; set; }

        public bool ObservedDryRun { get; private set; }

        public Task ExecuteAsync(CancellationToken cancellationToken = default)
        {
            ObservedDryRun = ContextProvider.IsDryRunSticky();
            return Throw is null ? Task.CompletedTask : Task.FromException(Throw);
        }
    }
}
