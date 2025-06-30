using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace Tridion.Dxa.Framework.Caching.CacheHandlers.RedisCache
{
    /// <summary>
    /// RedisCacheProvider
    /// </summary>
    /// <typeparam name="T">Type held by cache</typeparam>
    //public class RedisCacheHandler<T> : AbstractCacheHandler<T>
    //{
    //    private readonly RedisCache _cache;
    //    private readonly RedisCacheHandlerOptions _options;

    //    internal RedisCacheHandler(ICacheSerializer<T> cacheSerializer, RedisCacheHandlerOptions cacheOptions)
    //        : base(cacheSerializer, cacheOptions)
    //    {
    //        _options = cacheOptions;
    //        _cache =
    //            new RedisCache(new RedisCacheOptions
    //            {
    //                Configuration = cacheOptions.Hostname + ":" + cacheOptions.Port,
    //                InstanceName = cacheOptions.InstanceName ?? string.Empty
    //            });
    //    }

    //    protected RedisCacheHandlerOptions Options => CacheHandlerOptions as RedisCacheHandlerOptions;

    //    protected byte[] SerializeCacheData(T cacheData)
    //        => Serializer.Serialize(cacheData, CacheHandlerOptions.Compression, CacheHandlerOptions.CompressionLimit);

    //    protected T DeserializeCacheData(byte[] data) => Serializer.Deserialize(data);

    //    protected Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions CreatePolicy(string region,
    //        CacheItemPolicy cacheOptions)
    //    {
    //        while (true)
    //        {
    //            Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions options =
    //                new Microsoft.Extensions.Caching.Distributed.DistributedCacheEntryOptions();

    //            if (cacheOptions != null)
    //            {
    //                if (cacheOptions.AbsoluteExpiration != null)
    //                {
    //                    options.AbsoluteExpiration = cacheOptions.AbsoluteExpirationFromNow.Value;
    //                }
    //                else if (cacheOptions.SlidingExpiration != null)
    //                {
    //                    options.SlidingExpiration = cacheOptions.SlidingExpiration;
    //                }
    //            }
    //            else if (!string.IsNullOrEmpty(region) &&
    //                     CacheHandlerOptions.GlobalCacheOptions.Regions.ContainsKey(region))
    //            {
    //                var region1 = region;
    //                region = null;
    //                cacheOptions = CacheHandlerOptions.GlobalCacheOptions.Regions[region1];
    //                continue;
    //            }
    //            else if (CacheHandlerOptions.GlobalCacheItemPolicy != null)
    //            {
    //                region = null;
    //                cacheOptions = CacheHandlerOptions.GlobalCacheItemPolicy;
    //                continue;
    //            }

    //            return options;
    //        }
    //    }

    //    #region Retry

    //    private void RetryBlock(Action block)
    //    {
    //        bool tmp;
    //        RetryBlock(new Func<bool>(() =>
    //        {
    //            block();
    //            return true;
    //        }), out tmp);
    //    }

    //    private bool RetryBlock<TResult>(Func<TResult> block, out TResult returnedValue)
    //    {
    //        int retries = 0;
    //        int n = _options.GlobalCacheOptions.CacheRetries;
    //        while (Enabled && retries < n)
    //        {
    //            retries++;
    //            try
    //            {
    //                returnedValue = block();
    //                return true;
    //            }
    //            catch (RedisConnectionException ce)
    //            {
    //                if (retries < n) continue;
    //                TemporaryDisable();
    //                //Logger.Error($"Redis Connection Error '{Options.Hostname}:{Options.Port}' due to {ce}.");
    //            }
    //            catch (TimeoutException te)
    //            {
    //                if (retries < n) continue;
    //                TemporaryDisable();
    //                //Logger.Error($"Redis Timeout '{Options.Hostname}:{Options.Port}' due to {te}.");
    //            }
    //            catch (Exception e)
    //            {
    //                TemporaryDisable();
    //                //Logger.Error($"Redis Error '{Options.Hostname}:{Options.Port}' due to {e}.");
    //            }
    //        }
    //        returnedValue = default;
    //        return false;
    //    }

    //    #endregion

    //    public override T Get(string key) => Get(key, null);

    //    public override T Get(string key, string region)
    //    {
    //        T value;
    //        TryGet(key, region, out value);
    //        return value;
    //    }

    //    public override bool TryGet(string key, out T value) => TryGet(key, null, out value);

    //    public override bool TryGet(string key, string region, out T value)
    //    {
    //        if (Enabled)
    //            return RetryBlock(() =>
    //            {
    //                byte[] data = _cache.Get(GetCacheKey(key, region));
    //                return DeserializeCacheData(data);
    //            }, out value);
    //        value = default;
    //        return false;
    //    }

    //    public override async Task<T> GetAsync(string key)
    //        => await GetAsync(key, null);

    //    public override async Task<T> GetAsync(string key, string region)
    //        => await Task.Factory.StartNew(() => Get(key, region));

    //    public override T Set(string key, T value, CacheItemPolicy cacheItemPolicy = null)
    //        => Set(key, value, null, cacheItemPolicy);

    //    public override T Set(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null)
    //    {
    //        TrySet(key, value, region, cacheItemPolicy);
    //        return value;
    //    }

    //    public override bool TrySet(string key, T value, CacheItemPolicy cacheItemPolicy = null)
    //        => TrySet(key, value, null, cacheItemPolicy);

    //    public override bool TrySet(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null)
    //    {
    //        if (!Enabled || value == null) return false;
    //        bool tmp;
    //        return RetryBlock(() =>
    //        {
    //            byte[] data = SerializeCacheData(value);
    //            if (data == null) return false;
    //            var policy = CreatePolicy(region, cacheItemPolicy);
    //            _cache.Set(GetCacheKey(key, region), data, policy);
    //            return true;
    //        }, out tmp) || tmp;
    //    }

    //    public override async Task<T> SetAsync(string key, T value, CacheItemPolicy cacheItemPolicy = null)
    //        => await SetAsync(key, value, null, cacheItemPolicy);

    //    public override async Task<T> SetAsync(string key, T value, string region, CacheItemPolicy cacheItemPolicy = null)
    //        => await Task.Factory.StartNew(() => Set(key, value, region, cacheItemPolicy));

    //    public override T SetOrGetExisting(string key, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null)
    //        => SetOrGetExisting(key, null, valueFactory, cacheItemPolicy);

    //    public override T SetOrGetExisting(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null)
    //    {
    //        if (!Enabled) return valueFactory();

    //        var result = Get(key, region);
    //        if (result != null) return result;
    //        return Set(key, valueFactory(), region, cacheItemPolicy);
    //    }

    //    public override async Task<T> SetOrGetExistingAsync(string key, Func<T> valueFactory,
    //        CacheItemPolicy cacheItemPolicy = null)
    //        => await SetOrGetExistingAsync(key, null, valueFactory, cacheItemPolicy);

    //    public override async Task<T> SetOrGetExistingAsync(string key, string region, Func<T> valueFactory, CacheItemPolicy cacheItemPolicy = null)
    //        => await Task.Factory.StartNew(() => SetOrGetExisting(key, region, valueFactory, cacheItemPolicy));

    //    public override void Remove(string key) => Remove(key, null);

    //    public override void Remove(string key, string region)
    //    {
    //        RetryBlock(() => _cache.Remove(GetCacheKey(key, region)));
    //    }

    //    public override async Task RemoveAsync(string key)
    //        => await RemoveAsync(key, null);

    //    public override async Task RemoveAsync(string key, string region)
    //       => await Task.Factory.StartNew(() => Remove(key, region));

    //    public override void Dispose() => _cache.Dispose();

    //    public override Task<TResult> SetOrGetExistingAsync<TResult>(string key, Func<Task<TResult>> valueFactory,
    //        CacheItemPolicy cacheItemPolicy = null)
    //        => SetOrGetExistingAsync(key, null, valueFactory, cacheItemPolicy);

    //    public override Task<TResult> SetOrGetExistingAsync<TResult>(string key, string region, Func<Task<TResult>> valueFactory, CacheItemPolicy cacheItemPolicy = null)
    //    {
    //        throw new NotImplementedException();
    //    }
//    }
}
