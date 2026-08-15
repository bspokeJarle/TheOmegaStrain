using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class BedouinTent
    {
        public static OmegaObject3D CreateBedouinTent(ISurface parentSurface)
        {
            var tent = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "BedouinTent",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new BedouinTentControls(),
                ShadowOffset = new Vector3 { x = -9f, y = 0f, z = -8f }
            };

            AddPart(tent, "TentCanvas", TentCanvas(), true);
            AddPart(tent, "TentPatternBands", TentPatternBands(), true);
            AddPart(tent, "TentDarkSide", TentDarkSide(), true);
            AddPart(tent, "TentEntrance", TentEntrance(), true);
            AddPart(tent, "TentRopes", TentRopes(), true);
            AddPart(tent, "TentPoles", TentPoles(), true);

            tent.CrashBoxes = new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -36f, y = -15f, z = 1f },
                    new Vector3 { x = 36f, y = 15f, z = 17f })
            };
            tent.CrashBoxNames = new List<string?> { "BedouinTentBody" };

            OmegaObject3DHelpers.AddCustomShadowPart(tent, TentShadow());
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(tent);

            return tent;
        }

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColor> triangles, bool visible)
        {
            obj.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = name,
                Triangles = triangles,
                IsVisible = visible
            });
        }

        private static List<ITriangleMeshWithColor> TentCanvas()
        {
            const string light = "3F352D";
            const string mid = "2A211B";
            const string dark = "181310";
            const string back = "241A15";

            var tris = new List<ITriangleMeshWithColor>();
            var center = new Vector3 { x = 0f, y = 0f, z = 9f };

            var frontLeftGround = new Vector3 { x = -50f, y = -22f, z = 0f };
            var frontRightGround = new Vector3 { x = 50f, y = -22f, z = 0f };
            var backLeftGround = new Vector3 { x = -44f, y = 22f, z = 0f };
            var backRightGround = new Vector3 { x = 44f, y = 22f, z = 0f };
            var frontLeftTop = new Vector3 { x = -13f, y = -20f, z = 18f };
            var frontRightTop = new Vector3 { x = 13f, y = -20f, z = 18f };
            var backLeftTop = new Vector3 { x = -12f, y = 20f, z = 18f };
            var backRightTop = new Vector3 { x = 12f, y = 20f, z = 18f };

            OmegaObject3DHelpers.AddQuadOutward(tris, frontLeftGround, backLeftGround, backLeftTop, frontLeftTop, center, light);
            OmegaObject3DHelpers.AddQuadOutward(tris, frontLeftTop, backLeftTop, backRightTop, frontRightTop, center, mid);
            OmegaObject3DHelpers.AddQuadOutward(tris, frontRightTop, backRightTop, backRightGround, frontRightGround, center, dark);
            OmegaObject3DHelpers.AddQuadOutward(tris, backLeftGround, backLeftTop, backRightTop, backRightGround, center, back);

            return tris;
        }

        private static List<ITriangleMeshWithColor> TentPatternBands()
        {
            const string woolStripe = "D8C28A";
            const string paleStripe = "E7D8AF";
            const string redStripe = "9A3024";
            const string amberStripe = "B67A3D";

            var tris = new List<ITriangleMeshWithColor>();
            var center = new Vector3 { x = 0f, y = 0f, z = 9f };

            var frontLeftGround = new Vector3 { x = -50f, y = -22f, z = 0f };
            var frontRightGround = new Vector3 { x = 50f, y = -22f, z = 0f };
            var backLeftGround = new Vector3 { x = -44f, y = 22f, z = 0f };
            var backRightGround = new Vector3 { x = 44f, y = 22f, z = 0f };
            var frontLeftTop = new Vector3 { x = -13f, y = -20f, z = 18f };
            var frontRightTop = new Vector3 { x = 13f, y = -20f, z = 18f };
            var backLeftTop = new Vector3 { x = -12f, y = 20f, z = 18f };
            var backRightTop = new Vector3 { x = 12f, y = 20f, z = 18f };

            AddSlopeBand(tris, frontLeftGround, backLeftGround, backLeftTop, frontLeftTop, 0.44f, 0.51f, center, woolStripe);
            AddSlopeBand(tris, frontLeftGround, backLeftGround, backLeftTop, frontLeftTop, 0.68f, 0.74f, center, redStripe);
            AddSlopeBand(tris, frontRightGround, backRightGround, backRightTop, frontRightTop, 0.44f, 0.51f, center, paleStripe);
            AddSlopeBand(tris, frontRightGround, backRightGround, backRightTop, frontRightTop, 0.68f, 0.74f, center, redStripe);

            AddOverlayQuadOutward(
                tris,
                new Vector3 { x = -6f, y = -20f, z = 18.15f },
                new Vector3 { x = -5f, y = 20f, z = 18.15f },
                new Vector3 { x = -2f, y = 20f, z = 18.15f },
                new Vector3 { x = -3f, y = -20f, z = 18.15f },
                center,
                woolStripe,
                offset: 1.8f);

            AddOverlayQuadOutward(
                tris,
                new Vector3 { x = 4f, y = -20f, z = 18.15f },
                new Vector3 { x = 3f, y = 20f, z = 18.15f },
                new Vector3 { x = 6.5f, y = 20f, z = 18.15f },
                new Vector3 { x = 7.5f, y = -20f, z = 18.15f },
                center,
                amberStripe,
                offset: 1.8f);

            AddOverlayQuadOutward(
                tris,
                new Vector3 { x = -48f, y = -22.6f, z = 0.25f },
                new Vector3 { x = -13.5f, y = -20.4f, z = 18.35f },
                new Vector3 { x = -11.3f, y = -20.2f, z = 18.55f },
                new Vector3 { x = -43f, y = -22.5f, z = 0.25f },
                center,
                paleStripe,
                offset: 1.75f);

            AddOverlayQuadOutward(
                tris,
                new Vector3 { x = 43f, y = -22.5f, z = 0.25f },
                new Vector3 { x = 11.3f, y = -20.2f, z = 18.55f },
                new Vector3 { x = 13.5f, y = -20.4f, z = 18.35f },
                new Vector3 { x = 48f, y = -22.6f, z = 0.25f },
                center,
                paleStripe,
                offset: 1.75f);

            return tris;
        }

        private static List<ITriangleMeshWithColor> TentDarkSide()
        {
            const string dark = "422415";
            var center = new Vector3 { x = 0f, y = -24f, z = 6f };
            return new List<ITriangleMeshWithColor>
            {
                OmegaObject3DHelpers.CreateTriangleOutward(
                    new Vector3 { x = -8f, y = -23f, z = 0f },
                    new Vector3 { x = 8f, y = -23f, z = 0f },
                    new Vector3 { x = 0f, y = -22f, z = 13f },
                    center,
                    dark,
                    noHidden: true)
            };
        }

        private static List<ITriangleMeshWithColor> TentEntrance()
        {
            const string flap = "D6A96C";
            var center = new Vector3 { x = 0f, y = -23f, z = 8f };
            return new List<ITriangleMeshWithColor>
            {
                OmegaObject3DHelpers.CreateTriangleOutward(
                    new Vector3 { x = -50f, y = -23f, z = 0f },
                    new Vector3 { x = -13f, y = -20f, z = 18f },
                    new Vector3 { x = -8f, y = -23f, z = 0f },
                    center,
                    flap,
                    noHidden: true),
                OmegaObject3DHelpers.CreateTriangleOutward(
                    new Vector3 { x = 8f, y = -23f, z = 0f },
                    new Vector3 { x = 13f, y = -20f, z = 18f },
                    new Vector3 { x = 50f, y = -23f, z = 0f },
                    center,
                    flap,
                    noHidden: true),
                OmegaObject3DHelpers.CreateTriangleOutward(
                    new Vector3 { x = -13f, y = -20f, z = 18f },
                    new Vector3 { x = 13f, y = -20f, z = 18f },
                    new Vector3 { x = 0f, y = -22f, z = 13f },
                    center,
                    "E0B77B",
                    noHidden: true)
            };
        }

        private static List<ITriangleMeshWithColor> TentPoles()
        {
            const string pole = "A77A48";
            return new List<ITriangleMeshWithColor>
            {
                AddPole(-13f, -22f, 0f, -11f, -20f, 19f, pole),
                AddPole(13f, -22f, 0f, 11f, -20f, 19f, pole),
                AddPole(-12f, 22f, 0f, -10f, 20f, 18f, pole),
                AddPole(12f, 22f, 0f, 10f, 20f, 18f, pole)
            };
        }

        private static List<ITriangleMeshWithColor> TentRopes()
        {
            const string rope = "F0D99C";
            var tris = new List<ITriangleMeshWithColor>();

            AddRope(tris, new Vector3 { x = -13f, y = -20f, z = 18f }, new Vector3 { x = -70f, y = -35f, z = 0f }, rope);
            AddRope(tris, new Vector3 { x = 13f, y = -20f, z = 18f }, new Vector3 { x = 70f, y = -35f, z = 0f }, rope);
            AddRope(tris, new Vector3 { x = -12f, y = 20f, z = 18f }, new Vector3 { x = -64f, y = 36f, z = 0f }, rope);
            AddRope(tris, new Vector3 { x = 12f, y = 20f, z = 18f }, new Vector3 { x = 64f, y = 36f, z = 0f }, rope);
            AddRope(tris, new Vector3 { x = -50f, y = -22f, z = 0f }, new Vector3 { x = -44f, y = 22f, z = 0f }, "D7BF82", width: 1.2f);
            AddRope(tris, new Vector3 { x = 50f, y = -22f, z = 0f }, new Vector3 { x = 44f, y = 22f, z = 0f }, "D7BF82", width: 1.2f);

            return tris;
        }

        private static void AddSlopeBand(
            List<ITriangleMeshWithColor> tris,
            Vector3 frontGround,
            Vector3 backGround,
            Vector3 backTop,
            Vector3 frontTop,
            float from,
            float to,
            Vector3 center,
            string color)
        {
            AddOverlayQuadOutward(
                tris,
                Lerp(frontGround, frontTop, from),
                Lerp(backGround, backTop, from),
                Lerp(backGround, backTop, to),
                Lerp(frontGround, frontTop, to),
                center,
                color,
                offset: 1.8f);
        }

        private static void AddRope(
            List<ITriangleMeshWithColor> tris,
            Vector3 start,
            Vector3 end,
            string color,
            float width = 1.6f)
        {
            float dx = end.x - start.x;
            float dy = end.y - start.y;
            float length = Math.Max(1f, (float)Math.Sqrt((dx * dx) + (dy * dy)));
            float nx = (-dy / length) * width * 0.5f;
            float ny = (dx / length) * width * 0.5f;

            var center = new Vector3
            {
                x = (start.x + end.x) * 0.5f,
                y = (start.y + end.y) * 0.5f,
                z = (start.z + end.z) * 0.5f
            };

            OmegaObject3DHelpers.AddQuadOutward(
                tris,
                new Vector3 { x = start.x + nx, y = start.y + ny, z = start.z + 1.3f },
                new Vector3 { x = end.x + nx, y = end.y + ny, z = end.z + 1.3f },
                new Vector3 { x = end.x - nx, y = end.y - ny, z = end.z + 1.3f },
                new Vector3 { x = start.x - nx, y = start.y - ny, z = start.z + 1.3f },
                center,
                color,
                noHidden: true);
        }

        private static void AddOverlayQuadOutward(
            List<ITriangleMeshWithColor> tris,
            Vector3 v1,
            Vector3 v2,
            Vector3 v3,
            Vector3 v4,
            Vector3 center,
            string color,
            float offset)
        {
            var normal = GetOutwardNormal(v1, v2, v3, center);
            OmegaObject3DHelpers.AddQuadOutward(
                tris,
                Offset(v1, normal, offset),
                Offset(v2, normal, offset),
                Offset(v3, normal, offset),
                Offset(v4, normal, offset),
                center,
                color,
                noHidden: true);
        }

        private static Vector3 GetOutwardNormal(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 center)
        {
            var edge1 = OmegaObject3DHelpers.Subtract(v2, v1);
            var edge2 = OmegaObject3DHelpers.Subtract(v3, v1);
            var normal = OmegaObject3DHelpers.Normalize(OmegaObject3DHelpers.Cross(edge1, edge2));
            var mid = new Vector3
            {
                x = (v1.x + v2.x + v3.x) / 3f,
                y = (v1.y + v2.y + v3.y) / 3f,
                z = (v1.z + v2.z + v3.z) / 3f
            };
            var desired = OmegaObject3DHelpers.Normalize(OmegaObject3DHelpers.Subtract(mid, center));
            if (OmegaObject3DHelpers.Dot(normal, desired) < 0f)
                normal = new Vector3 { x = -normal.x, y = -normal.y, z = -normal.z };

            return normal;
        }

        private static Vector3 Offset(Vector3 v, Vector3 normal, float amount)
        {
            return new Vector3
            {
                x = v.x + (normal.x * amount),
                y = v.y + (normal.y * amount),
                z = v.z + (normal.z * amount)
            };
        }

        private static Vector3 Lerp(Vector3 a, Vector3 b, float t, float lift = 0f)
        {
            return new Vector3
            {
                x = a.x + ((b.x - a.x) * t),
                y = a.y + ((b.y - a.y) * t),
                z = a.z + ((b.z - a.z) * t) + lift
            };
        }

        private static TriangleMeshWithColor AddPole(float x1, float y1, float z1, float x2, float y2, float z2, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = new Vector3 { x = x1, y = y1, z = z1 },
                vert2 = new Vector3 { x = x2, y = y2, z = z2 },
                vert3 = new Vector3 { x = x1 + 1.4f, y = y1, z = z1 },
                noHidden = true
            };
        }

        private static List<ITriangleMeshWithColor> TentShadow()
        {
            const string sc = OmegaObject3DHelpers.ShadowColorHex;
            var a = new Vector3 { x = -50f, y = -23f, z = 0f };
            var b = new Vector3 { x = 50f, y = -23f, z = 0f };
            var c = new Vector3 { x = 44f, y = 22f, z = 0f };
            var d = new Vector3 { x = -44f, y = 22f, z = 0f };
            var ridge = new Vector3 { x = 0f, y = 0f, z = 18f };

            return new List<ITriangleMeshWithColor>
            {
                new TriangleMeshWithColor { Color = sc, vert1 = a, vert2 = b, vert3 = ridge },
                new TriangleMeshWithColor { Color = sc, vert1 = b, vert2 = c, vert3 = ridge },
                new TriangleMeshWithColor { Color = sc, vert1 = c, vert2 = d, vert3 = ridge },
                new TriangleMeshWithColor { Color = sc, vert1 = d, vert2 = a, vert3 = ridge }
            };
        }
    }
}
