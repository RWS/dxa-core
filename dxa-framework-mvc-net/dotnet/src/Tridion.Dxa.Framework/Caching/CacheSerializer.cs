using Sdl.Web.Common.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Caching
{
    public class CacheSerializer<T> : AbstractCacheSerializer<T>
    {
        private readonly ICacheSerializer<T> _xmlSerializer;
        private readonly ICacheSerializer<T> _jsonSerializer;
        private readonly ConcurrentDictionary<Type, ICacheSerializer<T>> _serializers;

        public CacheSerializer()
        {
            _jsonSerializer = new JsonSerializer<T>();
            _xmlSerializer = new XmlSerializer<T>();
            _serializers = new ConcurrentDictionary<Type, ICacheSerializer<T>>();
        }

        public override byte[] Serialize(T data, bool compress, int compressionLimit)
        {
            try
            {
                Type oType = data.GetType();
                if (_serializers.ContainsKey(oType))
                    return _serializers[oType] == null ? _jsonSerializer.Serialize(data, compress, compressionLimit) : _serializers[oType].Serialize(data, compress, compressionLimit);
                // prefer json
                byte[] output = _jsonSerializer.Serialize(data, compress, compressionLimit);
                if (output != null)
                {
                    _serializers.TryAdd(oType, _jsonSerializer);
                    return output;
                }
                // fallback to xml
                output = _xmlSerializer.Serialize(data, compress, compressionLimit);
                if (output == null) return null;
                _serializers.TryAdd(oType, _xmlSerializer);
                return output;
            }
            catch (Exception e)
            {
                Log.Debug("Failed to serialize data.", e);
            }
            return null;
        }

        public override T Deserialize(byte[] data)
        {
            if (data == null || data.Length <= 1) return default(T);
            try
            {
                CacheSerializerFlags flags = (CacheSerializerFlags)data[0];
                if (flags.HasFlag(CacheSerializerFlags.Xml))
                {
                    return _xmlSerializer.Deserialize(data);
                }
                if (flags.HasFlag(CacheSerializerFlags.Json))
                {
                    return _jsonSerializer.Deserialize(data);
                }
            }
            catch
            {
                // ignore, it's logged in specialized serializers
            }
            return default(T);
        }
    }
}
