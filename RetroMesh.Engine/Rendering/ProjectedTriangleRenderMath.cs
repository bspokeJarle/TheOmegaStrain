using System;
using System.Collections.Generic;

namespace Domain
{
    public readonly struct ProjectedTriangleRenderOptions
    {
        public bool GlowEffectsEnabled { get; init; }
        public bool EnhancedShadowsEnabled { get; init; }
        public bool HighGraphicsQuality { get; init; }
        public Predicate<string?>? IsGlowCandidatePartName { get; init; }
        public Predicate<string?>? IsEnhancedShadowCandidatePartName { get; init; }
    }

    public static class ProjectedTriangleRenderMath
    {
        public const string CrashBoxPartPrefix = "CrashBox-";

        public static bool IsInsideRenderDepth(float calculatedZ, float nearZ, float farZ)
        {
            return calculatedZ >= nearZ && calculatedZ <= farZ;
        }

        public static int CullTrianglesOutsideRenderDepth<TTriangle>(
            List<TTriangle> triangles,
            float nearZ,
            float farZ)
            where TTriangle : IProjectedTriangle
        {
            int writeIndex = 0;
            for (int readIndex = 0; readIndex < triangles.Count; readIndex++)
            {
                var triangle = triangles[readIndex];
                if (!IsInsideRenderDepth(triangle.CalculatedZ, nearZ, farZ))
                    continue;

                if (writeIndex != readIndex)
                    triangles[writeIndex] = triangle;

                writeIndex++;
            }

            if (writeIndex < triangles.Count)
                triangles.RemoveRange(writeIndex, triangles.Count - writeIndex);

            return writeIndex;
        }

        public static void SortTrianglesByDepth<TTriangle>(List<TTriangle> triangles)
            where TTriangle : IProjectedTriangle
        {
            triangles.Sort(static (a, b) => a.CalculatedZ.CompareTo(b.CalculatedZ));
        }

        public static float GetTriangleShadeKey<TTriangle>(
            TTriangle triangle,
            float nearZ,
            float farZ,
            Predicate<string?>? isDepthOnlyShadePartName = null)
            where TTriangle : IProjectedTriangle
        {
            return RenderShadeMath.GetTriangleShadeKey(
                triangle.CalculatedZ,
                triangle.TriangleAngle,
                nearZ,
                farZ,
                isDepthOnlyShadePartName?.Invoke(triangle.PartName) == true);
        }

        public static bool ShouldUseEffectRenderingPipeline<TTriangle>(
            TTriangle triangle,
            ProjectedTriangleRenderOptions options)
            where TTriangle : IProjectedTriangle
        {
            string? partName = triangle.PartName;
            return triangle.UseEffectRenderingPipeline ||
                   RenderPipelineMarkers.IsDynamicEffectPartName(partName) ||
                   ShouldRenderEnhancedShadow(partName, options) ||
                   ShouldRenderGlow(partName, options);
        }

        public static bool ShouldRenderAsSeparateTriangle(string? partName)
        {
            return RenderPipelineMarkers.IsDynamicEffectPartName(partName);
        }

        public static bool IsExplodingPartName(string? partName)
        {
            return string.Equals(partName, RenderPipelineMarkers.ExplodingPartName, StringComparison.Ordinal);
        }

        public static bool IsCrashBoxPartName(string? partName)
        {
            return partName != null && partName.StartsWith(CrashBoxPartPrefix, StringComparison.Ordinal);
        }

        public static int CountCrashBoxParts(IReadOnlyList<string?> partNames)
        {
            int count = 0;
            for (int i = 0; i < partNames.Count; i++)
            {
                if (IsCrashBoxPartName(partNames[i]))
                    count++;
            }

            return count;
        }

        public static string NormalizeColor(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
                return "000000";

            var normalized = raw.Trim();
            if (normalized.Length > 0 && normalized[0] == '#')
                normalized = normalized.Substring(1);

            return normalized.ToLowerInvariant();
        }

        private static bool ShouldRenderEnhancedShadow(string? partName, ProjectedTriangleRenderOptions options)
        {
            return options.HighGraphicsQuality &&
                   options.EnhancedShadowsEnabled &&
                   options.IsEnhancedShadowCandidatePartName?.Invoke(partName) == true;
        }

        private static bool ShouldRenderGlow(string? partName, ProjectedTriangleRenderOptions options)
        {
            return options.GlowEffectsEnabled &&
                   options.IsGlowCandidatePartName?.Invoke(partName) == true;
        }
    }
}
