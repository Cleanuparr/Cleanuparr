namespace Cleanuparr.Infrastructure.Interceptors;

public interface IDryRunInterceptor
{
    void Intercept(Action action);
    
    Task InterceptAsync(Delegate action, params object[] parameters);

    Task<T?> InterceptAsync<T>(Delegate action, params object[] parameters);
}