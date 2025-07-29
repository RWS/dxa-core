using Sdl.Web.Common.Interfaces;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Sdl.Web.Tridion.Caching
{
    /// <summary>
    /// Key lock cache provider wraps cache access with lock synchronization to prevent race conditions and deadlocks.
    /// </summary>
    public class KeylockCacheProvider : ICacheProvider
    {
        private static readonly ConcurrentDictionary<string, object> _keyLocks = new ConcurrentDictionary<string, object>();
        private readonly ICacheProvider _underlyingCacheProvider;

        [ThreadStatic]
        private static int _reentriesCount;

        public KeylockCacheProvider(ICacheProvider underlyingCacheProvider)
        {
            _underlyingCacheProvider = underlyingCacheProvider ?? throw new ArgumentNullException(nameof(underlyingCacheProvider));
        }

        public void Store<T>(string key, string region, T value, IEnumerable<string> dependencies = null)
        {
            var hash = CalcLockHash(key, region);
            lock (_keyLocks.GetOrAdd(hash, _ => new object()))
            {
                try
                {
                    _underlyingCacheProvider.Store(key, region, value, dependencies);
                }
                finally
                {
                    _keyLocks.TryRemove(hash, out _);
                }
            }
        }

        public bool TryGet<T>(string key, string region, out T value)
            => _underlyingCacheProvider.TryGet(key, region, out value);

        public T GetOrAdd<T>(string key, string region, Func<T> addFunction, IEnumerable<string> dependencies = null)
        {
            if (TryGet<T>(key, region, out T cachedValue))
                return cachedValue;

            var hash = CalcLockHash(key, region);
            var lockObject = _keyLocks.GetOrAdd(hash, _ => new object());

            lock (lockObject)
            {
                try
                {
                    // Double-check after acquiring the lock
                    if (TryGet<T>(key, region, out cachedValue))
                        return cachedValue;

                    Interlocked.Increment(ref _reentriesCount);
                    cachedValue = addFunction();

                    if (cachedValue != null)
                        _underlyingCacheProvider.Store(key, region, cachedValue, dependencies);

                    return cachedValue;
                }
                finally
                {
                    Interlocked.Decrement(ref _reentriesCount);
                    _keyLocks.TryRemove(hash, out _);
                }
            }
        }

        private static string CalcLockHash(string key, string region)
            => $"{region}:{key}:{_reentriesCount}";
    }
}