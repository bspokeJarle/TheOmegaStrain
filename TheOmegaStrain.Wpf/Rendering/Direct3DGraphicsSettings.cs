using RetroMesh.Engine;
using System;
using System.Collections.Generic;
using System.Globalization;
using TheOmegaStrain.Common.CommonGlobalState.States;

namespace TheOmegaStrain.Wpf.Rendering
{
    /// <summary>
    /// Expands Omega-specific visual settings into generic projected triangles.
    /// The Direct3D backend only needs to understand color and alpha.
    /// </summary>
    public static class Direct3DGraphicsSettings
    {
        public static void Apply(List<ProjectedTriangleMesh> triangles, GameSettingsState settings)
        {
            if (triangles == null || triangles.Count == 0 || settings == null)
                return;

            int sourceCount = triangles.Count;
            for (int i = 0; i < sourceCount; i++)
            {
                var triangle = triangles[i];
                triangle.Color = ApplyQualityColor(triangle.Color, settings.GraphicsQuality);

                if (ProjectedTriangleRenderMath.IsCrashBoxPartName(triangle.PartName))
                    triangle.Opacity = 0.25f;

                if (settings.GraphicsQuality == GraphicsQualityPreset.High &&
                    settings.EnhancedShadowsEnabled &&
                    WorldRenderer.IsEnhancedShadowCandidatePartName(triangle.PartName))
                {
                    triangles.Add(CreateScaledPass(triangle, 1.28f, "000000", 70f / 255f, -0.002f));
                }

                if (settings.GlowEffectsEnabled && WorldRenderer.IsGlowCandidatePartName(triangle.PartName))
                {
                    string glowColor = BoostGlowColor(triangle.Color, triangle.PartName);
                    triangles.Add(CreateScaledPass(
                        triangle,
                        GetGlowScale(triangle.PartName, outer: true),
                        glowColor,
                        GetGlowAlpha(triangle.PartName, outer: true) / 255f,
                        -0.002f));
                    triangles.Add(CreateScaledPass(
                        triangle,
                        GetGlowScale(triangle.PartName, outer: false),
                        glowColor,
                        GetGlowAlpha(triangle.PartName, outer: false) / 255f,
                        -0.001f));
                }

                triangles[i] = triangle;
            }
        }

        private static ProjectedTriangleMesh CreateScaledPass(
            ProjectedTriangleMesh source,
            float scale,
            string color,
            float opacity,
            float depthOrderOffset)
        {
            double centerX = (source.X1 + source.X2 + source.X3) / 3.0;
            double centerY = (source.Y1 + source.Y2 + source.Y3) / 3.0;
            source.X1 = ScaleCoordinate(source.X1, centerX, scale);
            source.Y1 = ScaleCoordinate(source.Y1, centerY, scale);
            source.X2 = ScaleCoordinate(source.X2, centerX, scale);
            source.Y2 = ScaleCoordinate(source.Y2, centerY, scale);
            source.X3 = ScaleCoordinate(source.X3, centerX, scale);
            source.Y3 = ScaleCoordinate(source.Y3, centerY, scale);
            source.CalculatedZ += depthOrderOffset;
            source.Color = color;
            source.Opacity = opacity;
            source.TextureId = null;
            source.UseEffectRenderingPipeline = true;
            return source;
        }

        private static int ScaleCoordinate(int value, double center, float scale) =>
            (int)Math.Round(center + (value - center) * scale, MidpointRounding.AwayFromZero);

        private static string ApplyQualityColor(string? rawColor, GraphicsQualityPreset quality)
        {
            var (r, g, b) = ParseColor(rawColor);
            if (quality == GraphicsQualityPreset.Balanced)
                return ToHex(r, g, b);

            float brightness = quality == GraphicsQualityPreset.High ? 18f : -8f;
            float contrast = quality == GraphicsQualityPreset.High ? 1.12f : 0.9f;
            float saturation = quality == GraphicsQualityPreset.High ? 1.14f : 0.82f;
            float gray = r * 0.299f + g * 0.587f + b * 0.114f;
            float adjustedR = ((gray + (r - gray) * saturation - 128f) * contrast) + 128f + brightness;
            float adjustedG = ((gray + (g - gray) * saturation - 128f) * contrast) + 128f + brightness;
            float adjustedB = ((gray + (b - gray) * saturation - 128f) * contrast) + 128f + brightness;
            return ToHex(ClampByte(adjustedR), ClampByte(adjustedG), ClampByte(adjustedB));
        }

        private static string BoostGlowColor(string color, string? partName)
        {
            var (r, g, b) = ParseColor(color);
            float boost = string.Equals(partName, "ExplodingPart", StringComparison.Ordinal) ? 1.35f : 1.6f;
            return ToHex(
                ClampByte(r * boost + 22f),
                ClampByte(g * boost + 22f),
                ClampByte(b * boost + 22f));
        }

        private static (byte R, byte G, byte B) ParseColor(string? raw)
        {
            string color = ProjectedTriangleRenderMath.NormalizeColor(raw);
            if (color.Length < 6)
                color = ProjectedTriangleRenderMath.NormalizeColor(
                    RenderColorShading.GetShadeOfColorFromNormal(1f, color));

            if (color.Length < 6 ||
                !byte.TryParse(color.AsSpan(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte r) ||
                !byte.TryParse(color.AsSpan(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte g) ||
                !byte.TryParse(color.AsSpan(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out byte b))
                return (0, 0, 0);

            return (r, g, b);
        }

        private static string ToHex(byte r, byte g, byte b) => $"{r:x2}{g:x2}{b:x2}";

        private static byte ClampByte(float value) => (byte)Math.Clamp((int)value, 0, 255);

        private static byte GetGlowAlpha(string? partName, bool outer)
        {
            if (partName != null && partName.StartsWith("Lazer_", StringComparison.Ordinal)) return outer ? (byte)86 : (byte)145;
            if (string.Equals(partName, "ExplodingPart", StringComparison.Ordinal)) return outer ? (byte)72 : (byte)122;
            if (string.Equals(partName, "Particle", StringComparison.Ordinal)) return outer ? (byte)58 : (byte)96;
            if (string.Equals(partName, "MuzzleFlash", StringComparison.Ordinal)) return outer ? (byte)92 : (byte)158;
            if (string.Equals(partName, "LightningBolts", StringComparison.Ordinal)) return outer ? (byte)78 : (byte)132;
            if (string.Equals(partName, "ShieldGlow", StringComparison.Ordinal)) return outer ? (byte)70 : (byte)118;
            return outer ? (byte)76 : (byte)128;
        }

        private static float GetGlowScale(string? partName, bool outer)
        {
            if (partName != null && partName.StartsWith("Lazer_", StringComparison.Ordinal)) return outer ? 2.9f : 1.85f;
            if (string.Equals(partName, "LightningBolts", StringComparison.Ordinal)) return outer ? 2.4f : 1.65f;
            if (string.Equals(partName, "ExplodingPart", StringComparison.Ordinal)) return outer ? 1.45f : 1.22f;
            if (string.Equals(partName, "Particle", StringComparison.Ordinal)) return outer ? 2.0f : 1.42f;
            if (string.Equals(partName, "MuzzleFlash", StringComparison.Ordinal)) return outer ? 2.35f : 1.55f;
            if (string.Equals(partName, "ShieldGlow", StringComparison.Ordinal)) return outer ? 1.55f : 1.24f;
            return outer ? 2.1f : 1.45f;
        }
    }
}
