using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class Cactus
    {
        private const float BodyRadius = 7f;
        private const float BodyHeight = 54f;
        private const float ArmRadius = 4.2f;
        private const float ArmReach = 24f;
        private const float ArmHeight = 24f;
        private static readonly float[] RotationAngles = { -45f, -30f, -15f, 0f, 15f, 30f, 45f };

        public static _3dObject CreateCactus(ISurface parentSurface)
        {
            int objectId = GameState.ObjectIdCounter++;
            var cactus = new _3dObject { ObjectId = objectId };
            float rotationZ = RotationAngles[Math.Abs(objectId) % RotationAngles.Length];
            float bodyHeight = BodyHeight;
            float bodyRadius = BodyRadius;
            string[] bodyColors = { "355F2B", "417336", "2C4E24" };
            string[] armColors = { "315A29", "3D6C32", "284821" };
            string[] bloomColors = { "B02D59", "D04A73", "8F244A" };

            cactus.HasShadow = true;

            AddPart(cactus, "CactusBody", RotateTriangles(CreateCappedCylinder(bodyRadius, bodyHeight, bodyColors[0], bodyColors[1], bodyColors[2]), rotationZ), true);

            AddPart(cactus, "CactusLeftArm", RotateTriangles(CreateArm(leftSide: true, attachZ: bodyHeight * 0.50f, reach: ArmReach, height: ArmHeight, armColors), rotationZ), true);
            AddPart(cactus, "CactusRightArm", RotateTriangles(CreateArm(leftSide: false, attachZ: bodyHeight * 0.43f, reach: ArmReach * 0.92f, height: ArmHeight * 0.84f, armColors), rotationZ), true);

            AddPart(cactus, "CactusBloom", RotateTriangles(CreateBloom(bodyHeight, bloomColors), rotationZ), true);

            cactus.ObjectOffsets = new Vector3 { };
            cactus.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            cactus.ParentSurface = parentSurface;
            cactus.Movement = new CactusControls();
            cactus.CrashBoxes = CactusCrashBoxes(bodyHeight);
            cactus.ShadowOffset = new Vector3 { x = -7, y = 0, z = -8 };

            _3dObjectHelpers.AddCustomShadowPart(cactus, CactusShadow(rotationZ, bodyHeight));
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(cactus);

            return cactus;
        }

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor>? triangles, bool visible)
        {
            if (triangles == null) return;

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = triangles,
                IsVisible = visible
            });
        }

        private static List<ITriangleMeshWithColor> CreateArm(bool leftSide, float attachZ, float reach, float height, string[] colors)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            float side = leftSide ? -1f : 1f;

            triangles.AddRange(CreateHorizontalPrism(
                startX: side * BodyRadius * 0.35f,
                endX: side * reach,
                centerY: 0f,
                centerZ: attachZ,
                radius: ArmRadius,
                colors));

            triangles.AddRange(CreateCappedCylinder(
                ArmRadius,
                height,
                colors[0],
                colors[1],
                colors[2],
                centerX: side * reach,
                centerY: 0f,
                baseZ: attachZ - 2f));

            return triangles;
        }

        private static List<ITriangleMeshWithColor> CreateBloom(float bodyHeight, string[] colors)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            var top = new Vector3 { x = 0f, y = 0f, z = bodyHeight + 7f };
            var center = new Vector3 { x = 0f, y = 0f, z = bodyHeight + 1.5f };
            const int petals = 6;
            const float radius = 5.2f;

            for (int i = 0; i < petals; i++)
            {
                float a1 = i * MathF.PI * 2f / petals;
                float a2 = (i + 1) * MathF.PI * 2f / petals;
                var p1 = new Vector3 { x = MathF.Cos(a1) * radius, y = MathF.Sin(a1) * radius, z = bodyHeight + 2f };
                var p2 = new Vector3 { x = MathF.Cos(a2) * radius, y = MathF.Sin(a2) * radius, z = bodyHeight + 2f };

                triangles.Add(new TriangleMeshWithColor { Color = colors[i % colors.Length], vert1 = center, vert2 = p1, vert3 = top });
                triangles.Add(new TriangleMeshWithColor { Color = colors[(i + 1) % colors.Length], vert1 = center, vert2 = top, vert3 = p2 });
            }

            return triangles;
        }

        private static List<ITriangleMeshWithColor> CreateCappedCylinder(
            float radius,
            float height,
            string colorA,
            string colorB,
            string colorC,
            float centerX = 0f,
            float centerY = 0f,
            float baseZ = 0f)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            string[] colors = { colorA, colorB, colorC };
            const int segments = 8;
            float topZ = baseZ + height;
            var bottomCenter = new Vector3 { x = centerX, y = centerY, z = baseZ };
            var topCenter = new Vector3 { x = centerX, y = centerY, z = topZ };

            for (int i = 0; i < segments; i++)
            {
                float a1 = i * MathF.PI * 2f / segments;
                float a2 = (i + 1) * MathF.PI * 2f / segments;
                string color = colors[i % colors.Length];

                var b1 = new Vector3 { x = centerX + radius * MathF.Cos(a1), y = centerY + radius * MathF.Sin(a1), z = baseZ };
                var b2 = new Vector3 { x = centerX + radius * MathF.Cos(a2), y = centerY + radius * MathF.Sin(a2), z = baseZ };
                var t1 = new Vector3 { x = centerX + radius * MathF.Cos(a1) * 0.78f, y = centerY + radius * MathF.Sin(a1) * 0.78f, z = topZ };
                var t2 = new Vector3 { x = centerX + radius * MathF.Cos(a2) * 0.78f, y = centerY + radius * MathF.Sin(a2) * 0.78f, z = topZ };

                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = b1, vert2 = b2, vert3 = t1 });
                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = b2, vert2 = t2, vert3 = t1 });
                triangles.Add(new TriangleMeshWithColor { Color = colorC, vert1 = topCenter, vert2 = t1, vert3 = t2 });
                triangles.Add(new TriangleMeshWithColor { Color = colorA, vert1 = bottomCenter, vert2 = b2, vert3 = b1 });
            }

            return triangles;
        }

        private static List<ITriangleMeshWithColor> CreateHorizontalPrism(
            float startX,
            float endX,
            float centerY,
            float centerZ,
            float radius,
            string[] colors)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            const int segments = 6;
            for (int i = 0; i < segments; i++)
            {
                float a1 = i * MathF.PI * 2f / segments;
                float a2 = (i + 1) * MathF.PI * 2f / segments;
                string color = colors[i % colors.Length];

                var s1 = new Vector3 { x = startX, y = centerY + radius * MathF.Cos(a1), z = centerZ + radius * MathF.Sin(a1) };
                var s2 = new Vector3 { x = startX, y = centerY + radius * MathF.Cos(a2), z = centerZ + radius * MathF.Sin(a2) };
                var e1 = new Vector3 { x = endX, y = centerY + radius * MathF.Cos(a1), z = centerZ + radius * MathF.Sin(a1) };
                var e2 = new Vector3 { x = endX, y = centerY + radius * MathF.Cos(a2), z = centerZ + radius * MathF.Sin(a2) };

                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = s1, vert2 = s2, vert3 = e1 });
                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = s2, vert2 = e2, vert3 = e1 });
            }

            return triangles;
        }

        private static List<ITriangleMeshWithColor> RotateTriangles(List<ITriangleMeshWithColor> triangles, float degrees)
        {
            float radians = degrees * MathF.PI / 180f;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);
            var rotated = new List<ITriangleMeshWithColor>(triangles.Count);

            foreach (var triangle in triangles)
            {
                rotated.Add(new TriangleMeshWithColor
                {
                    Color = triangle.Color,
                    noHidden = triangle.noHidden,
                    landBasedPosition = triangle.landBasedPosition,
                    angle = triangle.angle,
                    normal1 = CopyVector(triangle.normal1),
                    normal2 = CopyVector(triangle.normal2),
                    normal3 = CopyVector(triangle.normal3),
                    vert1 = RotateVector(triangle.vert1, cos, sin),
                    vert2 = RotateVector(triangle.vert2, cos, sin),
                    vert3 = RotateVector(triangle.vert3, cos, sin)
                });
            }

            return rotated;
        }

        private static Vector3 RotateVector(IVector3 vector, float cos, float sin)
        {
            float x = vector.x;
            float y = vector.y;
            return new Vector3
            {
                x = (x * cos) - (y * sin),
                y = (x * sin) + (y * cos),
                z = vector.z
            };
        }

        private static Vector3? CopyVector(IVector3? vector)
        {
            if (vector == null) return null;

            return new Vector3
            {
                x = vector.x,
                y = vector.y,
                z = vector.z
            };
        }

        private static List<ITriangleMeshWithColor> CactusShadow(float rotationZ, float bodyHeight)
        {
            const string sc = _3dObjectHelpers.ShadowColorHex;
            var triangles = new List<ITriangleMeshWithColor>();

            AddShadowStem(triangles, centerX: 0f, halfWidth: BodyRadius * 1.05f, baseY: -BodyRadius, topY: BodyRadius, height: bodyHeight, color: sc);
            AddShadowStem(triangles, centerX: -ArmReach * 0.68f, halfWidth: ArmRadius * 1.1f, baseY: -ArmRadius, topY: ArmRadius, height: bodyHeight * 0.82f, color: sc);
            AddShadowStem(triangles, centerX: ArmReach * 0.62f, halfWidth: ArmRadius, baseY: -ArmRadius, topY: ArmRadius, height: bodyHeight * 0.68f, color: sc);
            AddShadowArm(triangles, -BodyRadius * 0.2f, -ArmReach, bodyHeight * 0.58f, ArmRadius * 1.2f, sc);
            AddShadowArm(triangles, BodyRadius * 0.2f, ArmReach * 0.92f, bodyHeight * 0.50f, ArmRadius, sc);

            return RotateTriangles(triangles, rotationZ);
        }

        private static void AddShadowStem(
            List<ITriangleMeshWithColor> triangles,
            float centerX,
            float halfWidth,
            float baseY,
            float topY,
            float height,
            string color)
        {
            var bottomLeft = new Vector3 { x = centerX - halfWidth, y = baseY, z = 0f };
            var bottomRight = new Vector3 { x = centerX + halfWidth, y = baseY, z = 0f };
            var topLeft = new Vector3 { x = centerX - halfWidth * 0.75f, y = topY, z = height };
            var topRight = new Vector3 { x = centerX + halfWidth * 0.75f, y = topY, z = height };
            triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = bottomLeft, vert2 = bottomRight, vert3 = topRight });
            triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = bottomLeft, vert2 = topRight, vert3 = topLeft });
        }

        private static void AddShadowArm(
            List<ITriangleMeshWithColor> triangles,
            float startX,
            float endX,
            float z,
            float halfWidth,
            string color)
        {
            var a = new Vector3 { x = startX, y = -halfWidth, z = z };
            var b = new Vector3 { x = endX, y = -halfWidth, z = z };
            var c = new Vector3 { x = endX, y = halfWidth, z = z };
            var d = new Vector3 { x = startX, y = halfWidth, z = z };
            triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = b, vert3 = c });
            triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = c, vert3 = d });
        }

        public static List<List<IVector3>> CactusCrashBoxes(float bodyHeight = BodyHeight)
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -5.2f, y = -5.2f, z = 2f },
                    new Vector3 { x = 5.2f, y = 5.2f, z = bodyHeight - 5f }),
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -ArmReach - 1.5f, y = -3.1f, z = 25f },
                    new Vector3 { x = -BodyRadius * 0.35f, y = 3.1f, z = 46f }),
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = BodyRadius * 0.35f, y = -3.1f, z = 24f },
                    new Vector3 { x = ArmReach, y = 3.1f, z = 43f })
            };
        }
    }
}
