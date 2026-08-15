using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class DesertRockFormation
    {
        private static readonly float[] RotationAngles = { -38f, -22f, -8f, 0f, 17f, 31f, 46f };
        private static readonly string[] RockColors = { "8B6A45", "A37A4D", "6E5238", "B38A58" };

        public static OmegaObject3D CreateDesertRockFormation(ISurface parentSurface)
        {
            int objectId = GameState.ObjectIdCounter++;
            float rotationZ = RotationAngles[Math.Abs(objectId) % RotationAngles.Length];
            var rocks = new OmegaObject3D
            {
                ObjectId = objectId,
                ObjectName = "DesertRockFormation",
                HasShadow = true,
                ObjectOffsets = new Vector3 { },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new DesertRockControls(),
                ShadowOffset = new Vector3 { x = -9f, y = 0f, z = -8f }
            };

            AddPart(rocks, "DesertRockMain", RotateTriangles(CreateRock(0f, 0f, 20f, 14f, 26f, 0), rotationZ), true);
            AddPart(rocks, "DesertRockSideA", RotateTriangles(CreateRock(-23f, -4f, 13f, 9f, 15f, 1), rotationZ), true);
            AddPart(rocks, "DesertRockSideB", RotateTriangles(CreateRock(20f, 8f, 15f, 10f, 18f, 2), rotationZ), true);
            AddPart(rocks, "DesertRockFront", RotateTriangles(CreateRock(-4f, -18f, 10f, 7f, 10f, 3), rotationZ), true);

            rocks.CrashBoxes = DesertRockCrashBoxes();
            rocks.CrashBoxNames = new List<string?> { "RockFormation" };

            OmegaObject3DHelpers.AddCustomShadowPart(rocks, DesertRockShadow(rotationZ));
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(rocks);

            return rocks;
        }

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColor>? triangles, bool visible)
        {
            if (triangles == null) return;

            obj.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = name,
                Triangles = triangles,
                IsVisible = visible
            });
        }

        private static List<ITriangleMeshWithColor> CreateRock(float centerX, float centerY, float radiusX, float radiusY, float height, int colorOffset)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            const int segments = 7;
            var bottom = new List<Vector3>(segments);
            var top = new List<Vector3>(segments);
            var topCenter = new Vector3 { x = centerX + 1.8f, y = centerY - 1.2f, z = height };
            var bottomCenter = new Vector3 { x = centerX, y = centerY, z = 0f };

            for (int i = 0; i < segments; i++)
            {
                float angle = i * MathF.PI * 2f / segments;
                float wobble = 0.86f + (((i + colorOffset) % 3) * 0.11f);
                bottom.Add(new Vector3
                {
                    x = centerX + MathF.Cos(angle) * radiusX * wobble,
                    y = centerY + MathF.Sin(angle) * radiusY * (1.06f - ((i % 2) * 0.12f)),
                    z = 0f
                });
                top.Add(new Vector3
                {
                    x = centerX + 1.8f + MathF.Cos(angle + 0.16f) * radiusX * 0.48f * wobble,
                    y = centerY - 1.2f + MathF.Sin(angle + 0.16f) * radiusY * 0.48f,
                    z = height
                });
            }

            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;
                string color = RockColors[(i + colorOffset) % RockColors.Length];

                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = bottom[i], vert2 = bottom[next], vert3 = top[next] });
                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = bottom[i], vert2 = top[next], vert3 = top[i] });
                triangles.Add(new TriangleMeshWithColor { Color = RockColors[(i + colorOffset + 1) % RockColors.Length], vert1 = topCenter, vert2 = top[i], vert3 = top[next] });
                triangles.Add(new TriangleMeshWithColor { Color = RockColors[(i + colorOffset + 2) % RockColors.Length], vert1 = bottomCenter, vert2 = bottom[next], vert3 = bottom[i] });
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

        public static List<List<IVector3>> DesertRockCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -30f, y = -20f, z = 1f },
                    new Vector3 { x = 30f, y = 20f, z = 22f })
            };
        }

        private static List<ITriangleMeshWithColor> DesertRockShadow(float rotationZ)
        {
            const string sc = OmegaObject3DHelpers.ShadowColorHex;
            var triangles = new List<ITriangleMeshWithColor>();

            AddShadowRock(triangles, 0f, 0f, 23f, 16f, 18f, sc);
            AddShadowRock(triangles, -23f, -4f, 14f, 10f, 10f, sc);
            AddShadowRock(triangles, 20f, 8f, 16f, 11f, 12f, sc);
            AddShadowRock(triangles, -4f, -18f, 11f, 8f, 7f, sc);

            return RotateTriangles(triangles, rotationZ);
        }

        private static void AddShadowRock(
            List<ITriangleMeshWithColor> triangles,
            float centerX,
            float centerY,
            float radiusX,
            float radiusY,
            float height,
            string color)
        {
            var center = new Vector3 { x = centerX, y = centerY, z = height };
            const int segments = 8;

            for (int i = 0; i < segments; i++)
            {
                float a1 = i * MathF.PI * 2f / segments;
                float a2 = (i + 1) * MathF.PI * 2f / segments;
                float wobble1 = 0.88f + ((i % 3) * 0.07f);
                float wobble2 = 0.88f + (((i + 1) % 3) * 0.07f);
                var p1 = new Vector3 { x = centerX + MathF.Cos(a1) * radiusX * wobble1, y = centerY + MathF.Sin(a1) * radiusY, z = 0f };
                var p2 = new Vector3 { x = centerX + MathF.Cos(a2) * radiusX * wobble2, y = centerY + MathF.Sin(a2) * radiusY, z = 0f };
                triangles.Add(new TriangleMeshWithColor { Color = color, vert1 = center, vert2 = p1, vert3 = p2 });
            }
        }
    }
}
