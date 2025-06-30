using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tridion.Dxa.Framework.Core;

namespace Tridion.Dxa.Framework.Caching
{
    [Flags]
    public enum CacheSerializerFlags
    {
        None = 0x0,
        Compressed = 0x1,
        Xml = 0x2,
        Json = 0x4,
        Native = 0x8
    }

    public interface ICacheSerializer<T>
    {
        byte[] Serialize(T data);
        byte[] Serialize(T data, bool compress, int compressionLimit);
        T Deserialize(byte[] data);
    }

    public abstract class AbstractCacheSerializer<T> : ICacheSerializer<T>
    {
        protected bool IsPrimitive(Type[] types)
        {
            if (types == null || types.Length == 0)
                return false;
            foreach (Type t in types)
            {
                if (!IsPrimitive(t))
                    return false;
            }
            return true;
        }
        protected bool IsPrimitive(Type t)
        {
            return t.IsPrimitive || t == typeof(string) || (t.IsArray && IsPrimitive(t.GetElementType())) ||
                (ReflectionUtils.IsTypeGenericList(t) && IsPrimitive(t.GenericTypeArguments)) ||
                (ReflectionUtils.IsTypeDictionary(t) && IsPrimitive(t.GenericTypeArguments));
        }
        protected bool IsSerializable(Type[] types)
        {
            if (types == null || types.Length == 0)
                return false;
            foreach (Type t in types)
            {
                if (!IsSerializable(t))
                    return false;
            }
            return true;
        }
        protected bool IsSerializable(Type t)
        {
            t = TypeLoader.FindConcreteType(t) ?? t;
            if (!ReflectionUtils.HasAttribute(t, typeof(SerializableAttribute)))
                return false;

            if (t.IsArray)
                return IsSerializable(t.GetElementType());

            if (ReflectionUtils.IsTypeGenericList(t))
                return IsSerializable(t.GenericTypeArguments);

            return !ReflectionUtils.IsTypeDictionary(t) || IsSerializable(t.GenericTypeArguments);
        }
        public byte[] Serialize(T data) { return Serialize(data, false, 0); }
        public abstract byte[] Serialize(T data, bool compress, int compressionLimit);
        public abstract T Deserialize(byte[] data);
        protected byte[] Compress(CacheSerializerFlags flags, byte[] src, bool compress, int compressionLimit)
        {
            if (compress && src.Length > compressionLimit)
            {
                flags |= CacheSerializerFlags.Compressed;
                src = IOUtils.Deflate(src);
            }
            byte[] dst = new byte[src.Length + 1];
            dst[0] = (byte)flags;
            Buffer.BlockCopy(src, 0, dst, 1, src.Length);
            return dst;
        }
        protected byte[] Decompress(byte[] src)
        {
            CacheSerializerFlags flags = (CacheSerializerFlags)src[0];
            byte[] dst = new byte[src.Length - 1];
            Buffer.BlockCopy(src, 1, dst, 0, src.Length - 1);
            return flags.HasFlag(CacheSerializerFlags.Compressed) ? IOUtils.Inflate(dst) : dst;
        }
    }
}
