using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class AlienPlant
    {
        private const float ZoomRatio = 2f;
        private const float CrashboxSize = 1.15f;

        private static readonly Vector3 LargeCenter = new Vector3 { x = 0f, y = 0f, z = 10f };
        private static readonly Vector3 SmallCenter = new Vector3 { x = 0f, y = 0f, z = 6f };

        private static string baseDark = "3A2418";
        private static string baseMid = "5A3A24";

        private static string leafTeal = "23BFA3";
        private static string leafTealDark = "12796F";
        private static string leafGreen = "3BCF70";
        private static string leafGreenDark = "1F7D43";

        private static string petalPurple = "7E2AA8";
        private static string petalMagenta = "D936A4";
        private static string petalPink = "FF4FB8";
        private static string petalOrange = "FF8A1E";
        private static string petalYellow = "FFD04A";

        private static string stemPurple = "7A2FA3";
        private static string stemDark = "3C1752";
        private static string glowGold = "FFC247";

        // ----------------------------------------------------
        //  PUBLIC FACTORIES
        // ----------------------------------------------------

        public static _3dObject CreateLargeAlienPlant(ISurface parentSurface)
        {
            var plant = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "LargeAlienPlant",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -8, y = 0, z = -8 }
            };

            AddPart(plant, "AlienPlantBase", CreateBase(5.0f, 4.0f, LargeCenter), true);
            AddPart(plant, "AlienPlantOuterLeaves", CreateOuterLeaves(10, 24f, 7.0f, 4.0f, LargeCenter), true);
            AddPart(plant, "AlienPlantInnerPetals", CreateInnerPetals(11, 16f, 22f, LargeCenter), true);
            AddPart(plant, "AlienPlantStamens", CreateStamens(7, 26f, LargeCenter), true);

            plant.CrashBoxes = AlienPlantCrashBoxes(16f, 34f);
            plant.CrashBoxNames = new List<string?> { "AlienPlantBody" };

            _3dObjectHelpers.AddCustomShadowPart(plant, AlienPlantShadow(28f, 24f));
            _3dObjectHelpers.ApplyScaleToObject(plant, ZoomRatio);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(plant);

            return plant;
        }

        public static _3dObject CreateSmallAlienPlant(ISurface parentSurface)
        {
            var plant = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SmallAlienPlant",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -5, y = 0, z = -5 }
            };

            AddPart(plant, "AlienPlantBase", CreateBase(3.0f, 2.6f, SmallCenter), true);
            AddPart(plant, "AlienPlantOuterLeaves", CreateOuterLeaves(8, 14f, 4.5f, 2.8f, SmallCenter), true);
            AddPart(plant, "AlienPlantInnerPetals", CreateInnerPetals(8, 9f, 13f, SmallCenter), true);
            AddPart(plant, "AlienPlantStamens", CreateStamens(5, 15f, SmallCenter), true);

            plant.CrashBoxes = AlienPlantCrashBoxes(9f, 20f);
            plant.CrashBoxNames = new List<string?> { "AlienPlantBody" };

            _3dObjectHelpers.AddCustomShadowPart(plant, AlienPlantShadow(17f, 15f));
            _3dObjectHelpers.ApplyScaleToObject(plant, ZoomRatio);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(plant);

            return plant;
        }

        // ----------------------------------------------------
        //  BASE
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateBase(float radius, float height, Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();
            int segments = 8;

            var bottom = CreateRing(0f, 0f, 0f, radius, segments);
            var top = CreateRing(0f, 0f, height, radius * 0.72f, segments);

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                string color = i % 2 == 0 ? baseMid : baseDark;

                AddQuadOutward(tris, bottom[i], bottom[next], top[next], top[i], center, color);
            }

            var topCenter = new Vector3 { x = 0f, y = 0f, z = height + 0.4f };

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                tris.Add(CreateTriangleOutward(topCenter, top[next], top[i], center, baseDark));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  OUTER LEAVES
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateOuterLeaves(
            int count,
            float length,
            float width,
            float crownZ,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int i = 0; i < count; i++)
            {
                float angle = i * MathF.PI * 2f / count;
                float wave = MathF.Sin(i * 1.37f) * 1.8f;

                float dx = MathF.Cos(angle);
                float dy = MathF.Sin(angle);
                float sx = -dy;
                float sy = dx;

                float localLength = length * (0.88f + 0.22f * MathF.Sin(i * 1.8f));
                float localWidth = width * (0.75f + 0.25f * MathF.Cos(i * 1.2f));

                var root = new Vector3
                {
                    x = dx * 2.2f,
                    y = dy * 2.2f,
                    z = crownZ
                };

                var mid = new Vector3
                {
                    x = dx * localLength * 0.52f + sx * wave,
                    y = dy * localLength * 0.52f + sy * wave,
                    z = crownZ + 3.0f
                };

                var tip = new Vector3
                {
                    x = dx * localLength + sx * wave * 1.4f,
                    y = dy * localLength + sy * wave * 1.4f,
                    z = crownZ - 4.8f
                };

                var rootL = new Vector3 { x = root.x + sx * localWidth * 0.45f, y = root.y + sy * localWidth * 0.45f, z = root.z - 0.2f };
                var rootR = new Vector3 { x = root.x - sx * localWidth * 0.45f, y = root.y - sy * localWidth * 0.45f, z = root.z - 0.2f };

                var midL = new Vector3 { x = mid.x + sx * localWidth, y = mid.y + sy * localWidth, z = mid.z - 0.8f };
                var midR = new Vector3 { x = mid.x - sx * localWidth, y = mid.y - sy * localWidth, z = mid.z - 0.8f };

                string c1 = i % 3 == 0 ? leafTeal : i % 3 == 1 ? leafGreen : leafTealDark;
                string c2 = i % 2 == 0 ? leafGreenDark : leafTealDark;

                tris.Add(CreateTriangleOutward(rootL, mid, midL, center, c1));
                tris.Add(CreateTriangleOutward(rootR, midR, mid, center, c2));
                tris.Add(CreateTriangleOutward(midL, mid, tip, center, c1));
                tris.Add(CreateTriangleOutward(mid, midR, tip, center, c2));

                // Fold underside for thickness.
                var underside = new Vector3 { x = mid.x, y = mid.y, z = mid.z - 1.8f };
                tris.Add(CreateTriangleOutward(rootL, underside, rootR, center, leafTealDark));
                tris.Add(CreateTriangleOutward(midL, tip, underside, center, leafGreenDark));
                tris.Add(CreateTriangleOutward(midR, underside, tip, center, leafTealDark));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  INNER PETALS
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateInnerPetals(
            int count,
            float radius,
            float height,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int i = 0; i < count; i++)
            {
                float angle = i * MathF.PI * 2f / count;

                float dx = MathF.Cos(angle);
                float dy = MathF.Sin(angle);
                float sx = -dy;
                float sy = dx;

                float localHeight = height * (0.86f + 0.22f * MathF.Sin(i * 1.6f));
                float localRadius = radius * (0.65f + 0.25f * MathF.Cos(i * 1.3f));
                float width = 3.3f + 1.0f * MathF.Sin(i * 0.9f);

                var root = new Vector3
                {
                    x = dx * 3.2f,
                    y = dy * 3.2f,
                    z = 4.2f
                };

                var mid = new Vector3
                {
                    x = dx * localRadius * 0.55f,
                    y = dy * localRadius * 0.55f,
                    z = localHeight * 0.58f
                };

                var tip = new Vector3
                {
                    x = dx * localRadius * 0.25f,
                    y = dy * localRadius * 0.25f,
                    z = localHeight
                };

                var rootL = new Vector3 { x = root.x + sx * width * 0.45f, y = root.y + sy * width * 0.45f, z = root.z };
                var rootR = new Vector3 { x = root.x - sx * width * 0.45f, y = root.y - sy * width * 0.45f, z = root.z };

                var midL = new Vector3 { x = mid.x + sx * width, y = mid.y + sy * width, z = mid.z };
                var midR = new Vector3 { x = mid.x - sx * width, y = mid.y - sy * width, z = mid.z };

                string c1 = i % 4 == 0 ? petalPink : i % 4 == 1 ? petalMagenta : i % 4 == 2 ? petalPurple : petalOrange;
                string c2 = i % 2 == 0 ? petalPurple : petalOrange;

                tris.Add(CreateTriangleOutward(rootL, mid, midL, center, c1));
                tris.Add(CreateTriangleOutward(rootR, midR, mid, center, c2));
                tris.Add(CreateTriangleOutward(midL, mid, tip, center, c1));
                tris.Add(CreateTriangleOutward(mid, midR, tip, center, c2));

                // Central ridge gives each petal a low-poly fold.
                tris.Add(CreateTriangleOutward(rootL, rootR, mid, center, c2));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  STAMENS / GLOWING TIPS
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateStamens(int count, float height, Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int i = 0; i < count; i++)
            {
                float angle = i * MathF.PI * 2f / count;
                float dx = MathF.Cos(angle);
                float dy = MathF.Sin(angle);

                float lean = 4f + 2f * MathF.Sin(i * 1.9f);
                float baseZ = 7f;
                float topZ = height + MathF.Sin(i * 1.2f) * 3f;

                var start = new Vector3
                {
                    x = dx * 2f,
                    y = dy * 2f,
                    z = baseZ
                };

                var end = new Vector3
                {
                    x = dx * lean,
                    y = dy * lean,
                    z = topZ
                };

                AddStem(tris, start, end, 0.45f, center);

                AddGlowBud(tris, end, 1.6f, center);
            }

            return tris;
        }

        private static void AddStem(List<ITriangleMeshWithColor> tris, Vector3 start, Vector3 end, float thickness, Vector3 center)
        {
            float dx = end.x - start.x;
            float dy = end.y - start.y;
            float len = MathF.Sqrt(dx * dx + dy * dy);

            float nx = len < 0.001f ? 1f : -dy / len;
            float ny = len < 0.001f ? 0f : dx / len;

            var a = new Vector3 { x = start.x + nx * thickness, y = start.y + ny * thickness, z = start.z };
            var b = new Vector3 { x = start.x - nx * thickness, y = start.y - ny * thickness, z = start.z };
            var c = new Vector3 { x = end.x - nx * thickness, y = end.y - ny * thickness, z = end.z };
            var d = new Vector3 { x = end.x + nx * thickness, y = end.y + ny * thickness, z = end.z };

            AddQuadOutward(tris, a, b, c, d, center, stemPurple);

            var a2 = new Vector3 { x = a.x, y = a.y, z = a.z - thickness };
            var d2 = new Vector3 { x = d.x, y = d.y, z = d.z - thickness };

            AddQuadOutward(tris, a2, d2, c, b, center, stemDark);
        }

        private static void AddGlowBud(List<ITriangleMeshWithColor> tris, Vector3 c, float r, Vector3 center)
        {
            var top = new Vector3 { x = c.x, y = c.y, z = c.z + r };
            var bottom = new Vector3 { x = c.x, y = c.y, z = c.z - r };
            var left = new Vector3 { x = c.x, y = c.y - r, z = c.z };
            var right = new Vector3 { x = c.x, y = c.y + r, z = c.z };
            var front = new Vector3 { x = c.x + r, y = c.y, z = c.z };
            var back = new Vector3 { x = c.x - r, y = c.y, z = c.z };

            tris.Add(CreateTriangleOutward(top, right, front, center, glowGold));
            tris.Add(CreateTriangleOutward(top, back, right, center, petalYellow));
            tris.Add(CreateTriangleOutward(top, left, back, center, glowGold));
            tris.Add(CreateTriangleOutward(top, front, left, center, petalYellow));

            tris.Add(CreateTriangleOutward(bottom, front, right, center, petalOrange));
            tris.Add(CreateTriangleOutward(bottom, right, back, center, petalOrange));
            tris.Add(CreateTriangleOutward(bottom, back, left, center, petalOrange));
            tris.Add(CreateTriangleOutward(bottom, left, front, center, petalOrange));
        }

        // ----------------------------------------------------
        //  CRASHBOXES
        // ----------------------------------------------------

        private static List<List<IVector3>> AlienPlantCrashBoxes(float radius, float height)
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -radius, y = -radius, z = 0f },
                    new Vector3 { x = radius, y = radius, z = height }
                )
            };
        }

        // ----------------------------------------------------
        //  SHADOW
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor> AlienPlantShadow(float radius, float height)
        {
            var tris = new List<ITriangleMeshWithColor>();
            const string sc = _3dObjectHelpers.ShadowColorHex;

            // Base shadow
            AddShadowTri(tris, -radius * 0.35f, radius * 0.35f, 0f, height * 0.25f, sc);

            // Leaf/star shadow
            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI * 2f / 8f;
                float dx = MathF.Cos(a);
                float dz = MathF.Sin(a);

                var root = new Vector3 { x = 0f, y = 0f, z = height * 0.28f };
                var left = new Vector3 { x = dx * radius * 0.25f - dz * 2f, y = 0f, z = height * 0.28f + dz * radius * 0.18f };
                var tip = new Vector3 { x = dx * radius, y = 0f, z = height * 0.28f + dz * radius * 0.45f };
                var right = new Vector3 { x = dx * radius * 0.25f + dz * 2f, y = 0f, z = height * 0.28f + dz * radius * 0.18f };

                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = root, vert2 = left, vert3 = tip });
                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = root, vert2 = tip, vert3 = right });
            }

            return tris;
        }

        private static void AddShadowTri(List<ITriangleMeshWithColor> tris, float x1, float x2, float z1, float z2, string color)
        {
            tris.Add(new TriangleMeshWithColor
            {
                Color = color,
                vert1 = new Vector3 { x = x1, y = 0f, z = z1 },
                vert2 = new Vector3 { x = x2, y = 0f, z = z1 },
                vert3 = new Vector3 { x = 0f, y = 0f, z = z2 }
            });
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> CreateRing(float x, float y, float z, float radius, int segments)
        {
            var points = new List<Vector3>(segments);
            float step = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;

                points.Add(new Vector3
                {
                    x = x + MathF.Cos(a) * radius,
                    y = y + MathF.Sin(a) * radius,
                    z = z
                });
            }

            return points;
        }

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }

        private static void AddQuadOutward(
            List<ITriangleMeshWithColor> tris,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 v4,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            tris.Add(CreateTriangleOutward(v1, v2, v3, center, color, noHidden));
            tris.Add(CreateTriangleOutward(v1, v3, v4, center, color, noHidden));
        }

        private static TriangleMeshWithColor CreateTriangleOutward(
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 center,
            string color,
            bool noHidden = false)
        {
            var edge1 = Subtract(v2, v1);
            var edge2 = Subtract(v3, v1);
            var normal = Normalize(Cross(edge1, edge2));

            var mid = new Vector3
            {
                x = (v1.x + v2.x + v3.x) / 3f,
                y = (v1.y + v2.y + v3.y) / 3f,
                z = (v1.z + v2.z + v3.z) / 3f
            };

            var desired = Normalize(Subtract(mid, center));
            float dot = Dot(normal, desired);

            if (dot < 0f)
            {
                var temp = v2;
                v2 = v3;
                v3 = temp;
            }

            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = v1,
                vert2 = v2,
                vert3 = v3,
                noHidden = noHidden
            };
        }

        private static Vector3 Subtract(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.x - b.x,
                y = a.y - b.y,
                z = a.z - b.z
            };
        }

        private static Vector3 Cross(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = a.y * b.z - a.z * b.y,
                y = a.z * b.x - a.x * b.z,
                z = a.x * b.y - a.y * b.x
            };
        }

        private static float Dot(Vector3 a, Vector3 b)
        {
            return a.x * b.x + a.y * b.y + a.z * b.z;
        }

        private static Vector3 Normalize(Vector3 v)
        {
            float lenSq = v.x * v.x + v.y * v.y + v.z * v.z;

            if (lenSq <= 1e-6f)
                return new Vector3 { x = 0, y = 0, z = 0 };

            float invLen = 1.0f / MathF.Sqrt(lenSq);

            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }
    }
}
