using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public class CacheItemPolicy
    {
        public TimeSpan? AbsoluteExpiration
        {
            get;
            set;
        }

        public TimeSpan? SlidingExpiration
        {
            get;
            set;
        }

        public DateTimeOffset? AbsoluteExpirationFromNow => AbsoluteExpiration.HasValue ? new DateTimeOffset(DateTime.UtcNow.AddSeconds(AbsoluteExpiration.Value.TotalSeconds)) : new DateTimeOffset(DateTime.UtcNow.AddSeconds(300));
    }
}
