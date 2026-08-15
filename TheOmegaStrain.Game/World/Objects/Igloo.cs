using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class Igloo
    {
        private const float SmallIglooScale = 1.18f;
        private const float LargeIglooScale = 1.12f;
        private static readonly Vector3 SmallCenter = new Vector3 { x = 0f, y = 0f, z = 8f };
        private static readonly Vector3 LargeCenter = new Vector3 { x = 0f, y = 0f, z = 13f };

        private static string snowLight = "EAF7FF";
        private static string snowMid = "C9E4F2";
        private static string snowDark = "91B7C8";
        private static string iceBlue = "8FD8FF";
        private static string entranceDark = "101820";
        private static string glowBlue = "78D8FF";

        public static OmegaObject3D CreateSmallIgloo(ISurface parentSurface)
        {
            var igloo = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SmallIgloo",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -6, y = 0, z = -6 }
            };

            AddPart(igloo, "SmallIglooDome", CreateDome(15f, 12f, 2, 8, SmallCenter), true);
            AddPart(igloo, "SmallIglooEntrance", CreateEntrance(8.8f, 8.8f, 6.6f, SmallCenter), true);
            AddPart(igloo, "SmallIglooBlocks", CreateBlockLines(15.2f, 12.0f, 2, 8, SmallCenter), true);

            igloo.CrashBoxes = IglooCrashBoxes(15f, 12f, 8f);
            igloo.CrashBoxNames = new List<string?> { "IglooDome", "Entrance" };

            OmegaObject3DHelpers.AddCustomShadowPart(igloo, IglooShadow(15f, 12f));
            OmegaObject3DHelpers.ApplyScaleToObject(igloo, SmallIglooScale * LandBasedObjectSetup.WinterSurfaceObjectScale);
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(igloo);
            igloo.Movement = new IglooControls();

            return igloo;
        }

        public static OmegaObject3D CreateLargeIgloo(ISurface parentSurface)
        {
            var igloo = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "LargeIgloo",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -10, y = 0, z = -10 }
            };

            AddPart(igloo, "LargeIglooDome", CreateDome(25f, 19f, 3, 10, LargeCenter), true);
            AddPart(igloo, "LargeIglooEntrance", CreateEntrance(13f, 14f, 9.8f, LargeCenter), true);
            AddPart(igloo, "LargeIglooBlocks", CreateBlockLines(25.4f, 19.2f, 3, 10, LargeCenter), true);
            AddPart(igloo, "LargeIglooVent", CreateVent(LargeCenter), true);
            AddPart(igloo, "LargeIglooGlow", CreateEntranceGlow(), true);

            igloo.CrashBoxes = IglooCrashBoxes(25f, 20f, 13f);
            igloo.CrashBoxNames = new List<string?> { "IglooDome", "Entrance" };

            OmegaObject3DHelpers.AddCustomShadowPart(igloo, IglooShadow(25f, 20f));
            OmegaObject3DHelpers.ApplyScaleToObject(igloo, LargeIglooScale * LandBasedObjectSetup.WinterSurfaceObjectScale);
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(igloo);
            igloo.Movement = new IglooControls();

            return igloo;
        }

        private static List<ITriangleMeshWithColor>? CreateDome(
            float radius,
            float height,
            int rings,
            int segments,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();
            var ringList = new List<List<Vector3>>();

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float z = MathF.Sin(t * MathF.PI * 0.5f) * height;
                float ringRadius = MathF.Cos(t * MathF.PI * 0.5f) * radius;

                if (r == rings)
                    ringRadius = 0.1f;

                ringList.Add(CreateRing(0f, 0f, z, ringRadius, segments));
            }

            for (int r = 0; r < rings; r++)
            {
                var lower = ringList[r];
                var upper = ringList[r + 1];

                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    string color = GetSnowColor(r, i);

                    if (r == rings - 1)
                        tris.Add(CreateTriangleOutward(upper[i], lower[next], lower[i], center, color));
                    else
                        AddQuadOutward(tris, lower[i], lower[next], upper[next], upper[i], center, color);
                }
            }

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreateEntrance(
            float width,
            float depth,
            float height,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x1 = -width / 2f;
            float x2 = width / 2f;
            float yFront = -depth - 2f;
            float yBack = -4f;

            var frontLeftBottom = new Vector3 { x = x1, y = yFront, z = 0f };
            var frontRightBottom = new Vector3 { x = x2, y = yFront, z = 0f };
            var frontLeftTop = new Vector3 { x = x1, y = yFront, z = height };
            var frontRightTop = new Vector3 { x = x2, y = yFront, z = height };

            var backLeftBottom = new Vector3 { x = x1 * 0.85f, y = yBack, z = 0f };
            var backRightBottom = new Vector3 { x = x2 * 0.85f, y = yBack, z = 0f };
            var backLeftTop = new Vector3 { x = x1 * 0.65f, y = yBack, z = height + 1f };
            var backRightTop = new Vector3 { x = x2 * 0.65f, y = yBack, z = height + 1f };

            AddQuadOutward(tris, frontLeftBottom, backLeftBottom, backLeftTop, frontLeftTop, center, snowDark);
            AddQuadOutward(tris, frontRightBottom, frontRightTop, backRightTop, backRightBottom, center, snowMid);
            AddQuadOutward(tris, frontLeftTop, backLeftTop, backRightTop, frontRightTop, center, snowLight);

            var archTop = new Vector3 { x = 0f, y = yFront - 0.3f, z = height + 3f };
            tris.Add(CreateTriangleOutward(frontLeftTop, archTop, frontRightTop, center, snowLight));

            AddQuadOutward(
                tris,
                new Vector3 { x = x1 * 0.55f, y = yFront - 0.6f, z = 0f },
                new Vector3 { x = x2 * 0.55f, y = yFront - 0.6f, z = 0f },
                new Vector3 { x = x2 * 0.45f, y = yFront - 0.6f, z = height * 0.72f },
                new Vector3 { x = x1 * 0.45f, y = yFront - 0.6f, z = height * 0.72f },
                center,
                entranceDark,
                noHidden: true);

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreateEntranceGlow()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddQuadOutward(
                tris,
                new Vector3 { x = -3.8f, y = -15.6f, z = 1.4f },
                new Vector3 { x = 3.8f, y = -15.6f, z = 1.4f },
                new Vector3 { x = 2.5f, y = -15.6f, z = 6.4f },
                new Vector3 { x = -2.5f, y = -15.6f, z = 6.4f },
                LargeCenter,
                glowBlue,
                noHidden: true);

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreateBlockLines(
            float radius,
            float height,
            int rings,
            int segments,
            Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            for (int r = 1; r <= rings; r++)
            {
                float t = r / (float)(rings + 1);
                float z = MathF.Sin(t * MathF.PI * 0.5f) * height;
                float ringRadius = MathF.Cos(t * MathF.PI * 0.5f) * radius;

                for (int i = 0; i < segments; i += 3)
                {
                    float a1 = i * MathF.PI * 2f / segments;
                    float a2 = (i + 1.15f) * MathF.PI * 2f / segments;

                    var p1 = new Vector3 { x = MathF.Cos(a1) * ringRadius, y = MathF.Sin(a1) * ringRadius, z = z };
                    var p2 = new Vector3 { x = MathF.Cos(a2) * ringRadius, y = MathF.Sin(a2) * ringRadius, z = z + 0.35f };
                    var p3 = new Vector3 { x = MathF.Cos(a2) * (ringRadius - 1.2f), y = MathF.Sin(a2) * (ringRadius - 1.2f), z = z + 0.55f };

                    tris.Add(CreateTriangleOutward(p1, p2, p3, center, snowDark));
                }
            }

            for (int i = 0; i < segments; i += 3)
            {
                float a = i * MathF.PI * 2f / segments;
                float dx = MathF.Cos(a);
                float dy = MathF.Sin(a);

                var bottom = new Vector3 { x = dx * radius * 0.95f, y = dy * radius * 0.95f, z = 1f };
                var mid = new Vector3 { x = dx * radius * 0.65f, y = dy * radius * 0.65f, z = height * 0.55f };
                var top = new Vector3 { x = dx * radius * 0.25f, y = dy * radius * 0.25f, z = height * 0.86f };

                tris.Add(CreateTriangleOutward(bottom, mid, top, center, snowDark));
            }

            return tris;
        }

        private static List<ITriangleMeshWithColor>? CreateVent(Vector3 center)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var baseA = new Vector3 { x = -3f, y = 4f, z = 17f };
            var baseB = new Vector3 { x = 3f, y = 4f, z = 17f };
            var baseC = new Vector3 { x = 2.2f, y = 7.5f, z = 18f };
            var baseD = new Vector3 { x = -2.2f, y = 7.5f, z = 18f };

            var topA = new Vector3 { x = -2f, y = 4.5f, z = 21f };
            var topB = new Vector3 { x = 2f, y = 4.5f, z = 21f };
            var topC = new Vector3 { x = 1.5f, y = 7f, z = 21.5f };
            var topD = new Vector3 { x = -1.5f, y = 7f, z = 21.5f };

            AddQuadOutward(tris, baseA, baseB, topB, topA, center, snowDark);
            AddQuadOutward(tris, baseB, baseC, topC, topB, center, snowMid);
            AddQuadOutward(tris, baseC, baseD, topD, topC, center, entranceDark);
            AddQuadOutward(tris, baseD, baseA, topA, topD, center, snowDark);

            return tris;
        }

        private static List<List<IVector3>> IglooCrashBoxes(float radius, float height, float entranceDepth)
        {
            return new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -radius, y = -radius, z = 0f },
                    new Vector3 { x = radius, y = radius, z = height }
                ),

                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -radius * 0.35f, y = -radius - entranceDepth, z = 0f },
                    new Vector3 { x = radius * 0.35f, y = -radius * 0.4f, z = height * 0.55f }
                )
            };
        }

        private static List<ITriangleMeshWithColor> IglooShadow(float radius, float height)
        {
            var tris = new List<ITriangleMeshWithColor>();
            const string sc = OmegaObject3DHelpers.ShadowColorHex;

            var bottom = new Vector3 { x = 0f, y = 0f, z = 0f };

            for (int i = 0; i < 8; i++)
            {
                float a1 = i * MathF.PI * 2f / 8f;
                float a2 = (i + 1) * MathF.PI * 2f / 8f;

                var p1 = new Vector3 { x = MathF.Cos(a1) * radius, y = 0f, z = MathF.Sin(a1) * radius * 0.35f };
                var p2 = new Vector3 { x = MathF.Cos(a2) * radius, y = 0f, z = MathF.Sin(a2) * radius * 0.35f };

                tris.Add(new TriangleMeshWithColor { Color = sc, vert1 = bottom, vert2 = p1, vert3 = p2 });
            }

            tris.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -radius * 0.28f, y = 0f, z = -radius * 0.2f },
                vert2 = new Vector3 { x = radius * 0.28f, y = 0f, z = -radius * 0.2f },
                vert3 = new Vector3 { x = 0f, y = 0f, z = -radius * 0.95f }
            });

            return tris;
        }

        private static string GetSnowColor(int ring, int segment)
        {
            if ((ring + segment) % 4 == 0) return snowLight;
            if ((ring + segment) % 4 == 1) return snowMid;
            if ((ring + segment) % 4 == 2) return iceBlue;
            return snowDark;
        }

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

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null) return;

            obj.ObjectParts.Add(new OmegaObjectPart3D
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
            return new Vector3 { x = a.x - b.x, y = a.y - b.y, z = a.z - b.z };
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
