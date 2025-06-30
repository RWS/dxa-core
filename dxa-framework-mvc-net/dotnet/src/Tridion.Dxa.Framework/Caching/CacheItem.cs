using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public interface ICacheItem
    {
        object GetData();
    }

    public class CacheItem<T> : ICacheItem
    {
        public T Data { get; set; }

        internal CacheItem()
        { }

        public CacheItem(T data)
        {
            Data = data;
        }

        public object GetData() => Data;
    }
}
