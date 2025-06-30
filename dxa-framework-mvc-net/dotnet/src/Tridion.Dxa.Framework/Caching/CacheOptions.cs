using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public class CacheOptions
    {
        public Dictionary<string, CacheHandlerOptions> CacheHandlers { get; protected set; }
        public Dictionary<string, CacheItemPolicy> Regions { get; protected set; }
        public Dictionary<string, CacheHandlerOptions> RegionToCacheHandler { get; protected set; }
        public string DefaultHandler { get; set; }
        public TimeSpan CacheDisableTime { get; set; }
        public int CacheRetries { get; set; }
        public bool Enabled { get; set; } = true;
        public CacheOptions()
        {
            CacheHandlers = new Dictionary<string, CacheHandlerOptions>();
            Regions = new Dictionary<string, CacheItemPolicy>();
            RegionToCacheHandler = new Dictionary<string, CacheHandlerOptions>();
        }

        public void AddCacheHandler(CacheHandlerOptions cacheHandlerOptions)
        {
            if (cacheHandlerOptions != null)
            {
                CacheHandlers.Add(cacheHandlerOptions.Name, cacheHandlerOptions);
            }
        }

        public void AddRegion(string regionName, string cacheName, CacheItemPolicy cacheEntryOptions)
        {
            Regions.Add(regionName, cacheEntryOptions);
            if (string.IsNullOrEmpty(cacheName)) return;
            if (!CacheHandlers.ContainsKey(cacheName))
                throw new KeyNotFoundException($"Cache handler with name '{cacheName}' not found.");
            RegionToCacheHandler.Add(regionName, CacheHandlers[cacheName]);
        }

        public CacheHandlerOptions GetCacheHandlerOptionsFromCacheName(string cacheName)
        {
            return CacheHandlers.ContainsKey(cacheName) ? CacheHandlers[cacheName] : CacheHandlers[DefaultHandler];
        }

        public CacheHandlerOptions GetCacheHandlerOptionsForRegion(string region)
        {
            if (region != null && RegionToCacheHandler.ContainsKey(region))
            {
                return RegionToCacheHandler[region];
            }
            return CacheHandlers[DefaultHandler];
        }
    }
}