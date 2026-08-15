using System;

namespace RetroMesh.Engine
{
    public static class ColorMath
    {
        public static int ClampColor(int value)
        {
            return Math.Clamp(value, 0, 255);
        }

        public static string LerpColorHex(
            string hexFrom,
            string hexTo,
            float amount,
            bool lowerCase = false,
            bool roundChannels = false)
        {
            amount = Math.Clamp(amount, 0f, 1f);
            ParseHexColor(hexFrom, out int r1, out int g1, out int b1);
            ParseHexColor(hexTo, out int r2, out int g2, out int b2);

            int r = InterpolateChannel(r1, r2, amount, roundChannels);
            int g = InterpolateChannel(g1, g2, amount, roundChannels);
            int b = InterpolateChannel(b1, b2, amount, roundChannels);

            var result = $"{r:X2}{g:X2}{b:X2}";
            return lowerCase ? result.ToLowerInvariant() : result;
        }

        private static int InterpolateChannel(int from, int to, float amount, bool round)
        {
            float value = from + (to - from) * amount;
            return ClampColor(round ? (int)MathF.Round(value) : (int)value);
        }

        public static void ParseHexColor(string? hex, out int r, out int g, out int b)
        {
            hex = (hex ?? string.Empty).Trim().TrimStart('#');
            if (hex.Length < 6)
            {
                r = 255;
                g = 255;
                b = 255;
                return;
            }

            r = Convert.ToInt32(hex.Substring(0, 2), 16);
            g = Convert.ToInt32(hex.Substring(2, 2), 16);
            b = Convert.ToInt32(hex.Substring(4, 2), 16);
        }
    }
}
