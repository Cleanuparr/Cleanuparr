using System.Collections.Immutable;

namespace Cleanuparr.Infrastructure.Features.Context;

public static class ContextProvider
{
    private static readonly AsyncLocal<ImmutableDictionary<string, object>> _asyncLocalDict = new();

    public static void Set(string key, object value)
    {
        ImmutableDictionary<string, object> currentDict = _asyncLocalDict.Value ?? ImmutableDictionary<string, object>.Empty;
        _asyncLocalDict.Value = currentDict.SetItem(key, value);
    }
    
    public static void Set<T>(T value) where T : class
    {
        string key = typeof(T).Name ?? throw new Exception("Type name is null");
        Set(key, value);
    }

    public static object? Get(string key)
    {
        return _asyncLocalDict.Value?.TryGetValue(key, out object? value) is true ? value : null;
    }
    
    public static T Get<T>(string key) where T : class
    {
        return Get(key) as T ?? throw new Exception($"failed to get \"{key}\" from context");
    }
    
    public static T Get<T>() where T : class
    {
        string key = typeof(T).Name ?? throw new Exception("Type name is null");
        return Get<T>(key);
    }
}
