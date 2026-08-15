using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class PolarBear
    {
        private const float Scale = 1.45f;
        private const float CrashboxSize = 1.12f;
        private const float LengthScale = 0.90f;
        private const float HeightScale = 1.10f;
        private const float HeadScale = 1.14f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = -4f, y = 0f, z = 12f };
        private static readonly Vector3 HeadCenter = new Vector3 { x = 29f, y = 0f, z = 13f };

        private static string furLight = "EAF6FA";
        private static string furMid = "C9DCE5";
        private static string furDark = "8EA8B4";
        private static string furShadow = "6F8792";
        private static string noseBlack = "111417";
        private static string eyeBlack = "050607";

        public static _3dObject CreatePolarBear(ISurface parentSurface)
        {
            var bear = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "PolarBear",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                ShadowOffset = new Vector3 { x = -8f, y = 0f, z = -8f }
            };

            AddPart(bear, "PolarBearMainBody", MainBody(), true);
            AddPart(bear, "PolarBearShoulderBlock", ShoulderBlock(), true);
            AddPart(bear, "PolarBearNeck", Neck(), true);
            AddPart(bear, "PolarBearHead", Head(), true);
            AddPart(bear, "PolarBearUpperSnout", UpperSnout(), true);
            AddPart(bear, "PolarBearLowerJaw", LowerJaw(), true);
            AddPart(bear, "PolarBearEars", Ears(), true);
            AddPart(bear, "PolarBearEyesNose", EyesAndNose(), true);
            AddPart(bear, "PolarBearLegsAndPaws", LegsAndPaws(), true);
            AddPart(bear, "PolarBearTail", Tail(), true);

            bear.CrashBoxes = PolarBearCrashBoxes();
            bear.CrashBoxNames = new List<string?> { "Body", "Head", "Legs" };

            ApplyAxisScaleToObject(bear, LengthScale, 1f, HeightScale);
            ApplyAxisScaleToCrashBoxes(bear.CrashBoxes, LengthScale, 1f, HeightScale);
            ScalePartUniform(bear, "PolarBearHead", HeadScale);
            ScalePartUniform(bear, "PolarBearUpperSnout", HeadScale);
            ScalePartUniform(bear, "PolarBearLowerJaw", HeadScale);
            ScalePartUniform(bear, "PolarBearEars", HeadScale);
            ScalePartUniform(bear, "PolarBearEyesNose", HeadScale);

            _3dObjectHelpers.ApplyScaleToObject(bear, Scale * LandBasedObjectSetup.WinterSurfaceObjectScale);
            var shadow = PolarBearShadow();
            _3dObjectHelpers.ApplyScaleToTriangles(shadow, LandBasedObjectSetup.WinterSurfaceObjectScale);
            _3dObjectHelpers.AddCustomShadowPart(bear, shadow);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(bear);

            return bear;
        }

        // ----------------------------------------------------
        //  BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? MainBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var rear = Ring(-28f, 0f, 12f, 12f, 10f, 8);
            var mid = Ring(-10f, 0f, 13f, 14f, 11f, 8);
            var shoulder = Ring(8f, 0f, 14f, 12f, 12f, 8);

            StitchRings(tris, rear, mid, BodyCenter);
            StitchRings(tris, mid, shoulder, BodyCenter);

            var rearTip = new Vector3 { x = -39f, y = 0f, z = 11f };

            for (int i = 0; i < rear.Count; i++)
            {
                int next = (i + 1) % rear.Count;
                tris.Add(CreateTriangleOutward(rear[i], rear[next], rearTip, BodyCenter, GetFurColor(i)));
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? ShoulderBlock()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var back = Ring(-2f, 0f, 15f, 10.6f, 7.3f, 8);
            var front = Ring(14f, 0f, 16.2f, 9.1f, 8.3f, 8);
            StitchRings(tris, back, front, new Vector3 { x = 6f, y = 0f, z = 15.5f });

            // Raised shoulder peak, very important for polar bear silhouette.
            var leftBase = new Vector3 { x = 1f, y = -8.5f, z = 22.0f };
            var rightBase = new Vector3 { x = 1f, y = 8.5f, z = 22.0f };
            var backBase = new Vector3 { x = -8f, y = 0f, z = 19f };
            var frontBase = new Vector3 { x = 12f, y = 0f, z = 19.8f };
            var peak = new Vector3 { x = 4f, y = 0f, z = 28.6f };

            tris.Add(CreateTriangleOutward(leftBase, peak, frontBase, BodyCenter, furLight));
            tris.Add(CreateTriangleOutward(frontBase, peak, rightBase, BodyCenter, furLight));
            tris.Add(CreateTriangleOutward(rightBase, peak, backBase, BodyCenter, furMid));
            tris.Add(CreateTriangleOutward(backBase, peak, leftBase, BodyCenter, furDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? Neck()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var back = Ring(14f, 0f, 14f, 7f, 7f, 8);
            var front = Ring(24f, 0f, 13f, 5f, 5f, 8);

            StitchRings(tris, back, front, new Vector3 { x = 19f, y = 0f, z = 13f });

            return tris;
        }

        // ----------------------------------------------------
        //  HEAD
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? Head()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var back = Ring(24f, 0f, 13f, 5.6f, 5f, 8);
            var mid = Ring(32f, 0f, 12.7f, 5.2f, 4.3f, 8);
            var front = Ring(39f, 0f, 12f, 3.7f, 3.0f, 8);

            StitchRings(tris, back, mid, HeadCenter);
            StitchRings(tris, mid, front, HeadCenter);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? UpperSnout()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var backLeft = new Vector3 { x = 39.2f, y = -3.6f, z = 11.8f };
            var backMid = new Vector3 { x = 39.2f, y = 0f, z = 13.2f };
            var backRight = new Vector3 { x = 39.2f, y = 3.6f, z = 11.8f };

            var midLeft = new Vector3 { x = 44.1f, y = -2.6f, z = 11.6f };
            var midMid = new Vector3 { x = 44.1f, y = 0f, z = 12.4f };
            var midRight = new Vector3 { x = 44.1f, y = 2.6f, z = 11.6f };

            var tipTop = new Vector3 { x = 49.0f, y = 0f, z = 11.6f };
            var tipLeft = new Vector3 { x = 48.8f, y = -1.3f, z = 10.9f };
            var tipRight = new Vector3 { x = 48.8f, y = 1.3f, z = 10.9f };

            AddQuadOutward(tris, backLeft, backMid, midMid, midLeft, HeadCenter, furLight);
            AddQuadOutward(tris, backMid, backRight, midRight, midMid, HeadCenter, furLight);
            AddQuadOutward(tris, backRight, backLeft, midLeft, midRight, HeadCenter, furMid);

            tris.Add(CreateTriangleOutward(midLeft, midMid, tipTop, HeadCenter, furLight));
            tris.Add(CreateTriangleOutward(midMid, midRight, tipTop, HeadCenter, furLight));
            tris.Add(CreateTriangleOutward(midLeft, tipLeft, tipTop, HeadCenter, furMid));
            tris.Add(CreateTriangleOutward(midRight, tipTop, tipRight, HeadCenter, furMid));
            tris.Add(CreateTriangleOutward(midLeft, midRight, tipRight, HeadCenter, furDark));
            tris.Add(CreateTriangleOutward(midLeft, tipRight, tipLeft, HeadCenter, furDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? LowerJaw()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var backLeft = new Vector3 { x = 38.6f, y = -3.6f, z = 10.8f };
            var backRight = new Vector3 { x = 38.6f, y = 3.6f, z = 10.8f };
            var backBottom = new Vector3 { x = 38.6f, y = 0f, z = 9.1f };

            var midLeft = new Vector3 { x = 44.2f, y = -2.9f, z = 10.2f };
            var midRight = new Vector3 { x = 44.2f, y = 2.9f, z = 10.2f };
            var midBottom = new Vector3 { x = 44.2f, y = 0f, z = 8.2f };

            var tipTop = new Vector3 { x = 49.0f, y = 0f, z = 10.3f };
            var tipBottom = new Vector3 { x = 49.2f, y = 0f, z = 8.4f };

            AddQuadOutward(tris, backLeft, backRight, midRight, midLeft, HeadCenter, furMid);
            AddQuadOutward(tris, backLeft, midLeft, midBottom, backBottom, HeadCenter, furDark);
            AddQuadOutward(tris, backBottom, midBottom, midRight, backRight, HeadCenter, furDark);

            tris.Add(CreateTriangleOutward(midLeft, midRight, tipTop, HeadCenter, furMid));
            tris.Add(CreateTriangleOutward(midLeft, tipTop, tipBottom, HeadCenter, furDark));
            tris.Add(CreateTriangleOutward(midRight, tipBottom, tipTop, HeadCenter, furDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? Ears()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddEar(tris, -1f);
            AddEar(tris, 1f);

            return tris;
        }

        private static void AddEar(List<ITriangleMeshWithColor> tris, float side)
        {
            var a = new Vector3 { x = 27f, y = side * 3.8f, z = 16.8f };
            var b = new Vector3 { x = 30f, y = side * 6.0f, z = 19.3f };
            var c = new Vector3 { x = 33f, y = side * 3.8f, z = 16.5f };
            var d = new Vector3 { x = 30f, y = side * 4.8f, z = 17.2f };

            tris.Add(CreateTriangleOutward(a, b, c, HeadCenter, furMid));
            tris.Add(CreateTriangleOutward(a, d, b, HeadCenter, furDark));
            tris.Add(CreateTriangleOutward(d, c, b, HeadCenter, furShadow));
        }

        public static List<ITriangleMeshWithColor>? EyesAndNose()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddEye(tris, -1f);
            AddEye(tris, 1f);

            tris.Add(CreateTriangleOutward(
                new Vector3 { x = 49.2f, y = -1.4f, z = 11.7f },
                new Vector3 { x = 49.2f, y = 1.4f, z = 11.7f },
                new Vector3 { x = 50.2f, y = 0f, z = 10.3f },
                HeadCenter,
                noseBlack));

            return tris;
        }

        private static void AddEye(List<ITriangleMeshWithColor> tris, float side)
        {
            tris.Add(CreateTriangleOutward(
                new Vector3 { x = 38.5f, y = side * 3.2f, z = 14.9f },
                new Vector3 { x = 40.1f, y = side * 3.5f, z = 14.4f },
                new Vector3 { x = 39.2f, y = side * 3.1f, z = 13.1f },
                HeadCenter,
                eyeBlack));
        }

        // ----------------------------------------------------
        //  LEGS / PAWS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? LegsAndPaws()
        {
            var tris = new List<ITriangleMeshWithColor>();

            AddLeg(tris, 10f, -7.5f, front: true);
            AddLeg(tris, 10f, 7.5f, front: true);
            AddLeg(tris, -20f, -7.5f, front: false);
            AddLeg(tris, -20f, 7.5f, front: false);

            return tris;
        }

        private static void AddLeg(List<ITriangleMeshWithColor> tris, float x, float y, bool front)
        {
            float upperHeight = front ? 8f : 7f;
            float pawLength = front ? 8.2f : 7.8f;

            var legMin = new Vector3 { x = x - 3.2f, y = y - 2.8f, z = 1.2f };
            var legMax = new Vector3 { x = x + 3.2f, y = y + 2.8f, z = upperHeight };

            var center = new Vector3 { x = x, y = y, z = 4f };

            tris.AddRange(CreateBox(legMin, legMax, center, front ? furMid : furDark));

            // Big snow-boot paw, stable and chunky.
            var pawMin = new Vector3 { x = x - pawLength * 0.48f, y = y - 3.1f, z = 0f };
            var pawMax = new Vector3 { x = x + pawLength * 0.58f, y = y + 3.1f, z = 1.9f };

            tris.AddRange(CreateBox(pawMin, pawMax, center, front ? furLight : furMid));
        }

        public static List<ITriangleMeshWithColor>? Tail()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var top = new Vector3 { x = -38.5f, y = 0f, z = 13f };
            var bottom = new Vector3 { x = -38.5f, y = 0f, z = 9f };
            var left = new Vector3 { x = -41.5f, y = -2.2f, z = 11f };
            var right = new Vector3 { x = -41.5f, y = 2.2f, z = 11f };
            var back = new Vector3 { x = -44f, y = 0f, z = 11f };

            tris.Add(CreateTriangleOutward(top, right, back, BodyCenter, furMid));
            tris.Add(CreateTriangleOutward(top, back, left, BodyCenter, furMid));
            tris.Add(CreateTriangleOutward(bottom, back, right, BodyCenter, furDark));
            tris.Add(CreateTriangleOutward(bottom, left, back, BodyCenter, furDark));

            return tris;
        }

        // ----------------------------------------------------
        //  CRASHBOXES / SHADOW
        // ----------------------------------------------------

        public static List<List<IVector3>>? PolarBearCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -42f * CrashboxSize, y = -15f * CrashboxSize, z = 0f },
                    new Vector3 { x = 22f * CrashboxSize, y = 15f * CrashboxSize, z = 29f * CrashboxSize }
                ),

                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = 20f * CrashboxSize, y = -8f * CrashboxSize, z = 7f },
                    new Vector3 { x = 51f * CrashboxSize, y = 8f * CrashboxSize, z = 20f * CrashboxSize }
                ),

                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -28f * CrashboxSize, y = -12f * CrashboxSize, z = 0f },
                    new Vector3 { x = 16f * CrashboxSize, y = 12f * CrashboxSize, z = 8f * CrashboxSize }
                )
            };
        }

        private static List<ITriangleMeshWithColor> PolarBearShadow()
        {
            var tris = new List<ITriangleMeshWithColor>();
            const string sc = _3dObjectHelpers.ShadowColorHex;

            AddShadowRect(tris, -42f, 22f, 0f, 18f, sc);
            AddShadowRect(tris, 18f, 51f, 6f, 16f, sc);
            AddShadowRect(tris, -25f, -12f, -5f, 5f, sc);
            AddShadowRect(tris, 5f, 17f, -5f, 5f, sc);

            return tris;
        }

        private static void AddShadowRect(List<ITriangleMeshWithColor> tris, float x1, float x2, float z1, float z2, string color)
        {
            var a = new Vector3 { x = x1, y = 0f, z = z1 };
            var b = new Vector3 { x = x2, y = 0f, z = z1 };
            var c = new Vector3 { x = x2, y = 0f, z = z2 };
            var d = new Vector3 { x = x1, y = 0f, z = z2 };

            tris.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = b, vert3 = c });
            tris.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = c, vert3 = d });
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static List<Vector3> Ring(float x, float y, float z, float radiusY, float radiusZ, int segments)
        {
            var points = new List<Vector3>(segments);
            float step = MathF.PI * 2f / segments;

            for (int i = 0; i < segments; i++)
            {
                float a = i * step;

                points.Add(new Vector3
                {
                    x = x,
                    y = y + MathF.Cos(a) * radiusY,
                    z = z + MathF.Sin(a) * radiusZ
                });
            }

            return points;
        }

        private static void StitchRings(List<ITriangleMeshWithColor> tris, List<Vector3> a, List<Vector3> b, Vector3 center)
        {
            for (int i = 0; i < a.Count; i++)
            {
                int next = (i + 1) % a.Count;
                AddQuadOutward(tris, a[i], a[next], b[next], b[i], center, GetFurColor(i));
            }
        }

        private static string GetFurColor(int i)
        {
            switch (i % 4)
            {
                case 0:
                    return furLight;
                case 1:
                    return furMid;
                case 2:
                    return furDark;
                default:
                    return furShadow;
            }
        }

        private static List<ITriangleMeshWithColor> CreateBox(Vector3 min, Vector3 max, Vector3 center, string color)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var p000 = new Vector3 { x = min.x, y = min.y, z = min.z };
            var p001 = new Vector3 { x = min.x, y = min.y, z = max.z };
            var p010 = new Vector3 { x = min.x, y = max.y, z = min.z };
            var p011 = new Vector3 { x = min.x, y = max.y, z = max.z };
            var p100 = new Vector3 { x = max.x, y = min.y, z = min.z };
            var p101 = new Vector3 { x = max.x, y = min.y, z = max.z };
            var p110 = new Vector3 { x = max.x, y = max.y, z = min.z };
            var p111 = new Vector3 { x = max.x, y = max.y, z = max.z };

            AddQuadOutward(tris, p001, p101, p111, p011, center, color);
            AddQuadOutward(tris, p100, p000, p010, p110, center, furShadow);
            AddQuadOutward(tris, p101, p100, p110, p111, center, color);
            AddQuadOutward(tris, p000, p001, p011, p010, center, furDark);
            AddQuadOutward(tris, p011, p111, p110, p010, center, color);
            AddQuadOutward(tris, p100, p101, p001, p000, center, furDark);

            return tris;
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

        private static void ApplyAxisScaleToObject(_3dObject bear, float scaleX, float scaleY, float scaleZ)
        {
            var scaled = new HashSet<IVector3>(ReferenceComparer.Instance);

            foreach (var part in bear.ObjectParts)
            {
                if (part.Triangles == null)
                    continue;

                foreach (var tri in part.Triangles)
                {
                    ScaleVertex(tri.vert1, scaleX, scaleY, scaleZ, scaled);
                    ScaleVertex(tri.vert2, scaleX, scaleY, scaleZ, scaled);
                    ScaleVertex(tri.vert3, scaleX, scaleY, scaleZ, scaled);
                }
            }
        }

        private static void ScaleVertex(IVector3 vertex, float scaleX, float scaleY, float scaleZ, HashSet<IVector3> scaled)
        {
            if (!scaled.Add(vertex))
                return;

            vertex.x *= scaleX;
            vertex.y *= scaleY;
            vertex.z *= scaleZ;
        }

        private static void ApplyAxisScaleToCrashBoxes(List<List<IVector3>>? boxes, float scaleX, float scaleY, float scaleZ)
        {
            if (boxes == null)
                return;

            for (int i = 0; i < boxes.Count; i++)
            {
                for (int j = 0; j < boxes[i].Count; j++)
                {
                    boxes[i][j] = new Vector3
                    {
                        x = boxes[i][j].x * scaleX,
                        y = boxes[i][j].y * scaleY,
                        z = boxes[i][j].z * scaleZ
                    };
                }
            }
        }

        private static void ScalePartUniform(_3dObject bear, string partName, float scale)
        {
            var part = bear.ObjectParts.Find(p => p.PartName == partName);
            if (part == null || part.Triangles == null || part.Triangles.Count == 0)
                return;

            float minX = float.MaxValue;
            float minY = float.MaxValue;
            float minZ = float.MaxValue;
            float maxX = float.MinValue;
            float maxY = float.MinValue;
            float maxZ = float.MinValue;

            for (int i = 0; i < part.Triangles.Count; i++)
            {
                UpdateBounds(part.Triangles[i].vert1, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(part.Triangles[i].vert2, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
                UpdateBounds(part.Triangles[i].vert3, ref minX, ref minY, ref minZ, ref maxX, ref maxY, ref maxZ);
            }

            var center = new Vector3
            {
                x = (minX + maxX) * 0.5f,
                y = (minY + maxY) * 0.5f,
                z = (minZ + maxZ) * 0.5f
            };

            var scaled = new HashSet<IVector3>(ReferenceComparer.Instance);
            for (int i = 0; i < part.Triangles.Count; i++)
            {
                ScalePartVertex(part.Triangles[i].vert1, center, scale, scaled);
                ScalePartVertex(part.Triangles[i].vert2, center, scale, scaled);
                ScalePartVertex(part.Triangles[i].vert3, center, scale, scaled);
            }
        }

        private static void UpdateBounds(
            IVector3 vertex,
            ref float minX,
            ref float minY,
            ref float minZ,
            ref float maxX,
            ref float maxY,
            ref float maxZ)
        {
            minX = Math.Min(minX, vertex.x);
            minY = Math.Min(minY, vertex.y);
            minZ = Math.Min(minZ, vertex.z);
            maxX = Math.Max(maxX, vertex.x);
            maxY = Math.Max(maxY, vertex.y);
            maxZ = Math.Max(maxZ, vertex.z);
        }

        private static void ScalePartVertex(IVector3 vertex, Vector3 center, float scale, HashSet<IVector3> scaled)
        {
            if (!scaled.Add(vertex))
                return;

            vertex.x = center.x + ((vertex.x - center.x) * scale);
            vertex.y = center.y + ((vertex.y - center.y) * scale);
            vertex.z = center.z + ((vertex.z - center.z) * scale);
        }

        private sealed class ReferenceComparer : IEqualityComparer<IVector3>
        {
            public static readonly ReferenceComparer Instance = new();

            public bool Equals(IVector3? x, IVector3? y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(IVector3 obj)
            {
                return RuntimeHelpers.GetHashCode(obj);
            }
        }
    }
}
