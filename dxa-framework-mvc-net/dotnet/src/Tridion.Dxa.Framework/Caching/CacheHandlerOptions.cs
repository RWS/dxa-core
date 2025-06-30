using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public class CacheHandlerOptions
    {
        public string Name { get; set; }
        public string CacheType { get; set; }
        public CacheOptions GlobalCacheOptions { get; protected set; }
        public ICacheHandler CacheHandler { get; set; }
        public CacheItemPolicy GlobalCacheItemPolicy { get; set; }
        public bool Compression { get; set; }
        public int CompressionLimit { get; set; }
        public bool HashKey { get; set; }
        public CacheHandlerOptions(CacheOptions cacheOptions)
        {
            GlobalCacheOptions = cacheOptions;
        }
        public CacheHandlerOptions(CacheHandlerOptions cacheHandlerOptions)
        {
            Name = cacheHandlerOptions.Name;
            CacheType = cacheHandlerOptions.CacheType;
            GlobalCacheOptions = cacheHandlerOptions.GlobalCacheOptions;
            CacheHandler = cacheHandlerOptions.CacheHandler;
            GlobalCacheItemPolicy = cacheHandlerOptions.GlobalCacheItemPolicy;
            Compression = cacheHandlerOptions.Compression;
            CompressionLimit = cacheHandlerOptions.CompressionLimit;
            HashKey = cacheHandlerOptions.HashKey;
        }

        public virtual CacheHandlerOptions Copy()
            => new CacheHandlerOptions(this);
    }
}
