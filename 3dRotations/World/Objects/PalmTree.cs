using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class PalmTree
    {
        private const float CrashboxSize = 1.15f;
        private const float PalmScale = 1.18f;

        private static readonly Vector3 LargePalmCenter = new Vector3 { x = 0, y = 0, z = 32f };
        private static readonly Vector3 SmallPalmCenter = new Vector3 { x = 0, y = 0, z = 20f };

        // Colors
        private static string trunkDark = "5A351B";
        private static string trunkMid = "8B5A2B";
        private static string trunkLight = "B07A3A";

        private static string leafDark = "145C2A";
        private static string leafMid = "1F8A3B";
        private static string leafLight = "38B24A";
        private static string leafYellow = "8EBF38";

        private static string fruitOrange = "D98224";
        private static string fruitRed = "B9411E";

        // ----------------------------------------------------
        //  PUBLIC FACTORIES
        // ----------------------------------------------------

        public static _3dObject CreateLargePalm(ISurface parentSurface)
        {
            var palm = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "LargePalm",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -12, y = 0, z = -12 }
            };

            AddPart(palm, "LargePalmTrunk", CreateBentTrunk(
                height: 38f,
                baseRadius: 3.2f,
                topRadius: 2.1f,
                sections: 7,
                segments: 8,
                bendX: 4.5f,
                bendY: -2.0f,
                center: LargePalmCenter), true);

            AddPart(palm, "LargePalmCrownCore", CreateCrownCore(
                z: 39f,
                radius: 4.3f,
                height: 5.5f,
                center: LargePalmCenter), true);

            AddPart(palm, "LargePalmLeaves", CreatePalmLeaves(
                crownZ: 42f,
                leafCount: 12,
                leafLength: 30f,
                leafWidth: 5.8f,
                droop: 10f,
                upwardLift: 7f,
                asymmetry: 1.0f,
                center: LargePalmCenter), true);

            AddPart(palm, "LargePalmFruit", CreateFruitCluster(
                z: 37.5f,
                radius: 3.1f,
                center: LargePalmCenter), true);

            palm.CrashBoxes = PalmCrashBoxes(
                trunkHeight: 42f,
                trunkRadius: 4.5f,
                crownRadius: 30f,
                crownBottomZ: 30f,
                crownTopZ: 55f);

            palm.CrashBoxNames = new List<string?> { "Trunk", "Crown" };

            _3dObjectHelpers.AddCustomShadowPart(palm, LargePalmShadow());
            ScalePalm(palm);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(palm);

            return palm;
        }

        public static _3dObject CreateSmallPalm(ISurface parentSurface)
        {
            var palm = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SmallPalm",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -8, y = 0, z = -8 }
            };

            AddPart(palm, "SmallPalmTrunk", CreateBentTrunk(
                height: 23f,
                baseRadius: 2.3f,
                topRadius: 1.6f,
                sections: 5,
                segments: 8,
                bendX: -2.5f,
                bendY: 1.5f,
                center: SmallPalmCenter), true);

            AddPart(palm, "SmallPalmCrownCore", CreateCrownCore(
                z: 24f,
                radius: 3.0f,
                height: 3.8f,
                center: SmallPalmCenter), true);

            AddPart(palm, "SmallPalmLeaves", CreatePalmLeaves(
                crownZ: 26f,
                leafCount: 9,
                leafLength: 18f,
                leafWidth: 4.1f,
                droop: 6.5f,
                upwardLift: 4.5f,
                asymmetry: 0.75f,
                center: SmallPalmCenter), true);

            AddPart(palm, "SmallPalmFruit", CreateFruitCluster(
                z: 23f,
                radius: 2.1f,
                center: SmallPalmCenter), true);

            palm.CrashBoxes = PalmCrashBoxes(
                trunkHeight: 26f,
                trunkRadius: 3.2f,
                crownRadius: 19f,
                crownBottomZ: 19f,
                crownTopZ: 35f);

            palm.CrashBoxNames = new List<string?> { "Trunk", "Crown" };

            _3dObjectHelpers.AddCustomShadowPart(palm, SmallPalmShadow());
            ScalePalm(palm);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(palm);

            return palm;
        }

        // ----------------------------------------------------
        //  TRUNK
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateBentTrunk(
            float height,
            float baseRadius,
            float topRadius,
            int sections,
            int segments,
            float bendX,
            float bendY,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var rings = new List<List<Vector3>>();

            for (int s = 0; s <= sections; s++)
            {
                float t = s / (float)sections;
                float z = height * t;
                float radius = Lerp(baseRadius, topRadius, t);

                // Curved trunk using quadratic bend.
                float curve = t * t;
                float offsetX = bendX * curve;
                float offsetY = bendY * curve;

                rings.Add(CreateRing(
                    x: offsetX,
                    y: offsetY,
                    z: z,
                    radiusY: radius,
                    radiusX: radius * 0.88f,
                    segments: segments));
            }

            for (int s = 0; s < sections; s++)
            {
                var ringA = rings[s];
                var ringB = rings[s + 1];

                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    string color = i % 3 == 0 ? trunkLight : i % 3 == 1 ? trunkMid : trunkDark;

                    AddQuadOutward(
                        tris,
                        ringA[i],
                        ringA[next],
                        ringB[next],
                        ringB[i],
                        center,
                        color);
                }
            }

            // Add simple bark bands as raised triangular shards.
            for (int s = 1; s < sections; s++)
            {
                float t = s / (float)sections;
                float z = height * t;
                float offsetX = bendX * t * t;
                float offsetY = bendY * t * t;
                float radius = Lerp(baseRadius, topRadius, t) + 0.25f;

                for (int i = 0; i < segments; i += 2)
                {
                    float a = i * (MathF.PI * 2f / segments);
                    float x1 = offsetX + MathF.Cos(a) * radius;
                    float y1 = offsetY + MathF.Sin(a) * radius;

                    float a2 = (i + 1) * (MathF.PI * 2f / segments);
                    float x2 = offsetX + MathF.Cos(a2) * radius;
                    float y2 = offsetY + MathF.Sin(a2) * radius;

                    tris.Add(CreateTriangleOutward(
                        new Vector3 { x = x1, y = y1, z = z - 1.0f },
                        new Vector3 { x = x2, y = y2, z = z - 0.4f },
                        new Vector3 { x = (x1 + x2) * 0.5f, y = (y1 + y2) * 0.5f, z = z + 1.1f },
                        center,
                        trunkDark));
                }
            }

            return tris;
        }

        // ----------------------------------------------------
        //  CROWN
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor>? CreateCrownCore(
            float z,
            float radius,
            float height,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            int segments = 8;
            var lower = CreateRing(0, 0, z, radius, radius, segments);
            var upper = CreateRing(0, 0, z + height, radius * 0.65f, radius * 0.65f, segments);
            var top = new Vector3 { x = 0f, y = 0f, z = z + height + 2f };

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                AddQuadOutward(
                    tris,
                    lower[i],
                    lower[next],
                    upper[next],
                    upper[i],
                    center,
                    i % 2 == 0 ? trunkMid : trunkDark);

                tris.Add(CreateTriangleOutward(
                    top,
                    upper[next],
                    upper[i],
                    center,
                    i % 2 == 0 ? leafDark : trunkDark));
            }

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreatePalmLeaves(
            float crownZ,
            int leafCount,
            float leafLength,
            float leafWidth,
            float droop,
            float upwardLift,
            float asymmetry,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int i = 0; i < leafCount; i++)
            {
                float angle = i * (MathF.PI * 2f / leafCount);
                float wave = MathF.Sin(i * 1.7f) * asymmetry;

                float dirX = MathF.Cos(angle);
                float dirY = MathF.Sin(angle);

                float sideX = -dirY;
                float sideY = dirX;

                float localLength = leafLength * (0.86f + 0.20f * MathF.Sin(i * 2.1f));
                float localWidth = leafWidth * (0.85f + 0.25f * MathF.Cos(i * 1.4f));

                var root = new Vector3
                {
                    x = dirX * 2.2f,
                    y = dirY * 2.2f,
                    z = crownZ
                };

                var mid = new Vector3
                {
                    x = dirX * (localLength * 0.52f) + sideX * wave,
                    y = dirY * (localLength * 0.52f) + sideY * wave,
                    z = crownZ + upwardLift - droop * 0.28f
                };

                var tip = new Vector3
                {
                    x = dirX * localLength + sideX * wave * 1.6f,
                    y = dirY * localLength + sideY * wave * 1.6f,
                    z = crownZ - droop
                };

                var rootL = new Vector3
                {
                    x = root.x + sideX * localWidth * 0.45f,
                    y = root.y + sideY * localWidth * 0.45f,
                    z = root.z - 0.3f
                };

                var rootR = new Vector3
                {
                    x = root.x - sideX * localWidth * 0.45f,
                    y = root.y - sideY * localWidth * 0.45f,
                    z = root.z - 0.3f
                };

                var midL = new Vector3
                {
                    x = mid.x + sideX * localWidth,
                    y = mid.y + sideY * localWidth,
                    z = mid.z - 0.8f
                };

                var midR = new Vector3
                {
                    x = mid.x - sideX * localWidth,
                    y = mid.y - sideY * localWidth,
                    z = mid.z - 0.8f
                };

                string colorA = i % 4 == 0 ? leafLight : i % 4 == 1 ? leafMid : i % 4 == 2 ? leafDark : leafYellow;
                string colorB = i % 2 == 0 ? leafDark : leafMid;

                // Main broad leaf shape.
                tris.Add(CreateTriangleOutward(rootL, mid, midL, center, colorA));
                tris.Add(CreateTriangleOutward(rootR, midR, mid, center, colorB));
                tris.Add(CreateTriangleOutward(midL, mid, tip, center, colorA));
                tris.Add(CreateTriangleOutward(mid, midR, tip, center, colorB));

                // Slight underside fold, gives the leaf thickness/form.
                var underside = new Vector3
                {
                    x = mid.x,
                    y = mid.y,
                    z = mid.z - 1.6f
                };

                tris.Add(CreateTriangleOutward(rootL, underside, rootR, center, leafDark));
                tris.Add(CreateTriangleOutward(midL, tip, underside, center, leafDark));
                tris.Add(CreateTriangleOutward(midR, underside, tip, center, leafDark));
            }

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreateFruitCluster(float z, float radius, Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddFruit(tris, new Vector3 { x = 1.6f, y = 0.8f, z = z }, radius, fruitOrange, center);
            AddFruit(tris, new Vector3 { x = -1.5f, y = 1.0f, z = z - 0.8f }, radius * 0.85f, fruitRed, center);
            AddFruit(tris, new Vector3 { x = 0.2f, y = -1.5f, z = z - 1.0f }, radius * 0.9f, fruitOrange, center);

            return tris;
        }

        private static void AddFruit(List<ITriangleMeshWithColor> tris, Vector3 c, float r, string color, Vector3 center)
        {
            var top = new Vector3 { x = c.x, y = c.y, z = c.z + r };
            var bottom = new Vector3 { x = c.x, y = c.y, z = c.z - r };
            var left = new Vector3 { x = c.x, y = c.y - r, z = c.z };
            var right = new Vector3 { x = c.x, y = c.y + r, z = c.z };
            var front = new Vector3 { x = c.x + r, y = c.y, z = c.z };
            var back = new Vector3 { x = c.x - r, y = c.y, z = c.z };

            tris.Add(CreateTriangleOutward(top, right, front, center, color));
            tris.Add(CreateTriangleOutward(top, back, right, center, color));
            tris.Add(CreateTriangleOutward(top, left, back, center, color));
            tris.Add(CreateTriangleOutward(top, front, left, center, color));

            tris.Add(CreateTriangleOutward(bottom, front, right, center, color));
            tris.Add(CreateTriangleOutward(bottom, right, back, center, color));
            tris.Add(CreateTriangleOutward(bottom, back, left, center, color));
            tris.Add(CreateTriangleOutward(bottom, left, front, center, color));
        }

        // ----------------------------------------------------
        //  CRASHBOXES
        // ----------------------------------------------------

        private static List<List<IVector3>> PalmCrashBoxes(
            float trunkHeight,
            float trunkRadius,
            float crownRadius,
            float crownBottomZ,
            float crownTopZ)
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -trunkRadius, y = -trunkRadius, z = 0 },
                    new Vector3 { x = trunkRadius, y = trunkRadius, z = trunkHeight }
                ),

                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -crownRadius, y = -crownRadius, z = crownBottomZ },
                    new Vector3 { x = crownRadius, y = crownRadius, z = crownTopZ }
                )
            };
        }

        // ----------------------------------------------------
        //  SHADOWS
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor> LargePalmShadow()
        {
            return PalmShadow(
                trunkHeight: 38f,
                crownZ: 42f,
                crownRadius: 32f,
                trunkWidth: 5f);
        }

        private static List<ITriangleMeshWithColor> SmallPalmShadow()
        {
            return PalmShadow(
                trunkHeight: 23f,
                crownZ: 26f,
                crownRadius: 20f,
                trunkWidth: 3.5f);
        }

        private static List<ITriangleMeshWithColor> PalmShadow(
            float trunkHeight,
            float crownZ,
            float crownRadius,
            float trunkWidth)
        {
            var tris = new List<ITriangleMeshWithColor>();
            const string sc = _3dObjectHelpers.ShadowColorHex;

            var tA = new Vector3 { x = -trunkWidth, y = 0f, z = 0f };
            var tB = new Vector3 { x = trunkWidth, y = 0f, z = 0f };
            var tC = new Vector3 { x = trunkWidth * 0.45f, y = 0f, z = trunkHeight };
            var tD = new Vector3 { x = -trunkWidth * 0.45f, y = 0f, z = trunkHeight };

            tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = tA, vert2 = tB, vert3 = tC });
            tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = tA, vert2 = tC, vert3 = tD });

            var center = new Vector3 { x = 0f, y = 0f, z = crownZ };

            for (int i = 0; i < 8; i++)
            {
                float a = i * MathF.PI * 2f / 8f;
                float a2 = (i + 0.45f) * MathF.PI * 2f / 8f;

                var root = new Vector3 { x = 0f, y = 0f, z = crownZ };
                var left = new Vector3
                {
                    x = MathF.Cos(a - 0.12f) * crownRadius * 0.28f,
                    y = 0f,
                    z = crownZ + MathF.Sin(a - 0.12f) * crownRadius * 0.28f
                };
                var tip = new Vector3
                {
                    x = MathF.Cos(a2) * crownRadius,
                    y = 0f,
                    z = crownZ + MathF.Sin(a2) * crownRadius * 0.65f
                };
                var right = new Vector3
                {
                    x = MathF.Cos(a + 0.12f) * crownRadius * 0.28f,
                    y = 0f,
                    z = crownZ + MathF.Sin(a + 0.12f) * crownRadius * 0.28f
                };

                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = root, vert2 = left, vert3 = tip });
                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = root, vert2 = tip, vert3 = right });
            }

            return tris;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> CreateRing(
            float x,
            float y,
            float z,
            float radiusY,
            float radiusX,
            int segments)
        {
            var points = new List<Vector3>(segments);
            float step = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;

                points.Add(new Vector3
                {
                    x = x + MathF.Cos(a) * radiusX,
                    y = y + MathF.Sin(a) * radiusY,
                    z = z
                });
            }

            return points;
        }

        private static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * t;
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

        private static void ScalePalm(_3dObject palm)
        {
            _3dObjectHelpers.ApplyScaleToObject(palm, PalmScale);

            if (palm.ShadowOffset != null)
            {
                palm.ShadowOffset.x *= PalmScale;
                palm.ShadowOffset.y *= PalmScale;
                palm.ShadowOffset.z *= PalmScale;
            }
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
