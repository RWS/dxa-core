using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.Xml;
using Tridion.Dxa.Framework.Core.JSON.NET;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Sdl.Web.Common.Interfaces;
using Sdl.Web.Common.Logging;


namespace Sdl.Web.Tridion.Caching
{
    /// <summary>
    /// Default Cache Provider implementation based on CIL caching.
    /// </summary>
    public class DefaultCacheProvider : ICacheProvider
    {
        //private readonly ICacheProvider _cacheProvider;
        private readonly IMemoryCache _memoryCache;
        private readonly IDistributedCache _distributedCache;
        private IConfiguration _configuration;
        private static readonly long DefaultSlidingExpirationTimeLegacy = (long)TimeSpan.FromMinutes(5).TotalSeconds;

        public DefaultCacheProvider(IMemoryCache memoryCache, IDistributedCache distributedCache, IConfiguration configuration)
        {
            _memoryCache = memoryCache;
            _distributedCache = distributedCache;
            _configuration = configuration;
        }

        /// <summary>
        /// IsCachingEnabled It checks the caching is enabled or not
        /// </summary>
        /// <returns>bool</returns>
        private bool IsCachingEnabled()
        {
            bool cachingEnabled = _configuration.GetValue<bool>("SdlWebDelivery:Caching:Enabled");
            return cachingEnabled;
        }

        #region ICacheProvider members
        /// <summary>
        /// Stores a given key/value pair to a given cache Region.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="region">The name of the cache region. Different cache regions can have different retention policies.</param>
        /// <param name="value">The value. If <c>null</c>, this effectively removes the key from the cache.</param>
        /// <param name="dependencies">An optional set of dependent item IDs. Can be used to invalidate the cached item.</param>
        /// <typeparam name="T">The type of the value to add.</typeparam>
        public void Store<T>(string key, string region, T value, IEnumerable<string> dependencies = null)
        {
            if (!IsCachingEnabled()) return;

            var cacheHandler = GetCacheHandler(region);

            if (cacheHandler == "RedisCacheHandler")
            {
                StoreInRedis(key, region, value);
            }
            else if (cacheHandler == "DefaultMemCacheHandler")
            {
                StoreInMemory(key, value, region);
            }
        }


        /// <summary>
        /// Tries to get a cached value for a given key and cache region.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="region">The name of the cache region. Different cache regions can have different retention policies.</param>
        /// <param name="value">The cached value (output).</param>
        /// <typeparam name="T">The type of the value to get.</typeparam>
        /// <returns><c>true</c> if a cached value was found for the given key and cache region.</returns>
        public bool TryGet<T>(string key, string region, out T value)
        {
            value = default; // Initialize the out parameter with a default value

            // Check if caching is enabled
            if (!IsCachingEnabled())
            {
                return false; // Caching is disabled, return false (cache miss)
            }

            var cacheHandler = GetCacheHandler(region);

            if (cacheHandler == "RedisCacheHandler")
            {
                return TryGetFromRedis(key, out value);
            }
            else if (cacheHandler == "DefaultMemCacheHandler")
            {
                return _memoryCache.TryGetValue(key, out value);
            }

            return false; // Cache miss
        }

        /// <summary>
        /// Tries to get a cached value for a given key and cache region adding a new value if it didn't already exist.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="region"></param>
        /// <param name="addFunction"></param>
        /// <param name="dependencies"></param>
        /// <returns></returns>
        public T GetOrAdd<T>(string key, string region, Func<T> addFunction, IEnumerable<string> dependencies = null)
        {
            // If caching is disabled, return the value without storing it
            if (!IsCachingEnabled())
            {
                return addFunction();
            }

            // Determine the cache handler type for the given region
            string cacheHandlerType = GetCacheHandler(region);

            if (cacheHandlerType == "RedisCacheHandler")
            {
                // Try to retrieve the item from Redis cache
                if (TryGetFromRedis(key, out T cachedItem))
                {
                    return cachedItem; // Cache hit in Redis
                }
            }
            else if (cacheHandlerType == "DefaultMemCacheHandler")
            {
                // Try to retrieve the item from memory cache
                if (_memoryCache.TryGetValue(key, out T cachedItem))
                {
                    return cachedItem; // Cache hit in Memory
                }
            }

            // Cache miss, generate the item using the provided function
            T item = addFunction();

            if (item != null)
            {
                // Store the item in the appropriate cache based on the handler type
                if (cacheHandlerType == "RedisCacheHandler")
                {
                    StoreInRedis(key, region, item);
                }
                else if (cacheHandlerType == "DefaultMemCacheHandler")
                {
                    StoreInMemory(key, item, region);
                }
                else
                {
                    Log.Debug($"Unsupported cache handler type: {cacheHandlerType}");
                }
            }

            return item;
        }

        private string GetCacheHandler(string region)
        {
            var regionsSection = _configuration.GetSection("SdlWebDelivery:Caching:Regions");
            var regionConfig = regionsSection.GetChildren().FirstOrDefault(x => x.Key == region);

            if (regionConfig != null && regionConfig["CacheName"] != null)
            {
                return _configuration.GetValue<string>($"SdlWebDelivery:Caching:Handlers:{regionConfig["CacheName"]}:Type");
            }

            string defaultHandlerCache = _configuration.GetValue<string>("SdlWebDelivery:Caching:DefaultHandler");

            return _configuration.GetValue<string>($"SdlWebDelivery:Caching:Handlers:{defaultHandlerCache}:Type");
        }

        private string GetCacheName(string region)
        {
            var regionsSection = _configuration.GetSection("SdlWebDelivery:Caching:Regions");
            var regionConfig = regionsSection.GetChildren().FirstOrDefault(x => x.Key == region);

            if (regionConfig != null && regionConfig["CacheName"] != null)
            {
                return regionConfig["CacheName"];
            }

            return _configuration.GetValue<string>("SdlWebDelivery:Caching:DefaultHandler");
        }

        private void StoreInRedis<T>(string key, string region, T value)
        {
            var cacheName = GetCacheName(region);
            var regionCacheHandlerPolicy = _configuration.GetSection($"SdlWebDelivery:Caching:Handlers:{cacheName}:Policy");
            var useSlidingExpiration = regionCacheHandlerPolicy.GetSection("UseSlidingExpiration").Exists();

            var distributedCacheoptions = new DistributedCacheEntryOptions();

            if (useSlidingExpiration)
            {

                TimeSpan? slidingExpiration = null;

                long t = regionCacheHandlerPolicy.GetValue<long>("SlidingExpiration");

                if (t == -1)
                    t = DefaultSlidingExpirationTimeLegacy;

                slidingExpiration = TimeSpan.FromSeconds(t);
                distributedCacheoptions.SlidingExpiration = slidingExpiration;
            }
            else
            {
                long t = regionCacheHandlerPolicy.GetValue<long>("AbsoluteExpiration");

                TimeSpan? absoluteExpiration = null;

                absoluteExpiration = TimeSpan.FromSeconds(t);

                DateTimeOffset? AbsoluteExpirationFromNow = absoluteExpiration.HasValue ? new DateTimeOffset(DateTime.UtcNow.AddSeconds(absoluteExpiration.Value.TotalSeconds)) : new DateTimeOffset(DateTime.UtcNow.AddSeconds(300));
                distributedCacheoptions.AbsoluteExpiration = AbsoluteExpirationFromNow;
            }

            string format = DetermineFormat<T>();
            var serializedValue = SerializeData<T>(value, format, true);
            //var serializedValue = SerializeCacheData<T>(value);
            _distributedCache.SetString(key, serializedValue, distributedCacheoptions);
        }

        private void StoreInMemory<T>(string key, T value, string region)
        {
            var cacheName = GetCacheName(region);
            var regionCacheHandlerPolicy = _configuration.GetSection($"SdlWebDelivery:Caching:Handlers:{cacheName}:Policy");
            var useSlidingExpiration = regionCacheHandlerPolicy.GetSection("UseSlidingExpiration").Exists();

            var cacheEntryOptions = new MemoryCacheEntryOptions();

            if (useSlidingExpiration)
            {

                TimeSpan? slidingExpiration = null;

                long t = regionCacheHandlerPolicy.GetValue<long>("SlidingExpiration");

                if (t == -1)
                    t = DefaultSlidingExpirationTimeLegacy;

                slidingExpiration = TimeSpan.FromSeconds(t);
                cacheEntryOptions.SlidingExpiration = slidingExpiration;
            }
            else
            {
                long t = regionCacheHandlerPolicy.GetValue<long>("AbsoluteExpiration");

                TimeSpan? absoluteExpiration = null;

                absoluteExpiration = TimeSpan.FromSeconds(t);

                DateTimeOffset? AbsoluteExpirationFromNow = absoluteExpiration.HasValue ? new DateTimeOffset(DateTime.UtcNow.AddSeconds(absoluteExpiration.Value.TotalSeconds)) : new DateTimeOffset(DateTime.UtcNow.AddSeconds(300));
                cacheEntryOptions.AbsoluteExpiration = AbsoluteExpirationFromNow;
            }

            _memoryCache.Set(key, value, cacheEntryOptions);
        }

        private bool TryGetFromRedis<T>(string key, out T value)
        {
            var cachedData = _distributedCache.Get(key);
            if (cachedData != null)
            {
                string serializedData = Encoding.UTF8.GetString(cachedData);
                value = DeserializeData<T>(serializedData);
                return true;
            }

            value = default;
            return false;
        }
        #region Retry



        #endregion

        private string SerializeData<T>(T value, string format, bool enableCompression)
        {
            if (value == null) return null;

            string serializedData = string.Empty;

            // Default handling for serialization based on format
            switch (format?.ToLower())
            {
                case "xml":
                    serializedData = SerializeToXml(value);
                    break;

                case "json":
                default:
                    serializedData = JsonConvert.SerializeObject(value, Serializer.DefaultSettingsWithTypeInfo);
                    break;
            }

            // Handle compression if enabled
            return enableCompression ? HandleCompression<T>(serializedData) : serializedData;
        }

        private string SerializeToXml<T>(T value)
        {
            var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
            using (var stringWriter = new StringWriter())
            {
                xmlSerializer.Serialize(stringWriter, value);
                return stringWriter.ToString();
            }
        }

        private string HandleCompression<T>(string serializedData)
        {
            var compressedBytes = CompressData(Encoding.UTF8.GetBytes(serializedData));
            return $"COMPRESSED|{DetermineFormat<T>()}|{Convert.ToBase64String(compressedBytes)}";
        }

        private string DetermineFormat<T>()
        {
            Type type = typeof(T);

            // Decide the serialization format based on the type of T
            if (type == typeof(string))
            {
                return "Text";
            }
            else if (type == typeof(XmlDocument) || type == typeof(XDocument))
            {
                return "XML";
            }
            else if (type.IsPrimitive || type.IsValueType || type == typeof(Guid))
            {
                return "JSON"; // Default for simple types
            }
            else if (type.IsClass)
            {
                return "JSON"; // Default for complex objects
            }

            throw new NotSupportedException($"Serialization format for type {type.Name} is not supported.");
        }

        private T DeserializeData<T>(string data)
        {
            if (string.IsNullOrEmpty(data)) return default;

            // Extract metadata and data
            var parts = data.Split('|', 3);
            if (parts.Length < 3) throw new InvalidOperationException("Invalid cached data format.");

            string compressionStatus = parts[0];
            string format = parts[1];
            string serializedData = parts[2];

            // Handle compression
            if (compressionStatus == "COMPRESSED")
            {
                var compressedBytes = Convert.FromBase64String(serializedData);
                var decompressedBytes = DecompressData(compressedBytes);
                serializedData = Encoding.UTF8.GetString(decompressedBytes);
            }

            // Deserialize based on the format
            return format.ToLower() switch
            {
                "xml" => DeserializeFromXml<T>(serializedData),
                "json" or _ => (T)JsonConvert.DeserializeObject(serializedData, Serializer.DefaultSettingsWithTypeInfo),
            };
        }

        private T DeserializeFromXml<T>(string xmlData)
        {
            var xmlSerializer = new System.Xml.Serialization.XmlSerializer(typeof(T));
            using (var stringReader = new StringReader(xmlData))
            {
                return (T)xmlSerializer.Deserialize(stringReader);
            }
        }

        private byte[] CompressData(byte[] data)
        {
            if (data == null) return null;

            using var outputStream = new MemoryStream();
            using (var compressionStream = new GZipStream(outputStream, CompressionLevel.Optimal))
            {
                compressionStream.Write(data, 0, data.Length);
            }

            return outputStream.ToArray();
        }

        private byte[] DecompressData(byte[] compressedData)
        {
            if (compressedData == null) return null;

            using var inputStream = new MemoryStream(compressedData);
            using var outputStream = new MemoryStream();
            using (var decompressionStream = new GZipStream(inputStream, CompressionMode.Decompress))
            {
                decompressionStream.CopyTo(outputStream);
            }

            return outputStream.ToArray();
        }

        #endregion
    }
}
