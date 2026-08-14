using System;

namespace RetroMesh.Engine
{
    public static class RenderPipelineMarkers
    {
        public const string ExplodingPartName = "ExplodingPart";
        public const string ParticlePartName = "Particle";
        public const string ParticleShadowPartName = "ParticleShadow";
        public const string MuzzleFlashPartName = "MuzzleFlash";

        public static bool IsDynamicEffectPartName(string? partName)
        {
            return string.Equals(partName, ExplodingPartName, StringComparison.Ordinal)
                || string.Equals(partName, ParticlePartName, StringComparison.Ordinal)
                || string.Equals(partName, ParticleShadowPartName, StringComparison.Ordinal)
                || string.Equals(partName, MuzzleFlashPartName, StringComparison.Ordinal);
        }

        public static bool IsDynamicParticleObjectName(string? objectName)
        {
            return string.Equals(objectName, ParticlePartName, StringComparison.Ordinal)
                || string.Equals(objectName, ParticleShadowPartName, StringComparison.Ordinal);
        }

        public static bool ShouldUseEffectRenderingPipeline(string? objectName, string? partName)
        {
            return IsDynamicEffectPartName(partName)
                || IsDynamicParticleObjectName(objectName);
        }
    }
}
