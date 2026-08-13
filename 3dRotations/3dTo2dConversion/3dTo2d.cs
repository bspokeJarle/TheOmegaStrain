using _3dTesting._Coordinates;
using _3dTesting.Helpers;
using CommonUtilities._3DHelpers;
using CommonUtilities.CommonSetup;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dTesting._3dRotation
{
    public class _3dTo2d : IWorldProjector<_3dObject, _2dTriangleMesh>
    {
        private readonly bool enableLogging = false;
        private static int screenCenterX => ScreenSetup.screenSizeX / 2;
        private static int screenCenterY => ScreenSetup.screenSizeY / 2;
        private const double DebugCrashBoxScreenMargin = 0.05;
        private long CurrentFrame = 0;

        public List<_2dTriangleMesh> ConvertTo2dFromObjects(List<_3dObject> inhabitants, long? currentFrame)
        {
            return ConvertTo2dFromObjects(inhabitants, currentFrame, null);
        }

        public List<_2dTriangleMesh> ConvertTo2dFromObjects(
            List<_3dObject> inhabitants,
            long? currentFrame,
            List<_2dTriangleMesh>? reusableResult)
        {
            //If available use global framecounter
            if (currentFrame > 0) CurrentFrame = (long)currentFrame;
            var screenCoordinates = reusableResult ?? new List<_2dTriangleMesh>(inhabitants.Count * 2);
            screenCoordinates.Clear();

            int expectedCapacity = EstimateTriangleCapacity(inhabitants);
            if (screenCoordinates.Capacity < expectedCapacity)
                screenCoordinates.Capacity = expectedCapacity;

            foreach (var obj in inhabitants)
            {
                if (obj == null || (obj.ObjectName != "Star" && !obj.CheckInhabitantVisibility())) continue;

                if (!ObjectPlacementHelpers.TryGetRenderPosition(obj, screenCenterX, screenCenterY, out double screenX, out double screenY, out double screenZ))
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
        private void ConvertCrashBoxesTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<_2dTriangleMesh> result)
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

        private static bool TryClampDebugCrashBoxTriangle(
            ref (double x, double y) p1,
            ref (double x, double y) p2,
            ref (double x, double y) p3)
        {
            if (double.IsNaN(p1.x) || double.IsNaN(p1.y) ||
                double.IsNaN(p2.x) || double.IsNaN(p2.y) ||
                double.IsNaN(p3.x) || double.IsNaN(p3.y))
                return false;

            double minX = -(ScreenSetup.screenSizeX * DebugCrashBoxScreenMargin);
            double maxX = ScreenSetup.screenSizeX * (1 + DebugCrashBoxScreenMargin);
            double minY = -(ScreenSetup.screenSizeY * DebugCrashBoxScreenMargin);
            double maxY = ScreenSetup.screenSizeY * (1 + DebugCrashBoxScreenMargin);

            if ((p1.x < minX && p2.x < minX && p3.x < minX) ||
                (p1.x > maxX && p2.x > maxX && p3.x > maxX) ||
                (p1.y < minY && p2.y < minY && p3.y < minY) ||
                (p1.y > maxY && p2.y > maxY && p3.y > maxY))
                return false;

            p1 = ClampDebugPoint(p1, minX, maxX, minY, maxY);
            p2 = ClampDebugPoint(p2, minX, maxX, minY, maxY);
            p3 = ClampDebugPoint(p3, minX, maxX, minY, maxY);
            return true;
        }

        private static (double x, double y) ClampDebugPoint(
            (double x, double y) point,
            double minX,
            double maxX,
            double minY,
            double maxY)
        {
            return (
                Math.Clamp(point.x, minX, maxX),
                Math.Clamp(point.y, minY, maxY)
            );
        }

        // Creating Triangles for rendring the CrashBoxes for debugging purposes
        private _2dTriangleMesh CreateCrashBoxTriangle((double x, double y) p1, (double x, double y) p2, (double x, double y) p3, string color, _3dObject obj)
        {
            return new _2dTriangleMesh
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

        private void ConvertObjectTo2d(_3dObject obj, double objPosX, double objPosY, double objPosZ, List<_2dTriangleMesh> result)
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
                    result.Add(new _2dTriangleMesh
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
            return TriangleRenderPipelineMarkers.IsDynamicEffectPartName(partName)
                || string.Equals(objectName, "Particle", StringComparison.Ordinal)
                || string.Equals(objectName, "ParticleShadow", StringComparison.Ordinal);
        }

        private (double x, double y) ProjectVertex(Vector3 v, double objPosX, double objPosY, double objPosZ)
        {
            double denom = -v.z + objPosZ + ScreenSetup.perspectiveAdjustment;

            // If the point is on or behind the 'camera plane' -> do not render it.
            // (denom <= 0 means we cross the perspective plane and factor would flip)
            if (denom <= 1.0) // 1.0 as small safety margin
            {
                return (double.NaN, double.NaN);
            }

            double factor = ScreenSetup.perspectiveAdjustment / denom;

            double x = (v.x * factor * ScreenSetup.defaultObjectZoom) + objPosX;
            double y = (v.y * factor * ScreenSetup.defaultObjectZoom) + objPosY;
            return (x, y);
        }

        private static bool IsOnScreen(double x, double y)
        {
            return x >= -(ScreenSetup.screenSizeX * 0.2) && x <= (ScreenSetup.screenSizeX * 1.2)
                && y >= -(ScreenSetup.screenSizeY * 0.2) && y <= (ScreenSetup.screenSizeY * 1.2);
        }
    }
}
