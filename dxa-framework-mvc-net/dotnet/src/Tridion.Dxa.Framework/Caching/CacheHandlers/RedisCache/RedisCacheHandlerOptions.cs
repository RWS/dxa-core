using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching.CacheHandlers.RedisCache
{
    public class RedisCacheHandlerOptions : DistributedCacheHandlerOptions
    {
        public RedisCacheHandlerOptions(CacheOptions cacheOptions)
            : base(cacheOptions)
        { }
    }
}
