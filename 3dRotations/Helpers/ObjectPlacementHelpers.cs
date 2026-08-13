using _3dRotations.World;
using CommonUtilities._3DHelpers;
using Domain;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Helpers
{
    public static class ObjectPlacementHelpers
    {
        public static bool EnablePlacementLogging = false;

        private static ITriangleMeshWithColor? GetSurfaceTriangle(_3dObject obj)
        {
            var surface = obj?.ParentSurface;
            if (surface == null || obj?.SurfaceBasedId == null)
                return null;

            if (surface.RotatedSurfaceTriangleByLandId.TryGetValue(obj.SurfaceBasedId.Value, out var cachedTriangle))
                return cachedTriangle;

            return surface.RotatedSurfaceTriangles
                .FirstOrDefault(t => t.landBasedPosition == obj.SurfaceBasedId);
        }

        public static (float x, float y, float z) GetCrashBoxCenter(List<List<IVector3>> crashBoxes)
        {
            var allPoints = crashBoxes.SelectMany(box => box).Cast<Vector3>().ToList();
            return (
                allPoints.Average(p => p.x),
                allPoints.Average(p => p.y),
                allPoints.Average(p => p.z)
            );
        }

        public static bool TryGetRenderPosition(_3dObject obj, int screenCenterX, int screenCenterY, out double x, out double y, out double z)
        {
            x = y = z = 0;
            if (obj == null) return false;

            // Calculates the local render/collision-sync position based on the world position.
            var localWorldPosition = obj.GetLocalWorldPosition();

            if (localWorldPosition == null)
            {
                if (obj.SurfaceBasedId > 0)
                {
                    var triangle = GetSurfaceTriangle(obj);
                    if (triangle == null) return false;

                    CenterObjectAt(obj, triangle.vert1);
                    CenterCrashBoxesAt(obj, triangle.vert1);
                    x = screenCenterX + obj.ObjectOffsets.x;
                    y = screenCenterY + obj.ObjectOffsets.y;
                    z = obj.ObjectOffsets.z;
                }
                else
                {
                    x = screenCenterX + obj.ObjectOffsets.x;
                    y = screenCenterY + obj.ObjectOffsets.y;
                    z = obj.ObjectOffsets.z;
                }
            }
            else
            {
                // Store the calculated local crash-sync offset used by crash detection.
                // Signs must match the screen-position formula (negate X/Y) so that
                // crash-box coordinates are in the same space as screen-space objects.
                obj.CalculatedCrashOffset = new Vector3
                {
                    x = -localWorldPosition.x + obj.ObjectOffsets!.x,
                    y = -localWorldPosition.y + obj.ObjectOffsets!.y,
                    z = localWorldPosition.z + obj.ObjectOffsets!.z
                };
                //Calculate screen position
                x = screenCenterX - localWorldPosition.x + obj.ObjectOffsets.x;
                y = screenCenterY - localWorldPosition.y + obj.ObjectOffsets.y;
                z = localWorldPosition.z + obj.ObjectOffsets.z;
            }

            return true;
        }

        public static void CenterObjectAt(I3dObject obj, IVector3 targetPosition)
        {
            if (obj == null || targetPosition == null)
                return;

            IVector3 objectCenter = obj.UseSurfaceFootprintPivot
                ? new Vector3()
                : GetObjectGeometricCenter(obj, true);

            float shiftX = targetPosition.x - objectCenter.x;
            float shiftY = targetPosition.y - objectCenter.y;
            float shiftZ = targetPosition.z - objectCenter.z;

            foreach (var part in obj.ObjectParts)
            {
                foreach (var triangle in part.Triangles)
                {
                    triangle.vert1.x += shiftX;
                    triangle.vert1.y += shiftY;
                    triangle.vert1.z += shiftZ;

                    triangle.vert2.x += shiftX;
                    triangle.vert2.y += shiftY;
                    triangle.vert2.z += shiftZ;

                    triangle.vert3.x += shiftX;
                    triangle.vert3.y += shiftY;
                    triangle.vert3.z += shiftZ;
                }
            }
        }

        public static void CenterCrashBoxesAt(_3dObject obj, IVector3 targetPosition)
        {
            if (obj?.CrashBoxes == null || obj.CrashBoxes.Count == 0 || targetPosition == null)
                return;

            var allPoints = obj.CrashBoxes.SelectMany(box => box).Cast<Vector3>().ToList();

            if (allPoints.Count == 0) return;

            var center = obj.UseSurfaceFootprintPivot
                ? new Vector3()
                : new Vector3
                {
                    x = allPoints.Average(p => p.x),
                    y = allPoints.Min(p => p.y),
                    z = allPoints.Average(p => p.z)
                };

            float shiftX = targetPosition.x - center.x;
            float shiftY = targetPosition.y - center.y;
            float shiftZ = targetPosition.z - center.z;

            for (int i = 0; i < obj.CrashBoxes.Count; i++)
            {
                for (int j = 0; j < obj.CrashBoxes[i].Count; j++)
                {
                    var p = (Vector3)obj.CrashBoxes[i][j];
                    obj.CrashBoxes[i][j] = new Vector3
                    {
                        x = p.x + shiftX,
                        y = p.y + shiftY,
                        z = p.z + shiftZ
                    };
                }
            }
        }

        public static IVector3 GetObjectGeometricCenter(I3dObject obj, bool snapToBottomY = false)
        {
            float sumX = 0, sumY = 0, sumZ = 0;
            int count = 0;
            float minY = float.MaxValue;

            foreach (var part in obj.ObjectParts)
            {
                foreach (var triangle in part.Triangles)
                {
                    var vertices = new[] { triangle.vert1, triangle.vert2, triangle.vert3 };

                    foreach (var v in vertices)
                    {
                        sumX += v.x;
                        sumY += v.y;
                        sumZ += v.z;
                        count++;

                        if (snapToBottomY)
                            minY = Math.Min(minY, v.y);
                    }
                }
            }

            if (count == 0) return new Vector3();

            if (snapToBottomY)
            {
                float bottomSumX = 0, bottomSumY = 0, bottomSumZ = 0;
                int bottomCount = 0;
                const float bottomTolerance = 0.001f;

                foreach (var part in obj.ObjectParts)
                {
                    foreach (var triangle in part.Triangles)
                    {
                        var vertices = new[] { triangle.vert1, triangle.vert2, triangle.vert3 };

                        foreach (var v in vertices)
                        {
                            if (Math.Abs(v.y - minY) > bottomTolerance)
                                continue;

                            bottomSumX += v.x;
                            bottomSumY += v.y;
                            bottomSumZ += v.z;
                            bottomCount++;
                        }
                    }
                }

                if (bottomCount > 0)
                {
                    return new Vector3
                    {
                        x = bottomSumX / bottomCount,
                        y = bottomSumY / bottomCount,
                        z = bottomSumZ / bottomCount
                    };
                }
            }

            return new Vector3
            {
                x = sumX / count,
                y = sumY / count,
                z = sumZ / count
            };
        }

        public static void CenterCrashBoxAt(List<Vector3> crashBox, IVector3 targetPosition, IVector3 crashboxOffsets)
        {
            if (crashBox == null || crashBox.Count == 0 || targetPosition == null) return;

            float minY = crashBox.Min(p => p.y);
            float shiftY = targetPosition.y - minY + crashboxOffsets.y;

            if (Logger.ShouldLog(EnablePlacementLogging))
            {
                Logger.Log("[CenterAt] Target Y: " + targetPosition.y + ", CrashBox MinY: " + minY + ", CrashBoxOffsetY: " + crashboxOffsets.y + ", Final ShiftY: " + shiftY);
            }

            for (int i = 0; i < crashBox.Count; i++)
            {
                crashBox[i] = new Vector3
                {
                    x = crashBox[i].x,
                    y = crashBox[i].y + shiftY,
                    z = crashBox[i].z
                };
            }
        }


        public static void LogCrashboxAnalysis(string label, List<Vector3> box)
        {
            if (!Logger.ShouldLog(EnablePlacementLogging) || box == null || box.Count == 0) return;

            float yMin = box.Min(p => p.y);
            float yMax = box.Max(p => p.y);
            float xMin = box.Min(p => p.x);
            float xMax = box.Max(p => p.x);
            float zMin = box.Min(p => p.z);
            float zMax = box.Max(p => p.z);

            // Average center (what you have today)
            var avgCenter = new Vector3
            {
                x = box.Average(p => p.x),
                y = box.Average(p => p.y),
                z = box.Average(p => p.z)
            };

            // AABB center (matches CrashDetection GetCenterOfBox)
            var aabbCenter = new Vector3
            {
                x = (xMin + xMax) / 2f,
                y = (yMin + yMax) / 2f,
                z = (zMin + zMax) / 2f
            };

            string F(float v) => v.ToString("0.00", CultureInfo.InvariantCulture);

            Logger.Log("--- " + label + " ---");
            Logger.Log("Y-range: [" + F(yMin) + "–" + F(yMax) + "], X-range: [" + F(xMin) + "–" + F(xMax) + "], Z-range: [" + F(zMin) + "–" + F(zMax) + "]");
            Logger.Log("Center(AABB): (x=" + F(aabbCenter.x) + ", y=" + F(aabbCenter.y) + ", z=" + F(aabbCenter.z) + ")");
            Logger.Log("Center(AVG):  (x=" + F(avgCenter.x) + ", y=" + F(avgCenter.y) + ", z=" + F(avgCenter.z) + ")");

            foreach (var p in box)
                Logger.Log("(x=" + F(p.x) + ", y=" + F(p.y) + ", z=" + F(p.z) + ")");

            Logger.Log("--- End of " + label + " ---\n");
        }
    }
}
