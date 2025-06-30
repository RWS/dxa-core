using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tridion.Dxa.Framework.Core;

namespace Tridion.Dxa.Framework.Caching
{
    public interface ICacheHandlerSerializer<T>
    {
        ICacheSerializer<T> Serializer { get; set; }
    }

    /// <summary>
    /// AbstractCache
    /// </summary>
    /// <typeparam name="T">Type used in cache</typeparam>
    public abstract class AbstractCacheHandler<T> : ICache<T>, ICacheHandlerSerializer<T>, ICacheHandler
    {
        private static readonly string Keyprefix = "::CILCACHE::";
        private DateTime? _disableUntilTime;

        protected AbstractCacheHandler(ICacheSerializer<T> cacheSerializer, CacheHandlerOptions cacheOptions)
        {
            Serializer = cacheSerializer;
            CacheHandlerOptions = cacheOptions;
            _disableUntilTime = null;
        }

        private string HashKey(string key) => CacheHandlerOptions.HashKey ? Murmur3.Hash(key).ToString() : key;

        public virtual string GetCacheKey(string key, string region = null)
        {
            return string.IsNullOrEmpty(region) ? Keyprefix + HashKey(key) : Keyprefix + region + ":" + HashKey(key);
        }

        public ICacheSerializer<T> Serializer { get; set; }

        public CacheOptions CacheOptions => CacheHandlerOptions.GlobalCacheOptions;

        protected CacheHandlerOptions CacheHandlerOptions { get; }

        protected void TemporaryDisable() => _disableUntilTime = DateTime.Now.Add(CacheOptions.CacheDisableTime);

        public bool Enabled
        {
            get
            {
                if (_disableUntilTime == null)
                    return true;

                if (DateTime.Now < _disableUntilTime.Value) return false;
                _disableUntilTime = null;
                return true;
            }
        }

        public abstract T Get(string key);
        public abstract T Get(string key, string region);
        public abstract bool TryGet(string key, out T value);
        public abstract bool TryGet(string key, string region, out T value);
        public abstract Task<T> GetAsync(string key);
        public abstract Task<T> GetAsync(string key, string region);
        public abstract T Set(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        public abstract T Set(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);
        public abstract bool TrySet(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        public abstract bool TrySet(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<T> SetAsync(string key, T value, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<T> SetAsync(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null);
        public abstract T SetOrGetExisting(string key, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract T SetOrGetExisting(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<T> SetOrGetExistingAsync(string key, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<T> SetOrGetExistingAsync(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<TResult> SetOrGetExistingAsync<TResult>(string key, Func<Task<TResult>> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract Task<TResult> SetOrGetExistingAsync<TResult>(string key, string region, Func<Task<TResult>> valueFactory, CacheItemPolicy cacheItemPolicy = null);
        public abstract void Remove(string key);
        public abstract void Remove(string key, string region);
        public abstract Task RemoveAsync(string key);
        public abstract Task RemoveAsync(string key, string region);
        public abstract void Dispose();
    }
}
