using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core
{
    /// <summary>
    /// IOUtils
    /// 
    /// Collection of utilities for common IO operations
    /// </summary>
    public static class IOUtils
    {
        /// <summary>
        /// Copies one stream to another
        /// </summary>
        /// <param name="src">Source stream</param>
        /// <param name="dest">Destination stream</param>
        public static void CopyTo(Stream src, Stream dest)
        {
            byte[] bytes = new byte[4096];
            int read;
            while ((read = src.Read(bytes, 0, bytes.Length)) > 0)
            {
                dest.Write(bytes, 0, read);
            }
        }

        /// <summary>
        /// Read stream into a string
        /// </summary>
        /// <param name="stream"></param>
        /// <returns></returns>
        public static string ReadStream(Stream stream)
        {
            using (StreamReader reader = new StreamReader(stream))
            {
                return reader.ReadToEnd();
            }
        }

        /// <summary>
        /// Write data to a stream.
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="data">Data to write</param>
        public static void WriteData(Stream stream, byte[] data)
        {
            if (data.Length > 0)
            {
                stream.Write(data, 0, data.Length);
            }
        }

        /// <summary>
        /// Convert stream to byte array.
        /// </summary>
        /// <param name="input">Input stream</param>
        /// <returns>Byte array of stream contents</returns>
        public static byte[] ConvertStreamToByteArray(Stream input)
        {
            byte[] buffer = new byte[16 * 1024];
            using (MemoryStream ms = new MemoryStream())
            {
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ms.Write(buffer, 0, read);
                }
                return ms.ToArray();
            }
        }

        /// <summary>
        /// Given a string create a memory stream for it.
        /// </summary>
        /// <param name="s">String</param>
        /// <returns>Stream</returns>
        public static Stream CreateStreamFromString(string str)
        {
            MemoryStream stream = new MemoryStream();
            StreamWriter writer = new StreamWriter(stream);
            writer.Write(str);
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        /// <summary>
        /// Compress a block of data with GZIP
        /// </summary>
        /// <param name="data">Data to compress</param>
        /// <returns>Compressed data</returns>
        public static byte[] Deflate(byte[] data)
        {
            using (MemoryStream i = new MemoryStream(data))
            {
                using (MemoryStream o = new MemoryStream())
                {
                    using (Stream s = new GZipStream(o, CompressionMode.Compress))
                    {
                        CopyTo(i, s);
                    }
                    return o.ToArray();
                }
            }
        }

        /// <summary>
        /// Decompress a block of data
        /// </summary>
        /// <param name="data">Compressed data</param>
        /// <returns>Decompressed data</returns>
        public static byte[] Inflate(byte[] data)
        {
            using (MemoryStream i = new MemoryStream(data))
            {
                using (MemoryStream o = new MemoryStream())
                {
                    using (Stream s = new GZipStream(i, CompressionMode.Decompress))
                    {
                        CopyTo(s, o);
                    }
                    return o.ToArray();
                }
            }
        }
    }
}
