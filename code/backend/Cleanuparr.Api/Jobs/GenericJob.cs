using Cleanuparr.Infrastructure.Features.Jobs;
using Quartz;
using Serilog.Context;

namespace Cleanuparr.Api.Jobs;

[DisallowConcurrentExecution]
public sealed class GenericJob<T> : IJob
    where T : IHandler
{
    private readonly ILogger<GenericJob<T>> _logger;
    private readonly IServiceScopeFactory _scopeFactory;
    
    public GenericJob(ILogger<GenericJob<T>> logger, IServiceScopeFactory scopeFactory)
    {
        _logger = logger;
        _scopeFactory = scopeFactory;
    }
    
    public async Task Execute(IJobExecutionContext context)
    {
        using var _ = LogContext.PushProperty("JobName", typeof(T).Name);
        
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var handler = scope.ServiceProvider.GetRequiredService<T>();
            await handler.ExecuteAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{name} failed", typeof(T).Name);
        }
    }
}