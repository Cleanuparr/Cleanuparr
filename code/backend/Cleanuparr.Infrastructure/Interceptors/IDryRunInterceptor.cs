namespace Cleanuparr.Infrastructure.Interceptors;

public interface IDryRunInterceptor
{
    void Intercept(Delegate action, params object[] parameters);

    Task InterceptAsync(Delegate action, params object[] parameters);

    Task<T?> InterceptAsync<T>(Delegate action, params object[] parameters);

    Task<bool> IsDryRunEnabled();
}
