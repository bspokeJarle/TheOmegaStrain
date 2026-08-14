using System;
using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public sealed class PerspectiveProjectionPipeline<TTriangle>
        where TTriangle : IProjectedTriangle
    {
        private const double DebugCrashBoxScreenMargin = 0.05;
        private static readonly (int, int, int)[] CrashBoxFaceTriangles =
        {
            (0, 1, 2), (0, 2, 3),
            (4, 6, 5), (4, 7, 6),
            (4, 5, 1), (4, 1, 0),
            (3, 2, 6), (3, 6, 7),
            (0, 3, 7), (0, 7, 4),
            (1, 5, 6), (1, 6, 2)
        };

        private readonly IProjectionViewport viewport;
        private readonly Func<TTriangle> triangleFactory;

        public PerspectiveProjectionPipeline(
            IProjectionViewport viewport,
            Func<TTriangle> triangleFactory)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
            this.triangleFactory = triangleFactory ?? throw new ArgumentNullException(nameof(triangleFactory));
        }

        public int EstimateTriangleCapacity(
            IReadOnlyList<IRenderable3dObject> objects,
            Func<IRenderable3dObject, bool>? includeObject = null,
            Func<IRenderable3dObject, bool>? includeCrashBoxDebug = null)
        {
            int expectedCapacity = 0;
            int includedObjectCount = 0;
            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
            {
                var obj = objects[objectIndex];
                if (obj == null)
                    continue;

                if (includeObject != null && !includeObject(obj))
                    continue;

                includedObjectCount++;

                var parts = obj.ObjectParts;
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    var part = parts[partIndex];
                    if (!part.IsVisible)
                        continue;

                    expectedCapacity += part.Triangles.Count;
                }

                if (includeCrashBoxDebug?.Invoke(obj) == true && obj.CrashBoxes != null)
                    expectedCapacity += obj.CrashBoxes.Count * 12;
            }

            return Math.Max(expectedCapacity, includedObjectCount * 2);
        }

        public void ConvertObjectTo2d(
            IRenderable3dObject obj,
            double objPosX,
            double objPosY,
            double objPosZ,
            IList<TTriangle> result)
        {
            var parts = obj.ObjectParts;
            float objectOffsetsZ = obj.ObjectOffsets?.z ?? 0f;
            string? objectName = obj.ObjectName;
            float zSortBias = obj.ZSortBias;

            for (int partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var part = parts[partIndex];
                if (!part.IsVisible)
                    continue;

                var triangles = part.Triangles;
                for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
                {
                    var triangle = triangles[triangleIndex];
                    var normal = triangle.normal1;
                    if (normal.z <= 0 && !(triangle.noHidden ?? false))
                        continue;

                    var v1 = triangle.vert1;
                    var v2 = triangle.vert2;
                    var v3 = triangle.vert3;

                    var (x1, y1) = ProjectVertex(v1, objPosX, objPosY, objPosZ);
                    var (x2, y2) = ProjectVertex(v2, objPosX, objPosY, objPosZ);
                    var (x3, y3) = ProjectVertex(v3, objPosX, objPosY, objPosZ);

                    if (double.IsNaN(x1) || double.IsNaN(x2) || double.IsNaN(x3))
                        continue;

                    double xFactor = (x1 + x2 + x3) / 3;
                    double yFactor = (y1 + y2 + y3) / 3;

                    if (!ProjectionMath.IsOnScreen(xFactor, yFactor, viewport))
                        continue;

                    var projected = triangleFactory();
                    projected.X1 = Convert.ToInt32(x1);
                    projected.Y1 = Convert.ToInt32(y1);
                    projected.X2 = Convert.ToInt32(x2);
                    projected.Y2 = Convert.ToInt32(y2);
                    projected.X3 = Convert.ToInt32(x3);
                    projected.Y3 = Convert.ToInt32(y3);
                    projected.CalculatedZ = (float)(((v1.z + v2.z + v3.z) / 3f) + objectOffsetsZ - objPosZ) + zSortBias;
                    projected.Normal = normal.z;
                    projected.TriangleAngle = triangle.angle;
                    projected.Color = triangle.Color ?? string.Empty;
                    projected.PartName = part.PartName ?? string.Empty;
                    projected.UseEffectRenderingPipeline = RenderPipelineMarkers.ShouldUseEffectRenderingPipeline(objectName, part.PartName);
                    result.Add(projected);
                }
            }
        }

        public void ConvertCrashBoxesTo2d(
            IRenderable3dObject obj,
            double objPosX,
            double objPosY,
            double objPosZ,
            IList<TTriangle> result)
        {
            if (obj.CrashBoxes == null)
                return;

            for (int boxIndex = 0; boxIndex < obj.CrashBoxes.Count; boxIndex++)
            {
                var crashBox = obj.CrashBoxes[boxIndex];
                if (crashBox.Count != 8)
                    continue;

                var corners = MeshGeometryOperations.GenerateAabbCrashBoxFromRotated(
                    crashBox,
                    static (x, y, z) => new EngineVector3(x, y, z));

                foreach (var (i1, i2, i3) in CrashBoxFaceTriangles)
                {
                    var p1 = ProjectVertex(corners[i1], objPosX, objPosY, objPosZ);
                    var p2 = ProjectVertex(corners[i2], objPosX, objPosY, objPosZ);
                    var p3 = ProjectVertex(corners[i3], objPosX, objPosY, objPosZ);

                    if (!ProjectionMath.TryClampTriangleToViewport(
                            ref p1,
                            ref p2,
                            ref p3,
                            viewport,
                            DebugCrashBoxScreenMargin))
                    {
                        continue;
                    }

                    var projected = triangleFactory();
                    projected.X1 = (int)p1.x;
                    projected.Y1 = (int)p1.y;
                    projected.X2 = (int)p2.x;
                    projected.Y2 = (int)p2.y;
                    projected.X3 = (int)p3.x;
                    projected.Y3 = (int)p3.y;
                    projected.Color = "FF00FF";
                    projected.Normal = 1;
                    projected.PartName = $"CrashBox-{obj.ObjectName}";
                    result.Add(projected);
                }
            }
        }

        private (double x, double y) ProjectVertex(
            IVector3 vertex,
            double objPosX,
            double objPosY,
            double objPosZ)
        {
            return ProjectionMath.TryProjectVertex(vertex, objPosX, objPosY, objPosZ, viewport, out var screenPoint)
                ? screenPoint
                : (double.NaN, double.NaN);
        }
    }
}
