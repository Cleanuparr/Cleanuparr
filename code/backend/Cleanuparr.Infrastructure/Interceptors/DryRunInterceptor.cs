using System.Reflection;
using Cleanuparr.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Interceptors;

public class DryRunInterceptor : IDryRunInterceptor
{
    private readonly ILogger<DryRunInterceptor> _logger;
    private readonly DataContext _dataContext;
    
    public DryRunInterceptor(ILogger<DryRunInterceptor> logger, DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }
    
    public void Intercept(Action action)
    {
        MethodInfo methodInfo = action.Method;
        
        var config = _dataContext.GeneralConfigs
            .AsNoTracking()
            .First();
        
        if (config.DryRun)
        {
            _logger.LogInformation("[DRY RUN] skipping method: {name}", methodInfo.Name);
            return;
        }

        action();
    }
    
    public async Task InterceptAsync(Delegate action, params object[] parameters)
    {
        MethodInfo methodInfo = action.Method;
        
        var config = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();
        
        if (config.DryRun)
        {
            _logger.LogInformation("[DRY RUN] skipping method: {name}", methodInfo.Name);
            return;
        }

        object? result = action.DynamicInvoke(parameters);

        if (result is Task task)
        {
            await task;
        }
    }
    
    public async Task<T?> InterceptAsync<T>(Delegate action, params object[] parameters)
    {
        MethodInfo methodInfo = action.Method;
        
        var config = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();
        
        if (config.DryRun)
        {
            _logger.LogInformation("[DRY RUN] skipping method: {name}", methodInfo.Name);
            return default;
        }

        object? result = action.DynamicInvoke(parameters);

        if (result is Task<T?> task)
        {
            return await task;
        }

        return default;
    }
}
