using _3dRotations.Helpers;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Projection
{
    public class PerspectiveWorldProjector : IWorldProjector<_3dObject, ProjectedTriangleMesh>
    {
        private readonly bool enableLogging = false;
        private readonly IProjectionViewport viewport;
        private const double DebugCrashBoxScreenMargin = 0.05;
        private long CurrentFrame = 0;

        public PerspectiveWorldProjector() : this(new ScreenSetupProjectionViewport())
        {
        }

        public PerspectiveWorldProjector(IProjectionViewport viewport)
        {
            this.viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
        }

        public List<ProjectedTriangleMesh> ProjectToTriangles(List<_3dObject> inhabitants, long? currentFrame)
        {
            return ProjectToTriangles(inhabitants, currentFrame, null);
        }

        public List<ProjectedTriangleMesh> ProjectToTriangles(
            List<_3dObject> inhabitants,
            long? currentFrame,
            List<ProjectedTriangleMesh>? reusableResult)
        {
            //If available use global framecounter
            if (currentFrame > 0) CurrentFrame = (long)currentFrame;
            var screenCoordinates = reusableResult ?? new List<ProjectedTriangleMesh>(inhabitants.Count * 2);
            screenCoordinates.Clear();

            int expectedCapacity = EstimateTriangleCapacity(inhabitants);
            if (screenCoordinates.Capacity < expectedCapacity)
                screenCoordinates.Capacity = expectedCapacity;

            foreach (var obj in inhabitants)
            {
                if (obj == null || (obj.ObjectName != "Star" && !obj.CheckInhabitantVisibility())) continue;

                if (!ObjectPlacementHelpers.TryGetRenderPosition(obj, viewport.ScreenCenterX, viewport.ScreenCenterY, out double screenX, out double screenY, out double screenZ))
                    continue;

                //Standard 3d Rendring
                ConvertObjectTo2d(obj, screenX, screenY, screenZ, screenCoordinates);

                if (obj.CrashBoxDebugMode != null && (bool)obj.CrashBoxDebugMode)
                {
                    //Debug visualization of crashboxes
                    ConvertCrashBoxesTo2d(obj, screenX, screenY, screenZ, screenCoordinates);
                }
            }

            return screenCoordinates;
        }

        private static int EstimateTriangleCapacity(List<_3dObject> inhabitants)
        {
            int expectedCapacity = 0;
            foreach (var obj in inhabitants)
            {
                if (obj == null || (obj.ObjectName != "Star" && !obj.CheckInhabitantVisibility()))
                    continue;

                var parts = obj.ObjectParts;
                for (int partIndex = 0; partIndex < parts.Count; partIndex++)
                {
                    var part = parts[partIndex];
                    if (!part.IsVisible)
                        continue;

                    expectedCapacity += part.Triangles.Count;
                }

                if (obj.CrashBoxDebugMode == true && obj.CrashBoxes != null)
                {
                    expectedCapacity += obj.CrashBoxes.Count * 12;
                }
            }

            return Math.Max(expectedCapacity, inhabitants.Count * 2);
        }

        //This method is for debugging av crashboxes only
        private void ConvertCrashBoxesTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<ProjectedTriangleMesh> result)
        {
            for (int boxIndex = 0; boxIndex < obj.CrashBoxes.Count; boxIndex++)
            {
                var crashBox = obj.CrashBoxes[boxIndex];
                // Skip if not a valid 8-corner box
                if (crashBox.Count != 8) continue;

                var corners = _3dObjectHelpers.GenerateAabbCrashBoxFromRotated(crashBox);

                var faceTriangles = new (int, int, int)[]
                {
                    // Front face
                    (0, 1, 2), (0, 2, 3),
                    // Back face
                    (4, 6, 5), (4, 7, 6),
                    // Top face
                    (4, 5, 1), (4, 1, 0),
                    // Bottom face
                    (3, 2, 6), (3, 6, 7),
                    // Left face
                    (0, 3, 7), (0, 7, 4),
                    // Right face
                    (1, 5, 6), (1, 6, 2)
                };

                foreach (var (i1, i2, i3) in faceTriangles)
                {
                    var p1 = ProjectVertex((Vector3)corners[i1], objPosX, objPosY, objPosZ);
                    var p2 = ProjectVertex((Vector3)corners[i2], objPosX, objPosY, objPosZ);
                    var p3 = ProjectVertex((Vector3)corners[i3], objPosX, objPosY, objPosZ);

                    if (!TryClampDebugCrashBoxTriangle(ref p1, ref p2, ref p3))
                        continue;

                    var triangle = CreateCrashBoxTriangle(p1, p2, p3, "FF00FF", obj); // Magenta for visibility
                    result.Add(triangle);
                }
            }
        }

        private bool TryClampDebugCrashBoxTriangle(
            ref (double x, double y) p1,
            ref (double x, double y) p2,
            ref (double x, double y) p3)
        {
            return ProjectionMath.TryClampTriangleToViewport(
                ref p1,
                ref p2,
                ref p3,
                viewport,
                DebugCrashBoxScreenMargin);
        }

        // Creating Triangles for rendring the CrashBoxes for debugging purposes
        private ProjectedTriangleMesh CreateCrashBoxTriangle((double x, double y) p1, (double x, double y) p2, (double x, double y) p3, string color, _3dObject obj)
        {
            return new ProjectedTriangleMesh
            {
                X1 = (int)p1.x,
                Y1 = (int)p1.y,
                X2 = (int)p2.x,
                Y2 = (int)p2.y,
                X3 = (int)p3.x,
                Y3 = (int)p3.y,
                Color = color,
                Normal = 1,
                PartName = $"CrashBox-{obj.ObjectName}"
            };
        }

        private void ConvertObjectTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<ProjectedTriangleMesh> result)
        {
            var parts = obj.ObjectParts;
            var objectOffsets = obj.ObjectOffsets;
            var objectOffsetsZ = objectOffsets.z;
            var objectName = obj.ObjectName;
            var zSortBias = obj.ZSortBias;

            for (int partIndex = 0; partIndex < parts.Count; partIndex++)
            {
                var part = parts[partIndex];
                if (!part.IsVisible) continue;

                var triangles = part.Triangles;
                for (int triangleIndex = 0; triangleIndex < triangles.Count; triangleIndex++)
                {
                    var triangle = triangles[triangleIndex];
                    var normal = triangle.normal1;
                    if (normal.z <= 0 && !(triangle.noHidden ?? false)) continue;

                    var v1 = (Vector3)triangle.vert1;
                    var v2 = (Vector3)triangle.vert2;
                    var v3 = (Vector3)triangle.vert3;

                    var (x1, y1) = ProjectVertex(v1, objPosX, objPosY, objPosZ);
                    var (x2, y2) = ProjectVertex(v2, objPosX, objPosY, objPosZ);
                    var (x3, y3) = ProjectVertex(v3, objPosX, objPosY, objPosZ);

                    if (double.IsNaN(x1) || double.IsNaN(x2) || double.IsNaN(x3))
                    {
                        continue;
                    }

                    double xFactor = (x1 + x2 + x3) / 3;
                    double yFactor = (y1 + y2 + y3) / 3;

                    if (!IsOnScreen(xFactor, yFactor)) continue;

                    //Debugging Object sorting issues for specific objects
                    if (Logger.ShouldLog(enableLogging) && (objectName == "Seeder" || objectName == "Lazer"))
                    {
                        Logger.Log($"Converted 3D object '{objectName}' to 2D. CalculatedZ: {(float)((float)(((v1.z + v2.z + v3.z) / 3) + objectOffsetsZ) - objPosZ)}");
                    }
                    result.Add(new ProjectedTriangleMesh
                    {
                        X1 = Convert.ToInt32(x1),
                        Y1 = Convert.ToInt32(y1),
                        X2 = Convert.ToInt32(x2),
                        Y2 = Convert.ToInt32(y2),
                        X3 = Convert.ToInt32(x3),
                        Y3 = Convert.ToInt32(y3),
                        CalculatedZ = (float)((float)(((v1.z + v2.z + v3.z) / 3) + objectOffsetsZ) - objPosZ) + zSortBias,
                        Normal = normal.z,
                        TriangleAngle = triangle.angle,
                        Color = triangle.Color,
                        PartName = part.PartName,
                        UseEffectRenderingPipeline = ShouldUseEffectRenderingPipeline(objectName, part.PartName)
                    });
                }
            }
        }

        private static bool ShouldUseEffectRenderingPipeline(string? objectName, string? partName)
        {
            return RenderPipelineMarkers.ShouldUseEffectRenderingPipeline(objectName, partName);
        }

        private (double x, double y) ProjectVertex(Vector3 v, double objPosX, double objPosY, double objPosZ)
        {
            return ProjectionMath.TryProjectVertex(v, objPosX, objPosY, objPosZ, viewport, out var screenPoint)
                ? screenPoint
                : (double.NaN, double.NaN);
        }

        private bool IsOnScreen(double x, double y)
        {
            return ProjectionMath.IsOnScreen(x, y, viewport);
        }

        private sealed class ScreenSetupProjectionViewport : IProjectionViewport
        {
            public int ScreenWidth => ScreenSetup.screenSizeX;
            public int ScreenHeight => ScreenSetup.screenSizeY;
            public int ScreenCenterX => ScreenWidth / 2;
            public int ScreenCenterY => ScreenHeight / 2;
            public double PerspectiveAdjustment => ScreenSetup.perspectiveAdjustment;
            public double ObjectZoom => ScreenSetup.defaultObjectZoom;
        }
    }
}
