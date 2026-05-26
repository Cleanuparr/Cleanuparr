namespace Cleanuparr.Infrastructure.Interceptors;

public interface IDryRunInterceptor
{
    void Intercept(Action action);

    void Intercept(Delegate action, params object[] parameters);

    Task InterceptAsync(Func<Task> action);

    Task InterceptAsync(Delegate action, params object[] parameters);

    Task<T?> InterceptAsync<T>(Func<Task<T?>> action);

    Task<T?> InterceptAsync<T>(Delegate action, params object[] parameters);

    Task<bool> IsDryRunEnabled();
}
