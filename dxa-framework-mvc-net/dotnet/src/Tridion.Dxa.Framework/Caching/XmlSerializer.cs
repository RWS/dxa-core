using Sdl.Web.Common.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Tridion.Dxa.Framework.Core;

namespace Tridion.Dxa.Framework.Caching
{
    public class XmlSerializer<T> : AbstractCacheSerializer<T>
    {
        public override byte[] Serialize(T cacheData, bool compress, int compressionLimit)
        {
            if (cacheData == null) return null;
            try
            {
                Type cacheItemType = typeof(CacheItem<>).MakeGenericType(cacheData.GetType());
                ICacheItem cacheItem = (ICacheItem)ReflectionUtils.CreateInstance(cacheItemType, new object[] { cacheData });
                XmlSerializer xmlSerializer;
                if (cacheData.GetType() == typeof(T))
                {
                    Type dataType = ((ICacheItem)cacheData).GetData().GetType();
                    xmlSerializer = new XmlSerializer(cacheItemType, new Type[] { dataType });
                }
                else
                {
                    xmlSerializer = new XmlSerializer(cacheItemType);
                }

                using (var sw = new StringWriter())
                {
                    xmlSerializer.Serialize(sw, cacheItem);
                    string xml = sw.ToString();
                    byte[] data = Encoding.UTF8.GetBytes(cacheItemType.FullName + xml);
                    const CacheSerializerFlags flags = CacheSerializerFlags.Xml;
                    return Compress(flags, data, compress, compressionLimit);
                }
            }
            catch
            {
                // ignore
            }
            return null;
        }

        public override T Deserialize(byte[] data)
        {
            if (data == null) return default(T);
            try
            {
                data = Decompress(data);
                string xml = Encoding.UTF8.GetString(data);
                int index = xml.IndexOf("<");
                string typeName = xml.Substring(0, index);
                xml = xml.Substring(index);
                XmlSerializer xmlSerializer = new XmlSerializer(Type.GetType(typeName));
                using (StringReader sr = new StringReader(xml))
                {
                    ICacheItem cacheItem = (ICacheItem)xmlSerializer.Deserialize(sr);
                    return (T)cacheItem.GetData();
                }
            }
            catch (Exception e)
            {
                Log.Debug($"Failed to deserialize data to type '{typeof(T).FullName}' due to '{e.Message}'");
            }
            return default(T);
        }
    }
}
