using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Ai;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class Lazer
    {
        private const float ZoomRatio = 1f;

        public static _3dObject CreateLazer(ISurface parentSurface, float scaleMultiplier = 1f, float crashBoxScaleMultiplier = 1f)
        {
            var seg1 = LazerSegment1(); // longest, bright
            var seg2 = LazerSegment2(); // medium, darker
            var seg3 = LazerSegment3(); // shortest, grey
            var crash = LazerCrashBoxes();

            var beam = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            if (seg1 == null || seg2 == null || seg3 == null) return beam;

            beam.ObjectParts.Add(new _3dObjectPart { PartName = "Lazer_Long_Bright", Triangles = seg1, IsVisible = true });
            beam.ObjectParts.Add(new _3dObjectPart { PartName = "Lazer_Mid_Darker", Triangles = seg2, IsVisible = true });
            beam.ObjectParts.Add(new _3dObjectPart { PartName = "Lazer_Short_Grey", Triangles = seg3, IsVisible = true });

            // Place/aim via ObjectOffsets when spawning (e.g., muzzle position).
            beam.ObjectOffsets = new Vector3 { };
            beam.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            beam.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            beam.Particles = new ParticlesAI();
            beam.ParentSurface = parentSurface;
            beam.ObjectName = "Lazer";
            beam.CrashBoxDebugMode = false;
            beam.ImpactStatus = new ImpactStatus { ObjectName = "Lazer" };
            if (crash != null) beam.CrashBoxes = crash;

            _3dObjectHelpers.ApplyScaleToObject(beam, ZoomRatio * scaleMultiplier);
            ScaleCrashBoxesAroundCenter(beam.CrashBoxes, crashBoxScaleMultiplier);

            return beam;
        }

        // AABB covering all three segments (local coords, configurable via WeaponSetup)
        public static List<List<IVector3>>? LazerCrashBoxes()
        {
            var min = new Vector3
            {
                x = WeaponSetup.LazerCrashBoxMinX,
                y = WeaponSetup.LazerCrashBoxMinY,
                z = WeaponSetup.LazerCrashBoxMinZ
            };
            var max = new Vector3
            {
                x = WeaponSetup.LazerCrashBoxMaxX,
                y = WeaponSetup.LazerCrashBoxMaxY,
                z = WeaponSetup.LazerCrashBoxMaxZ
            };

            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(min, max)
            };
        }

        private static void ScaleCrashBoxesAroundCenter(List<List<IVector3>>? crashBoxes, float scale)
        {
            if (crashBoxes == null || scale == 1f)
                return;

            foreach (var box in crashBoxes)
            {
                if (box == null || box.Count == 0)
                    continue;

                var minX = float.MaxValue;
                var minY = float.MaxValue;
                var minZ = float.MaxValue;
                var maxX = float.MinValue;
                var maxY = float.MinValue;
                var maxZ = float.MinValue;

                foreach (var point in box)
                {
                    if (point.x < minX) minX = point.x;
                    if (point.y < minY) minY = point.y;
                    if (point.z < minZ) minZ = point.z;
                    if (point.x > maxX) maxX = point.x;
                    if (point.y > maxY) maxY = point.y;
                    if (point.z > maxZ) maxZ = point.z;
                }

                var center = new Vector3
                {
                    x = (minX + maxX) * 0.5f,
                    y = (minY + maxY) * 0.5f,
                    z = (minZ + maxZ) * 0.5f
                };

                for (var i = 0; i < box.Count; i++)
                {
                    var point = box[i];
                    box[i] = new Vector3
                    {
                        x = center.x + (point.x - center.x) * scale,
                        y = center.y + (point.y - center.y) * scale,
                        z = center.z + (point.z - center.z) * scale
                    };
                }
            }
        }

        // === Geometry helpers (inline per segment to avoid external deps) ===
        // Build a triangular frustum (3-sided tube) between two Y-positions along -Y,
        // centered on X=0, Z=28, using X and Z as the cross-section axes.
        private static List<ITriangleMeshWithColor> BuildTriTube(float yStart, float yEnd, float rStart, float rEnd, string colorHex)
        {
            // Axis is exactly along -Y. Cross-section plane is XZ at z=28.
            // Use an equilateral triangle: angles 0°, 120°, 240°.
            float[] ca = { 1f, -0.5f, -0.5f };
            float s3 = 0.8660254f; // sqrt(3)/2
            float[] sa = { 0f, s3, -s3 };

            // Centers
            var C0 = new Vector3 { x = 0f, y = yStart, z = 28f };
            var C1 = new Vector3 { x = 0f, y = yEnd, z = 28f };

            // Rings: three points at each end (X along cos, Z along sin)
            var A0 = new Vector3[3];
            var A1 = new Vector3[3];
            for (int i = 0; i < 3; i++)
            {
                A0[i] = new Vector3 { x = C0.x + rStart * ca[i], y = C0.y, z = C0.z + rStart * sa[i] };
                A1[i] = new Vector3 { x = C1.x + rEnd * ca[i], y = C1.y, z = C1.z + rEnd * sa[i] };
            }

            // 3 side quads → 6 triangles (RHS): (A0[i], A0[j], A1[j]) and (A0[i], A1[j], A1[i])
            var tris = new List<ITriangleMeshWithColor>();
            for (int i = 0; i < 3; i++)
            {
                int j = (i + 1) % 3;
                tris.Add(new TriangleMeshWithColor { Color = colorHex, vert1 = A0[i], vert2 = A0[j], vert3 = A1[j] });
                tris.Add(new TriangleMeshWithColor { Color = colorHex, vert1 = A0[i], vert2 = A1[j], vert3 = A1[i] });
            }
            // No end caps for a beam look; add if you ever want closed geometry.
            return tris;
        }

        // Segment 1 — longest, bright; starts at the muzzle and goes far forward
        public static List<ITriangleMeshWithColor>? LazerSegment1()
        {
            // Length ~75; slight taper
            float yStart = -45f;  // at cannon muzzle
            float yEnd = -120f; // forward along -Y
            float r0 = 3.2f, r1 = 2.6f;
            return BuildTriTube(yStart, yEnd, r0, r1, "FF5555");
        }

        // Segment 2 — medium, darker
        public static List<ITriangleMeshWithColor>? LazerSegment2()
        {
            // Length ~50; taper continues
            float yStart = -120f;
            float yEnd = -170f;
            float r0 = 2.6f, r1 = 2.0f;
            return BuildTriTube(yStart, yEnd, r0, r1, "CC2222");
        }

        // Segment 3 — shortest, grey tail
        public static List<ITriangleMeshWithColor>? LazerSegment3()
        {
            // Length ~30; final taper
            float yStart = -170f;
            float yEnd = -200f;
            float r0 = 2.0f, r1 = 1.4f;
            return BuildTriTube(yStart, yEnd, r0, r1, "AAAAAA");
        }
    }
}
