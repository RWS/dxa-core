﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tridion.Dxa.Framework.Core
{
    public class Murmur3
    {
        public static uint Hash(string data)
        {
            byte[] buf = Encoding.UTF8.GetBytes(data);
            return Hash(buf, (uint)buf.Length, 144);
        }

        public static uint Hash(byte[] data, uint length, uint seed)
        {
            uint nblocks = length >> 2;
            uint h1 = seed;
            const uint c1 = 0xcc9e2d51;
            const uint c2 = 0x1b873593;
            int i = 0;

            for (uint j = nblocks; j > 0; --j)
            {
                uint k1l = BitConverter.ToUInt32(data, i);

                k1l *= c1;
                k1l = rotl32(k1l, 15);
                k1l *= c2;

                h1 ^= k1l;
                h1 = rotl32(h1, 13);
                h1 = h1 * 5 + 0xe6546b64;

                i += 4;
            }

            nblocks <<= 2;
            uint k1 = 0;
            uint tailLength = length & 3;

            if (tailLength == 3)
                k1 ^= (uint)data[2 + nblocks] << 16;
            if (tailLength >= 2)
                k1 ^= (uint)data[1 + nblocks] << 8;
            if (tailLength >= 1)
            {
                k1 ^= data[nblocks];
                k1 *= c1; k1 = rotl32(k1, 15); k1 *= c2; h1 ^= k1;
            }

            h1 ^= length;
            h1 = fmix32(h1);
            return h1;
        }

        static uint fmix32(uint h)
        {
            h ^= h >> 16;
            h *= 0x85ebca6b;
            h ^= h >> 13;
            h *= 0xc2b2ae35;
            h ^= h >> 16;

            return h;
        }

        static uint rotl32(uint x, byte r) => (x << r) | (x >> (32 - r));
    }
}
