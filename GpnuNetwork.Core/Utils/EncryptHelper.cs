using System.Globalization;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace GpnuNetwork.Core.Utils;

public static class EncryptHelper
{
    private static ReadOnlySpan<byte> CharToHexLookup =>
    [
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 15
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 31
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 47
        0x0,  0x1,  0x2,  0x3,  0x4,  0x5,  0x6,  0x7,  0x8,  0x9,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 63
        0xFF, 0xA,  0xB,  0xC,  0xD,  0xE,  0xF,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 79
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 95
        0xFF, 0xa,  0xb,  0xc,  0xd,  0xe,  0xf,  0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 111
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 127
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 143
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 159
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 175
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 191
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 207
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 223
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, // 239
        0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF  // 255
    ];

    public static string AuthPasswordEncrypt(string password, string mac, string exponentHex, string modulusHex)
    {
        var content = $"{password}>{mac}";
        var e = new BigInteger(Convert.FromHexString((exponentHex.Length % 2 == 0 ? ' ' : '0') + exponentHex), true, true);
        var m = new BigInteger(Convert.FromHexString(modulusHex), true, true);
        var c = new BigInteger(Encoding.ASCII.GetBytes(content.Reverse().ToArray()));
        c = BigInteger.ModPow(c, e, m);
        return Convert.ToHexStringLower(c.ToByteArray(true, true));
    }

    public static BigInteger FromHex(string hexString)
    {
        var hex = new byte[(hexString.Length + 1) / 2];
        {
            int i = 0, j = 0;
            if (hexString.Length % 2 == 1)
                hex[i++] = CharToHexLookup[hexString[j++]];
            for (; j < hexString.Length; i++, j += 2)
                hex[i] = (byte)((CharToHexLookup[hexString[j]] << 4) | CharToHexLookup[hexString[j + 1]]);
        }

        // ceil
        var len = (hex.Length + 1) & ~1;
        var buffer = new byte[len];
        var shortBuf = MemoryMarshal.Cast<byte, ushort>(buffer);
        var remain = hex.Length & 1;
        var intHex = MemoryMarshal.Cast<byte, ushort>(hex.AsSpan()[remain..]);
        for (var i = 0; i < intHex.Length; i++)
            shortBuf[i] = intHex[intHex.Length - i - 1];
        if (remain != 0)
            buffer[^1] = hex[0];

        return new BigInteger(buffer, true, true);
    }

    public static string ToHex(BigInteger bi, bool lower = true)
    {
        var hex = bi.ToByteArray(true, true).AsSpan();
        var len = (hex.Length + (hex.Length & 1)) << 2;
        var result = new string('\0', len);
        var str = MemoryMarshal.CreateSpan(ref Unsafe.AsRef(in result.GetPinnableReference()), len);
        var fatStr = MemoryMarshal.Cast<char, uint>(str);

        uint casing = lower ? 0x200020u : 0;
        var remain = hex.Length & 1;

        if (remain != 0)
        {
            fatStr[0] = 0;
            fatStr[1] = ToCharsBuffer(hex[^1], casing);
            fatStr = fatStr[2..];
            hex = hex[..^1];
        }

        for (int i = hex.Length - 2, j = 0; i >= 0; i -= 2)
        {
            fatStr[j++] = ToCharsBuffer(hex[i], casing);
            fatStr[j++] = ToCharsBuffer(hex[i + 1], casing);
        }

        return result;
    }

    private static byte[] GetShortReverseBytes(string content)
    {
        var len = content.Length + (content.Length & 1);
        var arr = new byte[len];
        for (var i = 0; i < content.Length; i++)
            arr[i] = (byte)content[i ^ 1];
        return arr;
    }
    
    // By Executor-Cheng https://github.com/LagrangeDev/Lagrange.Core/pull/344#pullrequestreview-2027515322
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToCharsBuffer(byte value, uint casing = 0)
    {
        uint difference = BitConverter.IsLittleEndian 
            ? ((uint)value >> 4) + ((value & 0x0Fu) << 16) - 0x890089u 
            : ((value & 0xF0u) << 12) + (value & 0x0Fu) - 0x890089u;
        uint packedResult = ((((uint)-(int)difference & 0x700070u) >> 4) + difference + 0xB900B9u) | casing;
        return packedResult;
    }
}