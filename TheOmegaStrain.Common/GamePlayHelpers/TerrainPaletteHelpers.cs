using Domain;

namespace CommonUtilities.GamePlayHelpers
{
    public static class TerrainPaletteHelpers
    {
        private static readonly (int r, int g, int b)[] CraterColorsRgb =
        [
            (0x2B, 0x1D, 0x0E),
            (0x1A, 0x1A, 0x1A),
            (0x3A, 0x3A, 0x3A),
            (0x2E, 0x2E, 0x2E),
            (0x3D, 0x2B, 0x1D)
        ];

        public static string GetTerrainColorHex(int height, int maxHeight, SceneBiomeTypes biome)
        {
            GetTerrainColorRgb(height, maxHeight, biome, out int red, out int green, out int blue);
            return ToHex(red, green, blue);
        }

        public static string GetVisibleTerrainColorHex(
            SurfaceData tile,
            int tileX,
            int tileZ,
            int maxHeight,
            SceneBiomeTypes biome)
        {
            GetVisibleTerrainColorRgb(tile, tileX, tileZ, maxHeight, biome, out int red, out int green, out int blue);
            return ToHex(red, green, blue);
        }

        public static void GetVisibleTerrainColorRgb(
            SurfaceData tile,
            int tileX,
            int tileZ,
            int maxHeight,
            SceneBiomeTypes biome,
            out int red,
            out int green,
            out int blue)
        {
            GetTerrainColorRgb(tile.mapDepth, maxHeight, biome, out red, out green, out blue);
            ApplyTileColorOverrides(tile, tileX, tileZ, maxHeight, ref red, ref green, ref blue);
        }

        public static void ApplyTileColorOverrides(
            SurfaceData tile,
            int tileX,
            int tileZ,
            int maxHeight,
            ref int red,
            ref int green,
            ref int blue)
        {
            if (tile.isCratered)
            {
                var crater = CraterColorsRgb[(tileZ * 7 + tileX * 13) % CraterColorsRgb.Length];
                red = crater.r;
                green = crater.g;
                blue = crater.b;
            }

            if (tile.isInfected && IsInfectableTerrain(tile.mapDepth, maxHeight))
            {
                red = 255;
                green = 0;
                blue = 0;
            }
        }

        public static bool IsInfectableTerrain(int height, int maxHeight)
        {
            var terrain = GamePlayHelpers.GetTerrainType(height, maxHeight);
            return terrain == GamePlayHelpers.TerrainType.Grassland ||
                   terrain == GamePlayHelpers.TerrainType.Highlands;
        }

        public static void GetTerrainColorRgb(
            int height,
            int maxHeight,
            SceneBiomeTypes biome,
            out int red,
            out int green,
            out int blue)
        {
            maxHeight = Math.Max(1, maxHeight);

            if (biome == SceneBiomeTypes.Winter)
            {
                if (height < maxHeight * 0.05)
                {
                    red = 25;
                    green = 55;
                    blue = 165;
                }
                else if (height < maxHeight * 0.15)
                {
                    red = 60;
                    green = 110;
                    blue = 210;
                }
                else if (height < maxHeight * 0.4)
                {
                    float t = (height - (maxHeight * 0.15f)) / (maxHeight * 0.25f);
                    t = Math.Clamp(t, 0f, 1f);
                    red = 95 + (int)(t * 120);
                    green = 145 + (int)(t * 85);
                    blue = 215 + (int)(t * 25);
                }
                else if (height < maxHeight * 0.7)
                {
                    red = 195;
                    green = 210;
                    blue = 225;
                }
                else
                {
                    red = 235;
                    green = 240;
                    blue = 245;
                }

                ClampRgb(ref red, ref green, ref blue);
                return;
            }

            if (biome == SceneBiomeTypes.Rainforrest)
            {
                if (height < maxHeight * 0.05)
                {
                    red = 0;
                    green = 24;
                    blue = 170;
                }
                else if (height < maxHeight * 0.15)
                {
                    red = 0;
                    green = 95;
                    blue = 235;
                }
                else if (height < maxHeight * 0.4)
                {
                    red = 20;
                    green = 190;
                    blue = 45;
                }
                else if (height < maxHeight * 0.7)
                {
                    red = 120;
                    green = 95;
                    blue = 35;
                }
                else
                {
                    red = 125;
                    green = 145;
                    blue = 130;
                }

                ClampRgb(ref red, ref green, ref blue);
                return;
            }

            if (biome == SceneBiomeTypes.Desert)
            {
                if (height < maxHeight * 0.05)
                {
                    red = 20;
                    green = 55;
                    blue = 165;
                }
                else if (height < maxHeight * 0.15)
                {
                    red = 60;
                    green = 125;
                    blue = 220;
                }
                else if (height < maxHeight * 0.4)
                {
                    red = 205;
                    green = 175;
                    blue = 95;
                }
                else if (height < maxHeight * 0.7)
                {
                    red = 185;
                    green = 145;
                    blue = 80;
                }
                else
                {
                    red = 170;
                    green = 155;
                    blue = 135;
                }

                ClampRgb(ref red, ref green, ref blue);
                return;
            }

            if (height < maxHeight * 0.05)
            {
                red = 0;
                green = 0;
                blue = 180 + (int)((height / (maxHeight * 0.05)) * 75);
            }
            else if (height < maxHeight * 0.15)
            {
                red = 0;
                green = (int)((height / (maxHeight * 0.2)) * 100);
                blue = 255;
            }
            else if (height < maxHeight * 0.4)
            {
                red = 0;
                green = 150 + ((height - (int)(maxHeight * 0.2)) * 3);
                blue = 0;
            }
            else if (height < maxHeight * 0.7)
            {
                red = 139 + ((height - (int)(maxHeight * 0.4)) * 3);
                green = 69 + ((height - (int)(maxHeight * 0.4)) * 2);
                blue = 19;
            }
            else
            {
                red = 120 + ((height - (int)(maxHeight * 0.7)) * 3);
                green = 120 + ((height - (int)(maxHeight * 0.7)) * 3);
                blue = 120 + ((height - (int)(maxHeight * 0.7)) * 3);
            }

            ClampRgb(ref red, ref green, ref blue);
        }

        private static void ClampRgb(ref int red, ref int green, ref int blue)
        {
            red = Math.Clamp(red, 0, 255);
            green = Math.Clamp(green, 0, 255);
            blue = Math.Clamp(blue, 0, 255);
        }

        private static string ToHex(int red, int green, int blue)
        {
            red = Math.Clamp(red, 0, 255);
            green = Math.Clamp(green, 0, 255);
            blue = Math.Clamp(blue, 0, 255);
            return $"{red:X2}{green:X2}{blue:X2}";
        }
    }
}
