using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Helpers
{
    public static class ExplosionParticleHelpers
    {
        private const int DefaultExplosionThrust = 6;

        public static void ReleaseExplosionParticles(I3dObject theObject, IObjectMovement parentMovement, int thrust = DefaultExplosionThrust)
        {
            if (theObject.Particles == null) return;
            if (!TryCreateExplosionGuides(theObject, out var start, out var guide)) return;

            theObject.Particles.ReleaseParticles(
                guide,
                start,
                ToVector3(theObject.WorldPosition),
                parentMovement,
                Math.Max(1, thrust),
                true);

            MoveParticles(theObject);
        }

        public static void MoveParticles(I3dObject theObject)
        {
            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();
        }

        private static bool TryCreateExplosionGuides(I3dObject theObject, out ITriangleMeshWithColor start, out ITriangleMeshWithColor guide)
        {
            start = null!;
            guide = null!;

            var points = new List<IVector3>();
            CollectPoints(theObject, visibleOnly: true, points);

            if (points.Count == 0)
                CollectPoints(theObject, visibleOnly: false, points);

            if (points.Count == 0)
                return false;

            var center = GetCenter(points);
            start = CreatePointTriangle(center);
            guide = CreatePointTriangle(new Vector3 { x = center.x, y = center.y - 1f, z = center.z });
            return true;
        }

        private static void CollectPoints(I3dObject theObject, bool visibleOnly, List<IVector3> points)
        {
            foreach (var part in theObject.ObjectParts)
            {
                if (visibleOnly && !part.IsVisible) continue;
                if (part.PartName?.Contains("Guide", StringComparison.OrdinalIgnoreCase) == true) continue;
                if (part.Triangles == null) continue;

                foreach (var triangle in part.Triangles)
                {
                    points.Add(triangle.vert1);
                    points.Add(triangle.vert2);
                    points.Add(triangle.vert3);
                }
            }
        }

        private static Vector3 GetCenter(List<IVector3> points)
        {
            float x = 0f;
            float y = 0f;
            float z = 0f;

            foreach (var point in points)
            {
                x += point.x;
                y += point.y;
                z += point.z;
            }

            float count = points.Count;
            return new Vector3 { x = x / count, y = y / count, z = z / count };
        }

        private static ITriangleMeshWithColor CreatePointTriangle(Vector3 point)
        {
            return new TriangleMeshWithColor
            {
                Color = "ffffff",
                noHidden = true,
                vert1 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert2 = new Vector3 { x = point.x, y = point.y, z = point.z },
                vert3 = new Vector3 { x = point.x, y = point.y, z = point.z }
            };
        }

        private static Vector3 ToVector3(IVector3? vector)
        {
            if (vector == null) return new Vector3();
            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }
    }
}
