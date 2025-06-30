using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    /// <summary>
    /// ICache
    /// </summary>
    /// <typeparam name="T">Type of object in cache</typeparam>
    public interface ICache<T> : IDisposable
    {
        bool Enabled { get; }
        CacheOptions CacheOptions { get; }

        T Get(string key);
        T Get(string key, string region);

        bool TryGet(string key, out T value);
        bool TryGet(string key, string region, out T value);

        Task<T> GetAsync(string key);
        Task<T> GetAsync(string key, string region);

        T Set(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        T Set(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);

        bool TrySet(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        bool TrySet(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);

        Task<T> SetAsync(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        Task<T> SetAsync(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);

        T SetOrGetExisting(string key, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        T SetOrGetExisting(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);

        Task<T> SetOrGetExistingAsync(string key, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        Task<T> SetOrGetExistingAsync(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        Task<TResult> SetOrGetExistingAsync<TResult>(string key, Func<Task<TResult>> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        Task<TResult> SetOrGetExistingAsync<TResult>(string key, string region, Func<Task<TResult>> valueFactory, CacheItemPolicy cacheItemPolicy = null);

        void Remove(string key);
        void Remove(string key, string region);

        Task RemoveAsync(string key);
        Task RemoveAsync(string key, string region);
    }
}