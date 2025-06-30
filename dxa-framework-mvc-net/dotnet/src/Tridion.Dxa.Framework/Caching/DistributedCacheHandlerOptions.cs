using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public class DistributedCacheHandlerOptions : CacheHandlerOptions
    {
        public DistributedCacheHandlerOptions(CacheOptions cacheOptions)
            : base(cacheOptions)
        { }

        public DistributedCacheHandlerOptions(DistributedCacheHandlerOptions cacheHandlerOptions)
            : base(cacheHandlerOptions)
        {
            Hostname = cacheHandlerOptions.Hostname;
            Port = cacheHandlerOptions.Port;
            InstanceName = cacheHandlerOptions.InstanceName;
        }

        public string Hostname { get; set; }
        public int Port { get; set; }
        public string InstanceName { get; set; }

        public override CacheHandlerOptions Copy()
            => new DistributedCacheHandlerOptions(this);
    }
}
