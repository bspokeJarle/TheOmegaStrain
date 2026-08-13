using _3dTesting._Coordinates;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Media;

namespace _3dTesting.Rendering
{
    public class WorldRenderer : IProjectedTriangleRenderer<_2dTriangleMesh>
    {
        private readonly DrawingVisualHost visualHost;
        private readonly DrawingVisual visual = new DrawingVisual();
        private readonly bool _localLoggingEnabled =  false;
        private const bool enableRenderPerfLogging = true;
        private static int RenderPerfLogInterval => ScreenSetup.RuntimeTargetFps;

        private int renderingTriangleCount = 0;
        private long renderFrameCount = 0;
        private double averageRenderMs = 0;
        private double averageCullMs = 0;
        private double averageSortMs = 0;
        private double averageDrawMs = 0;

        private readonly Dictionary<(float ShadeKey, string BaseColor, GraphicsQualityPreset Quality), Color> colorCache = new();
        private readonly Dictionary<Color, SolidColorBrush> brushCache = new();
        private readonly Dictionary<Color, Pen> penCache = new();
        private readonly Dictionary<int, SolidColorBrush> backgroundBrushCache = new();
        private readonly Dictionary<(Color Color, byte Alpha), SolidColorBrush> alphaBrushCache = new();
        private readonly Dictionary<string, string> _normalizedColorCache = new();

        private readonly List<StreamGeometry> geometryPool = new();
        private int geometryPoolIndex = 0;

        private const float FarZ = ScreenSetup.RenderFarZ;
        private const float NearZ = ScreenSetup.RenderNearZ;

        public int GetRenderingTriangleCount() => renderingTriangleCount;

        public WorldRenderer(DrawingVisualHost host)
        {
            visualHost = host;
  
            if (ShouldLog())
            {
                int tier = (RenderCapability.Tier >> 16);
                string tierText = tier switch
                {
                    2 => "Render Tier 2 - Full hardware acceleration",
                    1 => "Render Tier 1 - Partial hardware acceleration",
                    0 => "Render Tier 0 - Software rendering only",
                    _ => $"Unknown Render Tier ({tier})"
                };

                Logger.Log($"[WorldRenderer] WPF Render Tier: {tierText}");
                if (tier < 2)
                    Logger.Log("[WorldRenderer] ⚠️ Performance Warning: Not using full hardware acceleration!");
            }

            PrewarmColorCache();
        }

        private bool ShouldLog() => Logger.ShouldLog(_localLoggingEnabled);

        private void PrewarmColorCache()
        {
            string[] commonColors = ["red", "green", "blue", "gray", "white", "black", "yellow", "orange", "brown"];
            int generatedCount = 0;

            // Choose how dense you want the prewarm. 100 steps -> about 101 * 9 = 909 entries before rounding/cache hits.
            const int steps = 500;
            float stepSize = (FarZ - NearZ) / steps;

            for (int i = 0; i <= steps; i++)
            {
                float calculatedZ = NearZ + (i * stepSize);

                // Map to 0..1 using the new helper
                float factor01 = GetDepthFactor01(calculatedZ);

                // Quantize for stable cache keys (same concept as runtime shading key)
                float roundedFactor01 = (float)Math.Round(factor01, 2, MidpointRounding.AwayFromZero);

                foreach (var baseColor in commonColors)
                {
                    string normalized = baseColor.ToLowerInvariant();

                    foreach (var quality in new[] { GraphicsQualityPreset.Balanced, GraphicsQualityPreset.High, GraphicsQualityPreset.Low })
                    {
                        var cacheKey = (roundedFactor01, normalized, quality);
                        if (colorCache.ContainsKey(cacheKey))
                            continue;

                        try
                        {
                            // NOTE: We keep the call signature the same for now,
                            // since you said you're aligned with renaming later.
                            Color color = (Color)ColorConverter.ConvertFromString(
                                Helpers.Colors.getShadeOfColorFromNormal(roundedFactor01, normalized));
                            color = ApplyGraphicsQualityColor(color, roundedFactor01, quality);

                            colorCache[cacheKey] = color;

                            if (!brushCache.ContainsKey(color))
                            {
                                SolidColorBrush brush = new SolidColorBrush(color);
                                brush.Freeze();
                                brushCache[color] = brush;
                            }

                            if (!penCache.ContainsKey(color))
                            {
                                Pen pen = new Pen(brushCache[color], 1);
                                pen.Freeze();
                                penCache[color] = pen;
                            }

                            generatedCount++;
                        }
                        catch (Exception ex)
                        {
                            if (ShouldLog())
                                Logger.Log($"[WorldRenderer] ⚠️ Failed to prewarm color (factor={roundedFactor01:0.00}, baseColor={baseColor}, calcZ={calculatedZ:0.00}): {ex.Message}");
                        }
                    }
                }
            }

            if (ShouldLog())
                Logger.Log($"[WorldRenderer] ✅ Prewarmed {generatedCount} colors + brushes + pens.");
        }


        public void RenderTriangles(List<_2dTriangleMesh> screenCoordinates)
        {
            bool logRenderTiming = Logger.ShouldLog(enableRenderPerfLogging);
            long renderStartTicks = logRenderTiming ? Stopwatch.GetTimestamp() : 0;
            long phaseTicks = renderStartTicks;
            double cullMs = 0;
            double sortMs = 0;
            double prepMs = 0;
            double drawMs = 0;
            double addVisualMs = 0;

            double MarkPhase()
            {
                if (!logRenderTiming)
                    return 0;

                long now = Stopwatch.GetTimestamp();
                double elapsedMs = TicksToMs(now - phaseTicks);
                phaseTicks = now;
                return elapsedMs;
            }

            CullTrianglesOutsideRenderDepth(screenCoordinates);
            cullMs = MarkPhase();

            renderingTriangleCount = screenCoordinates.Count;
            screenCoordinates.Sort((a, b) => a.CalculatedZ.CompareTo(b.CalculatedZ));
            sortMs = MarkPhase();

            geometryPoolIndex = 0;

            bool trackStats = ShouldLog();
            int colorHits = 0, colorMisses = 0;
            int brushHits = 0, brushMisses = 0;
            int penHits = 0, penMisses = 0;
            int drawCalls = 0;
            prepMs = MarkPhase();

            using (var dc = visual.RenderOpen())
            {
                dc.DrawRectangle(GetBackgroundBrush(), null, new Rect(0, 0, ScreenSetup.screenSizeX, ScreenSetup.screenSizeY));

                StreamGeometry? batchGeometry = null;
                StreamGeometryContext? batchContext = null;
                SolidColorBrush? batchBrush = null;
                Pen? batchPen = null;

                void FlushBatch()
                {
                    if (batchGeometry == null || batchContext == null || batchBrush == null || batchPen == null)
                    {
                        return;
                    }

                    batchContext.Close();
                    batchContext = null;
                    dc.DrawGeometry(batchBrush, batchPen, batchGeometry);
                    drawCalls++;
                }

                foreach (var triangle in screenCoordinates)
                {
                    if (ShouldUseEffectRenderingPipeline(triangle))
                    {
                        FlushBatch();
                        drawCalls += DrawEffectTriangle(dc, triangle);
                        batchGeometry = null;
                        batchContext = null;
                        batchBrush = null;
                        batchPen = null;
                        continue;
                    }

                    float shadeKey = GetTriangleShadeKey(triangle);
                    string baseColor = NormalizeColorCached(triangle.Color);
                    var quality = GetRenderQuality();
                    var colorCacheKey = (shadeKey, baseColor, quality);

                    if (!colorCache.TryGetValue(colorCacheKey, out Color color))
                    {
                        if (ShouldLog()) Logger.Log($"[WorldRenderer] ⚠️ Color cache miss for key ({shadeKey}, {baseColor}). CalculatedZ:{triangle.CalculatedZ} Angle:{triangle.TriangleAngle:0.00}");

                        string hex = Helpers.Colors.getShadeOfColorFromNormal(shadeKey, baseColor);

                        color = ApplyGraphicsQualityColor(HexToColor(hex), shadeKey, quality);
                        colorCache[colorCacheKey] = color;
                        if (trackStats) colorMisses++;
                    }
                    else if (trackStats)
                    {
                        colorHits++;
                    }

                    if (!brushCache.TryGetValue(color, out SolidColorBrush brush))
                    {
                        brush = new SolidColorBrush(color);
                        brush.Freeze();
                        brushCache[color] = brush;
                        if (trackStats) brushMisses++;
                    }
                    else if (trackStats)
                    {
                        brushHits++;
                    }

                    if (!penCache.TryGetValue(color, out Pen pen))
                    {
                        pen = new Pen(brush, 1);
                        pen.Freeze();
                        penCache[color] = pen;
                        if (trackStats) penMisses++;
                    }
                    else if (trackStats)
                    {
                        penHits++;
                    }

                    var effectiveBrush = IsCrashBoxPartName(triangle.PartName)
                        ? CreateCrashBoxBrush(brush.Color)
                        : brush;

                    if (!IsSameBatch(batchBrush, batchPen, effectiveBrush, pen))
                    {
                        FlushBatch();
                        batchGeometry = GetNextGeometry();
                        batchContext = batchGeometry.Open();
                        batchBrush = effectiveBrush;
                        batchPen = pen;
                    }

                    AddTriangleFigure(batchContext, triangle);
                }

                FlushBatch();
            }
            drawMs = MarkPhase();

            visualHost.AddVisual(visual);
            addVisualMs = MarkPhase();

            if (logRenderTiming)
            {
                renderFrameCount++;
                double totalMs = TicksToMs(Stopwatch.GetTimestamp() - renderStartTicks);
                averageRenderMs += (totalMs - averageRenderMs) / renderFrameCount;
                averageCullMs += (cullMs - averageCullMs) / renderFrameCount;
                averageSortMs += (sortMs - averageSortMs) / renderFrameCount;
                averageDrawMs += (drawMs - averageDrawMs) / renderFrameCount;

                if (renderFrameCount % RenderPerfLogInterval == 0)
                {
                    Logger.Log(
                        $"[RenderPerf] frame={renderFrameCount} triangles={renderingTriangleCount} drawCalls={drawCalls} totalMs={totalMs:0.###} cullMs={cullMs:0.###} sortMs={sortMs:0.###} prepMs={prepMs:0.###} drawMs={drawMs:0.###} addVisualMs={addVisualMs:0.###} " +
                        $"avgRenderMs={averageRenderMs:0.###} avgCullMs={averageCullMs:0.###} avgSortMs={averageSortMs:0.###} avgDrawMs={averageDrawMs:0.###}");
                }
            }

            if (trackStats)
            {
                Logger.Log($"[WorldRenderer] Caching stats - Colors: {colorHits} hits / {colorMisses} misses, " +
                           $"Brushes: {brushHits} hits / {brushMisses} misses, Pens: {penHits} hits / {penMisses} misses, DrawCalls: {drawCalls}");
            }
        }

        public static int CullTrianglesOutsideRenderDepth(List<_2dTriangleMesh> triangles)
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < triangles.Count; readIndex++)
            {
                var triangle = triangles[readIndex];
                if (triangle.CalculatedZ > FarZ || triangle.CalculatedZ < NearZ)
                    continue;

                if (writeIndex != readIndex)
                    triangles[writeIndex] = triangle;

                writeIndex++;
            }

            if (writeIndex < triangles.Count)
                triangles.RemoveRange(writeIndex, triangles.Count - writeIndex);

            return writeIndex;
        }

        public static int ProcessTrianglesForRender(
            List<_2dTriangleMesh> triangles,
            Dictionary<(float, string), Color> colorCache,
            Dictionary<Color, SolidColorBrush> brushCache,
            Dictionary<Color, Pen> penCache)
        {
            int processed = 0;

            for (int i = 0; i < triangles.Count; i++)
            {
                var triangle = triangles[i];

                if (triangle.CalculatedZ > FarZ || triangle.CalculatedZ < NearZ)
                    continue;

                float depthFactor01 = GetDepthFactor01(triangle.CalculatedZ);
                float angleFactor01 = NormalizeAngleTo01(triangle.TriangleAngle);

                float combinedFactor01 = Math.Clamp(angleFactor01 * depthFactor01, 0f, 1f);
                if (triangle.PartName != null && triangle.PartName.Contains("Star_Core"))
                    combinedFactor01 = depthFactor01;

                string? baseColor = triangle.Color;
                if (string.IsNullOrWhiteSpace(baseColor))
                {
                    baseColor = "000000";
                }
                else
                {
                    baseColor = baseColor.Trim();
                    if (baseColor.Length > 0 && baseColor[0] == '#')
                        baseColor = baseColor.Substring(1);
                    baseColor = baseColor.ToLowerInvariant();
                }

                float shadeKey = (float)Math.Round(combinedFactor01, 2, MidpointRounding.AwayFromZero);

                if (!colorCache.TryGetValue((shadeKey, baseColor), out Color color))
                {
                    string hex = Helpers.Colors.getShadeOfColorFromNormal(shadeKey, baseColor);
                    color = HexToColor(hex);
                    colorCache[(shadeKey, baseColor)] = color;
                }

                if (!brushCache.TryGetValue(color, out SolidColorBrush brush))
                {
                    brush = new SolidColorBrush(color);
                    brush.Freeze();
                    brushCache[color] = brush;
                }

                if (!penCache.TryGetValue(color, out Pen pen))
                {
                    pen = new Pen(brush, 1);
                    pen.Freeze();
                    penCache[color] = pen;
                }

                processed++;
            }

            return processed;
        }

        public static bool IsCrashBoxPartName(string? partName)
        {
            return partName != null && partName.StartsWith("CrashBox-", StringComparison.Ordinal);
        }

        public static bool ShouldRenderAsSeparateTriangle(string? partName)
        {
            return IsDynamicEffectPartName(partName);
        }

        public static bool ShouldUseEffectRenderingPipeline(_2dTriangleMesh triangle)
        {
            return triangle.UseEffectRenderingPipeline ||
                   IsDynamicEffectPartName(triangle.PartName) ||
                   ShouldRenderEnhancedShadow(triangle.PartName) ||
                   (GameState.SettingsState?.GlowEffectsEnabled == true && IsGlowCandidatePartName(triangle.PartName));
        }

        public static bool IsExplodingPartName(string? partName)
        {
            return string.Equals(partName, "ExplodingPart", StringComparison.Ordinal);
        }

        public static bool IsDynamicEffectPartName(string? partName)
        {
            return TriangleRenderPipelineMarkers.IsDynamicEffectPartName(partName);
        }

        public static bool IsGlowCandidatePartName(string? partName)
        {
            if (string.IsNullOrWhiteSpace(partName))
                return false;

            return partName.StartsWith("Lazer_", StringComparison.Ordinal) ||
                   string.Equals(partName, "PowerUpBody", StringComparison.Ordinal) ||
                   string.Equals(partName, "TravelSpeedPowerUpBody", StringComparison.Ordinal) ||
                   string.Equals(partName, "BulletBody", StringComparison.Ordinal) ||
                   string.Equals(partName, "MotherShipWeakSpot", StringComparison.Ordinal) ||
                   string.Equals(partName, "FrontCannonMuzzle", StringComparison.Ordinal) ||
                   string.Equals(partName, "MuzzleFlash", StringComparison.Ordinal) ||
                   string.Equals(partName, "DecoyFrontPulsePanel", StringComparison.Ordinal) ||
                   partName.StartsWith("CannonChargeRing", StringComparison.Ordinal) ||
                   string.Equals(partName, "Particle", StringComparison.Ordinal) ||
                   string.Equals(partName, "LightningBolts", StringComparison.Ordinal) ||
                   string.Equals(partName, "ExplodingPart", StringComparison.Ordinal);
        }

        public static bool IsEnhancedShadowCandidatePartName(string? partName)
        {
            return string.Equals(partName, "Shadow", StringComparison.Ordinal) ||
                   string.Equals(partName, "ParticleShadow", StringComparison.Ordinal);
        }

        public static int CountCrashBoxParts(string[] partNames)
        {
            int count = 0;
            for (int i = 0; i < partNames.Length; i++)
            {
                if (IsCrashBoxPartName(partNames[i]))
                {
                    count++;
                }
            }
            return count;
        }

        private static readonly Dictionary<Color, SolidColorBrush> CrashBoxBrushCache = new();

        public static SolidColorBrush CreateCrashBoxBrush(Color baseColor)
        {
            if (CrashBoxBrushCache.TryGetValue(baseColor, out var cached))
            {
                return cached;
            }

            var transparentBrush = new SolidColorBrush(baseColor) { Opacity = 0.25 };
            transparentBrush.Freeze();
            CrashBoxBrushCache[baseColor] = transparentBrush;
            return transparentBrush;
        }

        public static void ClearCrashBoxBrushCache()
        {
            CrashBoxBrushCache.Clear();
        }

        private SolidColorBrush GetBackgroundBrush()
        {
            var weather = GameState.WeatherVisualState;
            float lightning = weather?.LightningFlashIntensity ?? 0f;
            float impact = weather?.ImpactFlashIntensity ?? 0f;
            float intensity = Math.Max(lightning, impact);
            if (intensity <= 0.005f)
                return Brushes.Black;

            int key = Math.Clamp((int)MathF.Round(intensity * 16f), 0, 16);
            int warmthKey = Math.Clamp((int)MathF.Round(CalculateImpactWarmth(lightning, impact) * 16f), 0, 16);
            int cacheKey = key * 100 + warmthKey;
            if (backgroundBrushCache.TryGetValue(cacheKey, out var brush))
                return brush;

            float t = key / 16f;
            float warmth = warmthKey / 16f;

            byte lightningRed = (byte)(2 + 38 * t);
            byte lightningGreen = (byte)(4 + 56 * t);
            byte lightningBlue = (byte)(9 + 92 * t);

            byte impactRed = (byte)(12 + 120 * t);
            byte impactGreen = (byte)(4 + 54 * t);
            byte impactBlue = (byte)(2 + 20 * t);

            byte red = Mix(lightningRed, impactRed, warmth);
            byte green = Mix(lightningGreen, impactGreen, warmth);
            byte blue = Mix(lightningBlue, impactBlue, warmth);

            brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            backgroundBrushCache[cacheKey] = brush;
            return brush;
        }

        private StreamGeometry GetNextGeometry()
        {
            if (geometryPoolIndex >= geometryPool.Count)
                geometryPool.Add(new StreamGeometry());

            var geometry = geometryPool[geometryPoolIndex++];
            geometry.FillRule = FillRule.Nonzero;
            return geometry;
        }

        public static bool IsSameBatch(SolidColorBrush? currentBrush, Pen? currentPen, SolidColorBrush nextBrush, Pen nextPen)
        {
            return ReferenceEquals(currentBrush, nextBrush) && ReferenceEquals(currentPen, nextPen);
        }

        private int DrawEffectTriangle(DrawingContext dc, _2dTriangleMesh triangle)
        {
            Color color = GetCachedTriangleColor(triangle);
            int drawCalls = 0;

            if (ShouldRenderEnhancedShadow(triangle.PartName))
            {
                drawCalls += DrawSoftShadow(dc, triangle);
            }

            if (GameState.SettingsState?.GlowEffectsEnabled == true && IsGlowCandidatePartName(triangle.PartName))
            {
                drawCalls += DrawGlow(dc, triangle, color);
            }

            SolidColorBrush brush = GetCachedBrush(color);
            Pen pen = GetCachedPen(color, brush);
            StreamGeometry geometry = GetNextGeometry();

            using (var ctx = geometry.Open())
            {
                AddTriangleFigure(ctx, triangle);
            }

            dc.DrawGeometry(brush, pen, geometry);
            return drawCalls + 1;
        }

        private int DrawSoftShadow(DrawingContext dc, _2dTriangleMesh triangle)
        {
            DrawScaledTriangle(dc, triangle, Colors.Black, alpha: 70, scale: 1.28f);
            return 1;
        }

        private int DrawGlow(DrawingContext dc, _2dTriangleMesh triangle, Color color)
        {
            Color glowColor = BoostGlowColor(color, triangle.PartName);
            float outerScale = GetGlowScale(triangle.PartName, outer: true);
            float innerScale = GetGlowScale(triangle.PartName, outer: false);

            DrawScaledTriangle(dc, triangle, glowColor, GetGlowAlpha(triangle.PartName, outer: true), outerScale);
            DrawScaledTriangle(dc, triangle, glowColor, GetGlowAlpha(triangle.PartName, outer: false), innerScale);
            return 2;
        }

        private void DrawScaledTriangle(DrawingContext dc, _2dTriangleMesh triangle, Color color, byte alpha, float scale)
        {
            SolidColorBrush brush = GetCachedAlphaBrush(color, alpha);
            StreamGeometry geometry = GetNextGeometry();

            using (var ctx = geometry.Open())
            {
                AddScaledTriangleFigure(ctx, triangle, scale);
            }

            dc.DrawGeometry(brush, null, geometry);
        }

        private SolidColorBrush GetCachedAlphaBrush(Color color, byte alpha)
        {
            var key = (color, alpha);
            if (alphaBrushCache.TryGetValue(key, out var brush))
            {
                return brush;
            }

            brush = new SolidColorBrush(Color.FromArgb(alpha, color.R, color.G, color.B));
            brush.Freeze();
            alphaBrushCache[key] = brush;
            return brush;
        }

        private Color GetCachedTriangleColor(_2dTriangleMesh triangle)
        {
            float shadeKey = GetTriangleShadeKey(triangle);
            string baseColor = NormalizeColor(triangle.Color);
            var quality = GetRenderQuality();
            var cacheKey = (shadeKey, baseColor, quality);
            if (colorCache.TryGetValue(cacheKey, out Color color))
            {
                return color;
            }

            color = ApplyGraphicsQualityColor(HexToColor(Helpers.Colors.getShadeOfColorFromNormal(shadeKey, baseColor)), shadeKey, quality);
            colorCache[cacheKey] = color;
            return color;
        }

        private SolidColorBrush GetCachedBrush(Color color)
        {
            if (brushCache.TryGetValue(color, out SolidColorBrush brush))
            {
                return brush;
            }

            brush = new SolidColorBrush(color);
            brush.Freeze();
            brushCache[color] = brush;
            return brush;
        }

        private Pen GetCachedPen(Color color, SolidColorBrush brush)
        {
            if (penCache.TryGetValue(color, out Pen pen))
            {
                return pen;
            }

            pen = new Pen(brush, 1);
            pen.Freeze();
            penCache[color] = pen;
            return pen;
        }

        private static float GetTriangleShadeKey(_2dTriangleMesh triangle)
        {
            float depthFactor01 = GetDepthFactor01(triangle.CalculatedZ);
            float angleFactor01 = NormalizeAngleTo01(triangle.TriangleAngle);
            float combinedFactor01 = Math.Clamp(angleFactor01 * depthFactor01, 0f, 1f);

            if (triangle.PartName != null && triangle.PartName.Contains("Star_Core"))
            {
                combinedFactor01 = depthFactor01;
            }

            return (float)Math.Round(combinedFactor01, 2, MidpointRounding.AwayFromZero);
        }

        private static void AddTriangleFigure(StreamGeometryContext ctx, _2dTriangleMesh triangle)
        {
            var p1 = new Point(triangle.X1, triangle.Y1);
            var p2 = new Point(triangle.X2, triangle.Y2);
            var p3 = new Point(triangle.X3, triangle.Y3);

            ctx.BeginFigure(p1, true, true);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(p3, true, false);
        }

        private static void AddScaledTriangleFigure(StreamGeometryContext ctx, _2dTriangleMesh triangle, float scale)
        {
            double centerX = (triangle.X1 + triangle.X2 + triangle.X3) / 3.0;
            double centerY = (triangle.Y1 + triangle.Y2 + triangle.Y3) / 3.0;

            var p1 = ScalePoint(triangle.X1, triangle.Y1, centerX, centerY, scale);
            var p2 = ScalePoint(triangle.X2, triangle.Y2, centerX, centerY, scale);
            var p3 = ScalePoint(triangle.X3, triangle.Y3, centerX, centerY, scale);

            ctx.BeginFigure(p1, true, true);
            ctx.LineTo(p2, true, false);
            ctx.LineTo(p3, true, false);
        }

        private static Point ScalePoint(double x, double y, double centerX, double centerY, float scale)
        {
            return new Point(
                centerX + (x - centerX) * scale,
                centerY + (y - centerY) * scale);
        }

        private string NormalizeColorCached(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "000000";
            if (_normalizedColorCache.TryGetValue(raw, out var cached))
                return cached;
            var normalized = NormalizeColor(raw);
            _normalizedColorCache[raw] = normalized;
            return normalized;
        }

        private static string NormalizeColor(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "000000";

            var normalized = raw.Trim();
            if (normalized.Length > 0 && normalized[0] == '#')
                normalized = normalized.Substring(1);
            return normalized.ToLowerInvariant();
        }

        private static Color BoostGlowColor(Color color, string? partName)
        {
            float boost = string.Equals(partName, "ExplodingPart", StringComparison.Ordinal) ? 1.35f : 1.6f;
            return Color.FromRgb(
                ClampByte(color.R * boost + 22f),
                ClampByte(color.G * boost + 22f),
                ClampByte(color.B * boost + 22f));
        }

        private static byte GetGlowAlpha(string? partName, bool outer)
        {
            if (partName != null && partName.StartsWith("Lazer_", StringComparison.Ordinal))
                return outer ? (byte)86 : (byte)145;

            if (string.Equals(partName, "ExplodingPart", StringComparison.Ordinal))
                return outer ? (byte)72 : (byte)122;

            if (string.Equals(partName, "Particle", StringComparison.Ordinal))
                return outer ? (byte)58 : (byte)96;

            if (string.Equals(partName, "MuzzleFlash", StringComparison.Ordinal))
                return outer ? (byte)92 : (byte)158;

            if (string.Equals(partName, "LightningBolts", StringComparison.Ordinal))
                return outer ? (byte)78 : (byte)132;

            return outer ? (byte)76 : (byte)128;
        }

        private static float GetGlowScale(string? partName, bool outer)
        {
            if (partName != null && partName.StartsWith("Lazer_", StringComparison.Ordinal))
                return outer ? 2.9f : 1.85f;

            if (string.Equals(partName, "LightningBolts", StringComparison.Ordinal))
                return outer ? 2.4f : 1.65f;

            if (string.Equals(partName, "ExplodingPart", StringComparison.Ordinal))
                return outer ? 1.45f : 1.22f;

            if (string.Equals(partName, "Particle", StringComparison.Ordinal))
                return outer ? 2.0f : 1.42f;

            if (string.Equals(partName, "MuzzleFlash", StringComparison.Ordinal))
                return outer ? 2.35f : 1.55f;

            return outer ? 2.1f : 1.45f;
        }

        private static bool ShouldRenderEnhancedShadow(string? partName)
        {
            var settings = GameState.SettingsState;
            return settings != null &&
                   settings.GraphicsQuality == GraphicsQualityPreset.High &&
                   settings.EnhancedShadowsEnabled &&
                   IsEnhancedShadowCandidatePartName(partName);
        }

        private static GraphicsQualityPreset GetRenderQuality()
        {
            return GameState.SettingsState?.GraphicsQuality ?? GraphicsQualityPreset.Balanced;
        }

        private static Color ApplyGraphicsQualityColor(Color color, float shadeKey, GraphicsQualityPreset quality)
        {
            return quality switch
            {
                GraphicsQualityPreset.High => AdjustColor(color, brightness: 10f + shadeKey * 16f, contrast: 1.12f, saturation: 1.14f),
                GraphicsQualityPreset.Low => AdjustColor(color, brightness: -8f, contrast: 0.9f, saturation: 0.82f),
                _ => color
            };
        }

        private static Color AdjustColor(Color color, float brightness, float contrast, float saturation)
        {
            float r = color.R;
            float g = color.G;
            float b = color.B;
            float gray = r * 0.299f + g * 0.587f + b * 0.114f;

            r = gray + (r - gray) * saturation;
            g = gray + (g - gray) * saturation;
            b = gray + (b - gray) * saturation;

            r = ((r - 128f) * contrast) + 128f + brightness;
            g = ((g - 128f) * contrast) + 128f + brightness;
            b = ((b - 128f) * contrast) + 128f + brightness;

            return Color.FromRgb(ClampByte(r), ClampByte(g), ClampByte(b));
        }

        private static float CalculateImpactWarmth(float lightning, float impact)
        {
            float total = lightning + impact;
            return total <= 0.001f ? 0f : Math.Clamp(impact / total, 0f, 1f);
        }

        private static byte Mix(byte a, byte b, float t)
        {
            return ClampByte(a + (b - a) * Math.Clamp(t, 0f, 1f));
        }

        private static byte ClampByte(float value)
        {
            if (value <= 0f) return 0;
            if (value >= 255f) return 255;
            return (byte)value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float GetDepthFactor01(float calculatedZ)
        {
            float near = NearZ; // lav Z -> mørkere (0)
            float far = FarZ;  // høy Z -> lysere (1)

            if (calculatedZ <= near) return 0f;
            if (calculatedZ >= far) return 1f;

            return (calculatedZ - near) / (far - near);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float NormalizeAngleTo01(float angle)
        {
            // Angle is typically a dot product in [-1, 1]; map to [0, 1]
            float normalized = (angle + 1f) * 0.5f;
            return Math.Clamp(normalized, 0f, 1f);
        }

        /*private void DrawTriangle(DrawingContext dc, _2dTriangleMesh triangle, SolidColorBrush brush, Pen pen)
        {
            var p1 = new Point(triangle.X1, triangle.Y1);
            var p2 = new Point(triangle.X2, triangle.Y2);
            var p3 = new Point(triangle.X3, triangle.Y3);

            if (geometryPoolIndex >= geometryPool.Count)
                geometryPool.Add(new StreamGeometry());

            var geometry = geometryPool[geometryPoolIndex++];
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(p1, true, true);
                ctx.LineTo(p2, true, false);
                ctx.LineTo(p3, true, false);
            }

            if (triangle.PartName != null && triangle.PartName.StartsWith("CrashBox-"))
            {
                // If rendering CrashBoxes -> use semi-transparent brush
                var transparentBrush = new SolidColorBrush(brush.Color) { Opacity = 0.25 };
                transparentBrush.Freeze();
                dc.DrawGeometry(transparentBrush, pen, geometry);
            }
            else
            {
                // Normal rendering
                dc.DrawGeometry(brush, pen, geometry);
            }
        }*/

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Color HexToColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Black;
            if (hex[0] == '#') hex = hex.Substring(1);
            if (hex.Length < 6) return Colors.Black;

            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);

            return Color.FromArgb(255, r, g, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double TicksToMs(long ticks)
        {
            return ticks * 1000.0 / Stopwatch.Frequency;
        }
    }
}
