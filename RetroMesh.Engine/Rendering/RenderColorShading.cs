using System;
using System.Runtime.CompilerServices;

namespace Domain
{
    public static class RenderColorShading
    {
        private const int Brightness = 3;
        private static readonly char[] HexChars = "0123456789ABCDEF".ToCharArray();

        public static string GetShadeOfColorFromNormal(float normal, string? color)
        {
            ReadOnlySpan<char> span = string.IsNullOrEmpty(color)
                ? "000000".AsSpan()
                : color.AsSpan();

            if (span.Length > 0 && span[0] == '#')
                span = span[1..];

            if (span.Length < 6)
                return "#000000";

            float localNormal = Math.Abs(normal);

            int r = Math.Clamp((int)(ParseHexByte(span[0], span[1]) * localNormal) + Brightness, 0, 255);
            int g = Math.Clamp((int)(ParseHexByte(span[2], span[3]) * localNormal) + Brightness, 0, 255);
            int b = Math.Clamp((int)(ParseHexByte(span[4], span[5]) * localNormal) + Brightness, 0, 255);

            return string.Create(7, (r, g, b), static (dst, rgb) =>
            {
                dst[0] = '#';
                dst[1] = HexChars[rgb.r >> 4];
                dst[2] = HexChars[rgb.r & 0xF];
                dst[3] = HexChars[rgb.g >> 4];
                dst[4] = HexChars[rgb.g & 0xF];
                dst[5] = HexChars[rgb.b >> 4];
                dst[6] = HexChars[rgb.b & 0xF];
            });
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ParseHexByte(char hi, char lo)
            => (HexDigit(hi) << 4) | HexDigit(lo);

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int HexDigit(char c)
        {
            if (c >= '0' && c <= '9') return c - '0';
            if (c >= 'a' && c <= 'f') return c - 'a' + 10;
            if (c >= 'A' && c <= 'F') return c - 'A' + 10;
            return 0;
        }
    }
}
