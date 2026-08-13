using System;

namespace Domain
{
    public readonly struct GroundShadowProjectionOptions
    {
        public float ShadowSize { get; init; }
        public float BaseProjectedScale { get; init; }
        public float MinProjectedScale { get; init; }
        public float AltitudeShrinkFactor { get; init; }
        public float AltitudeProjection { get; init; }
        public float MaxAltitudeForProjection { get; init; }
        public float ShadowLift { get; init; }
        public float ShadowSlopeX { get; init; }
        public float ShadowSlopeY { get; init; }
        public float SurfaceTiltDegrees { get; init; }
    }

    public readonly record struct GroundShadowProjectionResult(
        EngineVector3 Vertex1,
        EngineVector3 Vertex2,
        EngineVector3 Vertex3,
        float Scale,
        float RawAltitude,
        float ClampedAltitude);

    public static class GroundShadowProjectionMath
    {
        public static GroundShadowProjectionResult ProjectTriangleShadow(
            float particleScreenY,
            float groundScreenY,
            float groundLocalX,
            float groundLocalY,
            float groundLocalZ,
            GroundShadowProjectionOptions options)
        {
            float tiltRadians = options.SurfaceTiltDegrees * MathF.PI / 180f;
            float tiltCos = MathF.Cos(tiltRadians);
            float tiltSin = MathF.Sin(tiltRadians);

            float altitudeRaw = MathF.Max(0f, groundScreenY - particleScreenY);
            float altitude = MathF.Min(altitudeRaw, options.MaxAltitudeForProjection);
            float scale = MathF.Max(
                options.MinProjectedScale,
                options.BaseProjectedScale - altitudeRaw * options.AltitudeShrinkFactor);

            float projectedX = altitude * options.ShadowSlopeX * options.AltitudeProjection;
            float projectedY = altitude * options.ShadowSlopeY * options.AltitudeProjection;

            float anchorX = groundLocalX + projectedX;
            float anchorY = groundLocalY + projectedY * tiltCos - options.ShadowLift;
            float anchorZ = groundLocalZ + projectedY * tiltSin;

            float halfSize = options.ShadowSize * scale;
            return new GroundShadowProjectionResult(
                new EngineVector3(anchorX - halfSize, anchorY, anchorZ),
                new EngineVector3(anchorX + halfSize, anchorY, anchorZ),
                new EngineVector3(anchorX, anchorY + halfSize * tiltCos, anchorZ + halfSize * tiltSin),
                scale,
                altitudeRaw,
                altitude);
        }
    }
}
