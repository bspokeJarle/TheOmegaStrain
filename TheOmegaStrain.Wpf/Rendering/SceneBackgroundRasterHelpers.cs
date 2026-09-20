using RetroMesh.Engine;
using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Wpf.Rendering
{
    /// <summary>
    /// Builds the scene sky as screen-space triangles behind the projected world.
    /// The lower gradient anchor follows the projected Surface horizon vertically,
    /// while the top remains fixed to the viewport.
    /// </summary>
    public static class SceneBackgroundRasterHelpers
    {
        public const string PartName = "SceneBackgroundRaster";
        private const int RasterBandCount = 32;
        private const float GradientHeightFraction = 0.68f;
        private const float HorizonSamplePercentile = 0.15f;
        private const float HorizonSmoothingPerSecond = 8f;
        private const float StarTransitionStartRatio = 0.65f;

        private static readonly RgbColor HillsWoodsTop = new(0, 1, 2);
        private static readonly RgbColor HillsWoodsMiddle = new(8, 24, 31);
        private static readonly RgbColor HillsWoodsHorizon = new(0, 1, 1);

        private static readonly RgbColor WinterTop = new(0, 1, 3);
        private static readonly RgbColor WinterMiddle = new(16, 32, 48);
        private static readonly RgbColor WinterHorizon = new(0, 0, 1);

        private static readonly RgbColor RainforestTop = new(0, 2, 2);
        private static readonly RgbColor RainforestMiddle = new(7, 29, 25);
        private static readonly RgbColor RainforestHorizon = new(0, 1, 0);

        private static readonly RgbColor DesertTop = new(3, 1, 0);
        private static readonly RgbColor DesertMiddle = new(38, 22, 12);
        private static readonly RgbColor DesertHorizon = new(2, 1, 0);

        public static bool AppendForScene(
            List<ProjectedTriangleMesh> triangles,
            SceneTypes sceneType,
            SceneBiomeTypes sceneBiome,
            int screenWidth,
            int screenHeight,
            float lightningFlashIntensity,
            float impactFlashIntensity,
            float deltaSeconds,
            float surfaceAltitude,
            float starFadeInAltitude,
            bool enabled,
            ref float? smoothedHorizonY)
        {
            if (!enabled ||
                !TryGetProfile(sceneType, sceneBiome, out var profile) ||
                screenWidth <= 0 || screenHeight <= 0)
            {
                smoothedHorizonY = null;
                return false;
            }

            if (!TryCalculateSurfaceHorizonY(triangles, screenHeight, out int measuredHorizonY))
            {
                // Once the Surface leaves the projected viewport, the raster follows it
                // out instead of being held on screen by a fallback position.
                smoothedHorizonY = null;
                return false;
            }

            float smoothingAmount = 1f - MathF.Exp(
                -HorizonSmoothingPerSecond * Math.Clamp(deltaSeconds, 0f, 0.1f));
            smoothedHorizonY = smoothedHorizonY.HasValue
                ? smoothedHorizonY.Value + (measuredHorizonY - smoothedHorizonY.Value) * smoothingAmount
                : measuredHorizonY;
            int horizonY = (int)MathF.Round(smoothedHorizonY.Value);
            float rasterVisibility = CalculateRasterVisibility(surfaceAltitude, starFadeInAltitude);
            if (rasterVisibility <= 0f)
                return false;

            int gradientHeight = Math.Max(1, (int)MathF.Round(screenHeight * GradientHeightFraction));
            int gradientTop = horizonY - gradientHeight;

            // The raster has a stable screen-space height and is anchored at the Surface
            // horizon. It therefore moves vertically with the planet instead of stretching.
            AppendRectangle(triangles, 0, 0, screenWidth, Math.Max(0, gradientTop),
                ApplyWeatherFlash(profile.TopColor, lightningFlashIntensity, impactFlashIntensity));

            for (int band = 0; band < profile.BandCount; band++)
            {
                float startFraction = band / (float)profile.BandCount;
                float endFraction = (band + 1) / (float)profile.BandCount;
                int top = gradientTop + (int)MathF.Round(gradientHeight * startFraction);
                int bottom = gradientTop + (int)MathF.Round(gradientHeight * endFraction);
                float colorFraction = (band + 0.5f) / profile.BandCount;
                var rasterColor = InterpolateThreeStops(
                    profile.TopColor,
                    profile.MiddleColor,
                    profile.HorizonColor,
                    colorFraction);
                var color = ApplyWeatherFlash(
                    Interpolate(profile.TopColor, rasterColor, rasterVisibility),
                    lightningFlashIntensity,
                    impactFlashIntensity);

                AppendRectangle(
                    triangles,
                    0,
                    Math.Max(0, top),
                    screenWidth,
                    Math.Min(screenHeight, Math.Max(top + 1, bottom)),
                    color);
            }

            // The sky must meet the terrain in darkness. This also prevents bright colour
            // from leaking through gaps between irregular Surface triangles.
            var lowerColor = ApplyWeatherFlash(
                Interpolate(profile.TopColor, profile.HorizonColor, rasterVisibility),
                lightningFlashIntensity,
                impactFlashIntensity);
            AppendRectangle(triangles, 0, horizonY, screenWidth, screenHeight, lowerColor);
            return true;
        }

        public static (byte Red, byte Green, byte Blue) GetClearColor(
            SceneTypes sceneType,
            SceneBiomeTypes sceneBiome,
            float lightningFlashIntensity,
            float impactFlashIntensity,
            bool enabled)
        {
            if (!enabled || !TryGetProfile(sceneType, sceneBiome, out var profile))
            {
                return WeatherFlashColorHelpers.GetBackgroundColor(
                    lightningFlashIntensity,
                    impactFlashIntensity);
            }

            var color = ApplyWeatherFlash(
                profile.TopColor,
                lightningFlashIntensity,
                impactFlashIntensity);
            return (color.Red, color.Green, color.Blue);
        }

        public static bool TryCalculateSurfaceHorizonY(
            IReadOnlyList<ProjectedTriangleMesh> triangles,
            int screenHeight,
            out int horizonY)
        {
            var surfaceTopSamples = new List<int>();

            for (int i = 0; i < triangles.Count; i++)
            {
                var triangle = triangles[i];
                if (!string.Equals(triangle.PartName, "Surface", StringComparison.Ordinal))
                    continue;

                int top = Math.Min(triangle.Y1, Math.Min(triangle.Y2, triangle.Y3));
                if (top >= -screenHeight && top <= screenHeight * 2)
                    surfaceTopSamples.Add(top);
            }

            if (surfaceTopSamples.Count == 0)
            {
                horizonY = 0;
                return false;
            }

            surfaceTopSamples.Sort();
            int sampleIndex = Math.Clamp(
                (int)MathF.Round((surfaceTopSamples.Count - 1) * HorizonSamplePercentile),
                0,
                surfaceTopSamples.Count - 1);
            horizonY = surfaceTopSamples[sampleIndex];
            return true;
        }

        public static bool IsBackgroundPart(string? partName) =>
            string.Equals(partName, PartName, StringComparison.Ordinal);

        public static float CalculateRasterVisibility(float surfaceAltitude, float starFadeInAltitude)
        {
            if (starFadeInAltitude <= 0f)
                return 1f;

            float fadeStart = starFadeInAltitude * StarTransitionStartRatio;
            if (surfaceAltitude <= fadeStart)
                return 1f;
            if (surfaceAltitude >= starFadeInAltitude)
                return 0f;

            return 1f - ((surfaceAltitude - fadeStart) / (starFadeInAltitude - fadeStart));
        }

        private static bool TryGetProfile(
            SceneTypes sceneType,
            SceneBiomeTypes sceneBiome,
            out BackgroundProfile profile)
        {
            // Menus and the outro keep their established presentation. Campaign,
            // tutorial and simulation skies use the palette of their active biome.
            if (sceneType != SceneTypes.Game &&
                sceneType != SceneTypes.Tutorial &&
                sceneType != SceneTypes.Simulation)
            {
                profile = default;
                return false;
            }

            profile = sceneBiome switch
            {
                SceneBiomeTypes.Winter => CreateProfile(WinterTop, WinterMiddle, WinterHorizon),
                SceneBiomeTypes.Rainforrest => CreateProfile(RainforestTop, RainforestMiddle, RainforestHorizon),
                SceneBiomeTypes.Desert => CreateProfile(DesertTop, DesertMiddle, DesertHorizon),
                _ => CreateProfile(HillsWoodsTop, HillsWoodsMiddle, HillsWoodsHorizon)
            };
            return true;
        }

        private static BackgroundProfile CreateProfile(
            RgbColor top,
            RgbColor middle,
            RgbColor horizon) => new(top, middle, horizon, RasterBandCount);

        private static void AppendRectangle(
            List<ProjectedTriangleMesh> triangles,
            int left,
            int top,
            int right,
            int bottom,
            RgbColor color)
        {
            if (right <= left || bottom <= top)
                return;

            string hex = $"{color.Red:X2}{color.Green:X2}{color.Blue:X2}";
            triangles.Add(CreateTriangle(left, top, right, top, right, bottom, hex));
            triangles.Add(CreateTriangle(left, top, right, bottom, left, bottom, hex));
        }

        private static ProjectedTriangleMesh CreateTriangle(
            int x1,
            int y1,
            int x2,
            int y2,
            int x3,
            int y3,
            string color) => new()
            {
                X1 = x1,
                Y1 = y1,
                X2 = x2,
                Y2 = y2,
                X3 = x3,
                Y3 = y3,
                CalculatedZ = ScreenSetup.RenderFarZ,
                TriangleAngle = 1f,
                Normal = 1f,
                Color = color,
                Opacity = 1f,
                // Direct3D derives depth from reciprocal W. A very small positive value
                // keeps this screen-space geometry behind every projected world object.
                Rhw1 = 0.0001f,
                Rhw2 = 0.0001f,
                Rhw3 = 0.0001f,
                PartName = PartName
            };

        private static RgbColor Interpolate(RgbColor start, RgbColor end, float amount) => new(
            Mix(start.Red, end.Red, amount),
            Mix(start.Green, end.Green, amount),
            Mix(start.Blue, end.Blue, amount));

        private static RgbColor InterpolateThreeStops(
            RgbColor top,
            RgbColor middle,
            RgbColor horizon,
            float amount)
        {
            const float middlePosition = 0.55f;
            return amount <= middlePosition
                ? Interpolate(top, middle, amount / middlePosition)
                : Interpolate(middle, horizon, (amount - middlePosition) / (1f - middlePosition));
        }

        private static RgbColor ApplyWeatherFlash(
            RgbColor baseColor,
            float lightningFlashIntensity,
            float impactFlashIntensity)
        {
            var flash = WeatherFlashColorHelpers.GetBackgroundColor(
                lightningFlashIntensity,
                impactFlashIntensity);
            return new RgbColor(
                ClampByte(baseColor.Red + flash.Red),
                ClampByte(baseColor.Green + flash.Green),
                ClampByte(baseColor.Blue + flash.Blue));
        }

        private static byte Mix(byte start, byte end, float amount) =>
            ClampByte(start + (end - start) * Math.Clamp(amount, 0f, 1f));

        private static byte ClampByte(float value) =>
            (byte)Math.Clamp((int)MathF.Round(value), 0, 255);

        private readonly record struct RgbColor(byte Red, byte Green, byte Blue);
        private readonly record struct BackgroundProfile(
            RgbColor TopColor,
            RgbColor MiddleColor,
            RgbColor HorizonColor,
            int BandCount);
    }
}
