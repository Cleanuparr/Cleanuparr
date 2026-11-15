using System;
using System.Threading;
using System.Threading.Tasks;

namespace QBittorrent.Client.Internal
{
    internal class Cached<T>
    {
        private readonly Func<CancellationToken, Task<T>> _factory;
        private Task<T> _value;

        public Cached(Func<CancellationToken, Task<T>> factory)
        {
            _factory = factory;
        }

        public Task<T> GetValueAsync(CancellationToken token)
        {
            var task = Interlocked.CompareExchange(ref _value, null, null);
            if (task == null)
            {
                task = _factory(token);
                task.ContinueWith(CacheValue, TaskContinuationOptions.OnlyOnRanToCompletion);
            }
            return task;
        }

        private void CacheValue(Task<T> task)
        {
            Interlocked.CompareExchange(ref _value, task, null);
        }
    }
}
