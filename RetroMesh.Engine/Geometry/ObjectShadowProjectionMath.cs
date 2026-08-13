namespace Domain
{
    public readonly struct ObjectShadowProjectionOptions
    {
        public float ShadowBaseX { get; init; }
        public float ShadowBaseY { get; init; }
        public float ShadowBaseZ { get; init; }
        public float ShadowOffsetX { get; init; }
        public float ShadowOffsetY { get; init; }
        public float ShadowOffsetZ { get; init; }
        public float Scale { get; init; }
        public float ShadowSlopeX { get; init; }
        public float ShadowSlopeY { get; init; }
        public float VertexStretchBoost { get; init; }
        public float SurfaceTiltDegrees { get; init; }
    }

    public readonly record struct ObjectShadowProjectionResult(
        EngineVector3 Vertex1,
        EngineVector3 Vertex2,
        EngineVector3 Vertex3);

    public static class ObjectShadowProjectionMath
    {
        public static ObjectShadowProjectionResult ProjectModelTriangleShadow(
            ITriangleMeshWithColor triangle,
            ObjectShadowProjectionOptions options)
        {
            float tiltRadians = options.SurfaceTiltDegrees * MathF.PI / 180f;
            float tiltCos = MathF.Cos(tiltRadians);
            float tiltSin = MathF.Sin(tiltRadians);
            float stretchX = options.ShadowSlopeX * options.VertexStretchBoost;
            float stretchY = options.ShadowSlopeY * options.VertexStretchBoost;

            return new ObjectShadowProjectionResult(
                ProjectVertex(triangle.vert1, options, stretchX, stretchY, tiltCos, tiltSin),
                ProjectVertex(triangle.vert2, options, stretchX, stretchY, tiltCos, tiltSin),
                ProjectVertex(triangle.vert3, options, stretchX, stretchY, tiltCos, tiltSin));
        }

        private static EngineVector3 ProjectVertex(
            IVector3 vertex,
            ObjectShadowProjectionOptions options,
            float stretchX,
            float stretchY,
            float tiltCos,
            float tiltSin)
        {
            float projectedX = vertex.x + vertex.z * stretchX;
            float projectedY = vertex.y + vertex.z * stretchY;
            float scaledX = projectedX * options.Scale;
            float scaledY = projectedY * options.Scale;

            return new EngineVector3(
                options.ShadowBaseX + scaledX + options.ShadowOffsetX,
                options.ShadowBaseY + scaledY * tiltCos + options.ShadowOffsetY,
                options.ShadowBaseZ + scaledY * tiltSin + options.ShadowOffsetZ);
        }
    }
}
