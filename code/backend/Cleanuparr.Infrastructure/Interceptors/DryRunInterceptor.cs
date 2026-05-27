using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Cleanuparr.Persistence;
using Cleanuparr.Persistence.Models.Configuration.General;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Cleanuparr.Infrastructure.Interceptors;

public partial class DryRunInterceptor : IDryRunInterceptor
{
    private readonly ILogger<DryRunInterceptor> _logger;
    private readonly DataContext _dataContext;

    [GeneratedRegex(@"(\w+)\s*\(")]
    private static partial Regex MethodNameRegex();

    public DryRunInterceptor(ILogger<DryRunInterceptor> logger, DataContext dataContext)
    {
        _logger = logger;
        _dataContext = dataContext;
    }

    public void Intercept(
        Action action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
    {
        if (IsDryRun(expression))
        {
            return;
        }

        action();
    }

    public async Task InterceptAsync(
        Func<Task> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
    {
        if (await IsDryRunAsync(expression))
        {
            return;
        }

        await action();
    }

    public async Task<T?> InterceptAsync<T>(
        Func<Task<T>> action,
        [CallerArgumentExpression(nameof(action))] string? expression = null)
    {
        if (await IsDryRunAsync(expression))
        {
            return default;
        }

        return await action();
    }

    public async Task<bool> IsDryRunEnabled()
    {
        GeneralConfig config = await _dataContext.GeneralConfigs
            .AsNoTracking()
            .FirstAsync();

        return config.DryRun;
    }

    private bool IsDryRun(string? expression)
    {
        GeneralConfig config = _dataContext.GeneralConfigs
            .AsNoTracking()
            .First();

        if (!config.DryRun)
        {
            return false;
        }

        _logger.LogInformation("[DRY RUN] skipping method: {name}", ExtractMethodName(expression));
        return true;
    }

    private async Task<bool> IsDryRunAsync(string? expression)
    {
        if (!await IsDryRunEnabled())
        {
            return false;
        }

        _logger.LogInformation("[DRY RUN] skipping method: {name}", ExtractMethodName(expression));
        return true;
    }

    private static string ExtractMethodName(string? expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return "unknown";
        }

        Match match = MethodNameRegex().Match(expression);
        return match.Success ? match.Groups[1].Value : expression;
    }
}
