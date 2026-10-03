using Cleanuparr.Infrastructure.Features.Context;
using Cleanuparr.Infrastructure.Interceptors;
using Cleanuparr.Infrastructure.Tests.Features.Jobs.TestHelpers;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Cleanuparr.Infrastructure.Tests.Interceptors;

public sealed class DryRunInterceptorTests : IDisposable
{
    private readonly DataContext _dataContext;
    private readonly DryRunInterceptor _interceptor;

    public DryRunInterceptorTests()
    {
        _dataContext = TestDataContextFactory.Create();
        _interceptor = new DryRunInterceptor(Substitute.For<ILogger<DryRunInterceptor>>(), _dataContext);
    }

    public void Dispose()
    {
        _dataContext.Dispose();
    }

    private async Task SetDbDryRun(bool value)
    {
        GeneralConfig config = await _dataContext.GeneralConfigs.SingleAsync();
        config.DryRun = value;
        await _dataContext.SaveChangesAsync();
    }

    [Fact]
    public async Task IsDryRunEnabled_DbTrueNoStickyValue_ReturnsTrue()
    {
        await SetDbDryRun(true);

        bool result = await _interceptor.IsDryRunEnabled();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task IsDryRunEnabled_DbFalseNoStickyValue_ReturnsFalse()
    {
        await SetDbDryRun(false);

        bool result = await _interceptor.IsDryRunEnabled();

        result.ShouldBeFalse();
    }

    [Fact]
    public async Task IsDryRunEnabled_StickyTrueDbFalse_ReturnsTrue()
    {
        await SetDbDryRun(false);
        ContextProvider.SetDryRun(true);

        bool result = await _interceptor.IsDryRunEnabled();

        result.ShouldBeTrue();
    }

    [Fact]
    public async Task InterceptAsync_StickyTrueDbFalse_SkipsTheAction()
    {
        await SetDbDryRun(false);
        ContextProvider.SetDryRun(true);
        bool ran = false;

        await _interceptor.InterceptAsync(() =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Intercept_StickyTrueDbFalse_SkipsTheAction()
    {
        _dataContext.GeneralConfigs.Single().DryRun = false;
        _dataContext.SaveChanges();
        ContextProvider.SetDryRun(true);
        bool ran = false;

        _interceptor.Intercept(() => ran = true);

        ran.ShouldBeFalse();
    }

    [Fact]
    public void Intercept_StickyFalseDbFalse_RunsTheAction()
    {
        _dataContext.GeneralConfigs.Single().DryRun = false;
        _dataContext.SaveChanges();
        bool ran = false;

        _interceptor.Intercept(() => ran = true);

        ran.ShouldBeTrue();
    }
}
