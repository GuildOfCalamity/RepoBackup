using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WinUIDemo;

/// <summary>
/// My simplified interpretation of Google's CityHash.
/// Most of the stacking calls will be in-lined by the compiler.
/// </summary>
public static class LowCityHash
{
    const ulong k0 = 0xc3a5c85c97cb3127;
    const ulong k1 = 0xb492b66fbe98f273;
    const ulong k2 = 0x9ae16a3b2f90404f;
    const ulong k3 = 0xc949d7c7509e6557;

    public static ulong Hash64(string data, int length = 0)
    {
        if (string.IsNullOrEmpty(data))
            return 0;

        return Hash64(Encoding.UTF8.GetBytes(data), length);
    }

    public static ulong Hash64(byte[] data, int length = 0)
    {
        if (data.Length == 0)
            return 0;

        if (length == 0)
            length = data.Length;

        if (length <= 32)
        {
            if (length <= 16)
                return HashLen0to16(data, length);
            else
                return HashLen17to32(data, length);
        }
        else if (length <= 64)
            return HashLen33to64(data, length);

        ulong x = Fetch64(data);
        ulong y = Fetch64(data, length - 16) ^ k1;
        ulong z = Fetch64(data, length - 56) ^ k0;

        ulong[] v = new ulong[2];
        ulong[] w = new ulong[2];
        
        v[0] = Rotate(y - x + k0 + Fetch64(data, length - 32), 37) * k1;
        v[1] = Rotate(y + Fetch64(data, length - 24), 42) * k1;

        w[0] = Rotate(y - x + k2 + Fetch64(data, length - 32), 37) * k3;
        w[1] = Rotate(y + Fetch64(data, length - 24), 42) * k3;

        x ^= Rotate(k1 + Fetch64(data, length - 48), 49) * (z + (ulong)length);
        y += Rotate(Fetch64(data, length - 40), 35) * k1;
        z *= Rotate(k1 + Fetch64(data, length - 16), 53);

        for (int i = 0; i < length - 1; i += 64)
        {
            x = Rotate(x + y + v[0] + Fetch64(data, i + 8), 37) * k1;
            y = Rotate(y + v[1] + Fetch64(data, i + 48), 42) * k1;
            x ^= w[1];
            y += v[0] + Fetch64(data, i + 40);
            z = Rotate(z + w[0], 33) * k1;

            WeakHashLen32WithSeeds(data, i, v[1] * k1, x + w[0], v);
            WeakHashLen32WithSeeds(data, i + 32, z + w[1], y + Fetch64(data, i + 16), w);
        }

        return HashLen16(HashLen16(v[0], w[0]) + ShiftMix(y) * k1 + z, HashLen16(v[1], w[1]) + x);
    }

    #region [Helpers]
    static ulong Fetch64(byte[] data, int offset = 0)
    {
        return BitConverter.ToUInt64(data, offset);
    }

    static uint Fetch32(byte[] data, int offset = 0)
    {
        return BitConverter.ToUInt32(data, offset);
    }

    static ulong Rotate(ulong val, int shift)
    {
        return shift == 0 ? val : ((val >> shift) | (val << (64 - shift)));
    }

    static ulong ShiftMix(ulong val)
    {
        return val ^ (val >> 47);
    }

    static ulong HashLen16(ulong u, ulong v)
    {
        return Hash128to64(new ulong[] { u, v });
    }

    static ulong HashLen16(ulong u, ulong v, ulong mul)
    {
        ulong a = (u ^ v) * mul;
        a ^= (a >> 47);
        ulong b = (v ^ a) * mul;
        b ^= (b >> 47);
        b *= mul;
        return b;
    }

    static void WeakHashLen32WithSeeds(byte[] data, int offset, ulong seedA, ulong seedB, ulong[] result)
    {
        ulong part1 = Fetch64(data, offset);
        ulong part2 = Fetch64(data, offset + 8);
        ulong part3 = Fetch64(data, offset + 16);
        ulong part4 = Fetch64(data, offset + 24);

        seedA += part1;
        seedB = Rotate(seedB + seedA + part4, 21);
        ulong c = seedA;
        seedA += part2;
        seedA += part3;
        seedB += Rotate(seedA, 44);

        result[0] = seedA + part4;
        result[1] = seedB + c;
    }

    static ulong HashLen0to16(byte[] data, int length)
    {
        if (length >= 8)
        {
            ulong mul = k2 + (ulong)length * 2;
            ulong a = Fetch64(data) + k2;
            ulong b = Fetch64(data, length - 8);
            ulong c = Rotate(b, 37) * mul + a;
            ulong d = (Rotate(a, 25) + b) * mul;
            return HashLen16(c, d, mul);
        }
        if (length >= 4)
        {
            ulong mul = k2 + (ulong)length * 2;
            ulong a = Fetch32(data);
            return HashLen16((ulong)length + (a << 3), Fetch32(data, length - 4), mul);
        }
        if (length > 0)
        {
            byte a = data[0];
            byte b = data[length >> 1];
            byte c = data[length - 1];
            uint y = a + ((uint)b << 8);
            uint z = (uint)length + ((uint)c << 2);
            return ShiftMix(y * k2 ^ z * k0) * k2;
        }
        return k2;
    }

    static ulong HashLen17to32(byte[] data, int length)
    {
        ulong mul = k2 + (ulong)length * 2;
        ulong a = Fetch64(data) * k1;
        ulong b = Fetch64(data, 8);
        ulong c = Fetch64(data, length - 8) * mul;
        ulong d = Fetch64(data, length - 16) * k2;
        return HashLen16(Rotate(a + b, 43) + Rotate(c, 30) + d, a + Rotate(b + k2, 18) + c, mul);
    }

    static ulong HashLen33to64(byte[] data, int length)
    {
        ulong mul = k2 + (ulong)length * 2;
        ulong a = Fetch64(data) * k2;
        ulong b = Fetch64(data, 8);
        ulong c = Fetch64(data, length - 8) * mul;
        ulong d = Fetch64(data, length - 16) * k2;
        ulong y = Rotate(a + b, 43) + Rotate(c, 30) + d;
        ulong z = HashLen16(y, a + Rotate(b + k2, 18) + c, mul);
        ulong e = Fetch64(data, 16) * k2;
        ulong f = Fetch64(data, 24);
        ulong g = (y + Fetch64(data, length - 32)) * mul;
        ulong h = (z + Fetch64(data, length - 24)) * mul;
        return HashLen16(Rotate(e + f, 43) + Rotate(g, 30) + h, e + Rotate(f + a, 18) + g, mul);
    }

    static ulong Hash128to64(ulong[] x)
    {
        const ulong kMul = 0x9ddfea08eb382d69;
        ulong a = (x[0] ^ x[1]) * kMul;
        a ^= (a >> 47);
        ulong b = (x[1] ^ a) * kMul;
        b ^= (b >> 47);
        b *= kMul;
        return b;
    }
    #endregion
}
