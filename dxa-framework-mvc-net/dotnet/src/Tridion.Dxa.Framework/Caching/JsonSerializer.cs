using Newtonsoft.Json;
using Sdl.Web.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tridion.Dxa.Framework.Core.JSON.NET;

namespace Tridion.Dxa.Framework.Caching
{
    public class JsonSerializer<T> : AbstractCacheSerializer<T>
    {
        public override byte[] Serialize(T cacheData, bool compress, int compressionLimit)
        {
            if (cacheData == null) return null;
            try
            {
                string json = JsonConvert.SerializeObject(cacheData, Serializer.DefaultSettingsWithTypeInfo);
                byte[] data = Encoding.UTF8.GetBytes(json);
                CacheSerializerFlags flags = CacheSerializerFlags.Json;
                var output = Compress(flags, data, compress, compressionLimit);
                return output;
            }
            catch (Exception e)
            {
                Log.Debug($"Failed to serialize data of type '{typeof(T).FullName}' due to '{e.Message}'");
            }
            return null;
        }

        public override T Deserialize(byte[] data)
        {
            if (data == null) return default(T);
            try
            {
                data = Decompress(data);
                string json = Encoding.UTF8.GetString(data);
                return (T)JsonConvert.DeserializeObject(json, Serializer.DefaultSettingsWithTypeInfo);
            }
            catch (Exception e)
            {
                Log.Debug($"Failed to deserialize data to type '{typeof(T).FullName}' due to '{e.Message}'");
            }
            return default(T);
        }
    }
}
