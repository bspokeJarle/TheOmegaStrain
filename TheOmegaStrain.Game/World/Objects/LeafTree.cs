using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class LeafTree
    {
        public static readonly string[] LeafColors =
        {
            "8CCB55",
            "AFD45D",
            "DDD35F",
            "E8AE4D",
            "D8893F"
        };

        private static readonly string[] TrunkColors = { "7A431F", "8B5428", "5F351C" };
        private const float TrunkRadius = 4.5f;
        private const float TrunkHeight = 18f;
        private const float CrownRadius = 34f;
        private const float CrownHeight = 36f;

        public static _3dObject CreateLeafTree(ISurface parentSurface)
        {
            var trunkAndBranches = TrunkTriangles();
            trunkAndBranches.AddRange(BranchTriangles());

            var tree = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            tree.HasShadow = true;

            tree.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "TreeTrunk",
                Triangles = trunkAndBranches,
                IsVisible = true
            });

            tree.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "TreeFoliage",
                Triangles = LeafTriangles(),
                IsVisible = true
            });

            tree.ObjectOffsets = new Vector3();
            tree.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            tree.ParentSurface = parentSurface;
            tree.ShadowOffset = new Vector3 { x = -10, y = 0, z = -10 };
            tree.CrashBoxes = LeafTreeCrashBoxes();

            _3dObjectHelpers.AddCustomShadowPart(tree, LeafTreeShadow());
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(tree);

            return tree;
        }

        private static List<ITriangleMeshWithColor> TrunkTriangles()
        {
            var trunk = new List<ITriangleMeshWithColor>();
            const int segments = 9;

            for (int i = 0; i < segments; i++)
            {
                float angle1 = i * (2f * System.MathF.PI / segments);
                float angle2 = (i + 1) * (2f * System.MathF.PI / segments);
                string color = TrunkColors[i % TrunkColors.Length];

                var v1 = new Vector3 { x = TrunkRadius * System.MathF.Cos(angle1), y = TrunkRadius * System.MathF.Sin(angle1), z = 0f };
                var v2 = new Vector3 { x = TrunkRadius * System.MathF.Cos(angle2), y = TrunkRadius * System.MathF.Sin(angle2), z = 0f };
                var v3 = new Vector3 { x = TrunkRadius * 0.72f * System.MathF.Cos(angle1), y = TrunkRadius * 0.72f * System.MathF.Sin(angle1), z = TrunkHeight };
                var v4 = new Vector3 { x = TrunkRadius * 0.72f * System.MathF.Cos(angle2), y = TrunkRadius * 0.72f * System.MathF.Sin(angle2), z = TrunkHeight };

                trunk.Add(new TriangleMeshWithColor { Color = color, vert1 = v1, vert2 = v2, vert3 = v3 });
                trunk.Add(new TriangleMeshWithColor { Color = color, vert1 = v2, vert2 = v4, vert3 = v3 });
            }

            return trunk;
        }

        private static List<ITriangleMeshWithColor> BranchTriangles()
        {
            var branches = new List<ITriangleMeshWithColor>();

            AddBranch(branches, V(0f, 0f, 11f), V(-24f, -8f, 31f), 3.3f, 1.2f, 0);
            AddBranch(branches, V(0f, 0f, 13f), V(22f, -12f, 34f), 3.1f, 1.1f, 1);
            AddBranch(branches, V(0f, 0f, 15f), V(-14f, 22f, 38f), 2.8f, 1.0f, 2);
            AddBranch(branches, V(0f, 0f, 16f), V(18f, 21f, 40f), 2.7f, 1.0f, 0);
            AddBranch(branches, V(-2f, 0f, 18f), V(-30f, 6f, 43f), 2.4f, 0.9f, 1);
            AddBranch(branches, V(2f, 0f, 19f), V(30f, 6f, 44f), 2.4f, 0.9f, 2);
            AddBranch(branches, V(0f, -1f, 20f), V(-9f, -27f, 45f), 2.1f, 0.8f, 0);
            AddBranch(branches, V(0f, 1f, 21f), V(8f, 27f, 47f), 2.1f, 0.8f, 1);

            return branches;
        }

        private static List<ITriangleMeshWithColor> LeafTriangles()
        {
            var leaves = new List<ITriangleMeshWithColor>();

            AddLeafCluster(leaves, V(-24f, -8f, 31f), 0);
            AddLeafCluster(leaves, V(22f, -12f, 34f), 1);
            AddLeafCluster(leaves, V(-14f, 22f, 38f), 2);
            AddLeafCluster(leaves, V(18f, 21f, 40f), 3);
            AddLeafCluster(leaves, V(-30f, 6f, 43f), 4);
            AddLeafCluster(leaves, V(30f, 6f, 44f), 5);
            AddLeafCluster(leaves, V(-9f, -27f, 45f), 6);
            AddLeafCluster(leaves, V(8f, 27f, 47f), 7);
            AddLeafCluster(leaves, V(0f, 0f, 49f), 8);

            return leaves;
        }

        private static void AddBranch(
            List<ITriangleMeshWithColor> branches,
            Vector3 start,
            Vector3 end,
            float startWidth,
            float endWidth,
            int colorOffset)
        {
            float dx = end.x - start.x;
            float dy = end.y - start.y;
            float length = System.MathF.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f)
                length = 1f;

            float px = -dy / length;
            float py = dx / length;
            float startHalf = startWidth * 0.5f;
            float endHalf = endWidth * 0.5f;
            string color = TrunkColors[colorOffset % TrunkColors.Length];

            var s1 = V(start.x + px * startHalf, start.y + py * startHalf, start.z);
            var s2 = V(start.x - px * startHalf, start.y - py * startHalf, start.z);
            var e1 = V(end.x + px * endHalf, end.y + py * endHalf, end.z);
            var e2 = V(end.x - px * endHalf, end.y - py * endHalf, end.z);

            branches.Add(new TriangleMeshWithColor { Color = color, noHidden = true, vert1 = s1, vert2 = s2, vert3 = e1 });
            branches.Add(new TriangleMeshWithColor { Color = color, noHidden = true, vert1 = s2, vert2 = e2, vert3 = e1 });
        }

        private static void AddLeafCluster(List<ITriangleMeshWithColor> leaves, Vector3 anchor, int seed)
        {
            AddLeaf(leaves, Offset(anchor, -4f, -2f, -1f), 7.5f, 11f, -0.45f + seed * 0.07f, seed);
            AddLeaf(leaves, Offset(anchor, 4f, -1f, 2f), 6.6f, 10f, 0.35f + seed * 0.05f, seed + 1);
            AddLeaf(leaves, Offset(anchor, -1f, 5f, 1f), 6.2f, 9.5f, 1.15f + seed * 0.04f, seed + 2);
            AddLeaf(leaves, Offset(anchor, 2f, 1f, 5f), 5.8f, 8.8f, -1.05f + seed * 0.06f, seed + 3);
        }

        private static void AddLeaf(
            List<ITriangleMeshWithColor> leaves,
            Vector3 center,
            float width,
            float height,
            float angle,
            int colorOffset)
        {
            float cos = System.MathF.Cos(angle);
            float sin = System.MathF.Sin(angle);
            float halfWidth = width * 0.5f;

            var baseLeft = V(
                center.x - cos * halfWidth - sin * width * 0.18f,
                center.y - sin * halfWidth + cos * width * 0.18f,
                center.z - height * 0.25f);

            var baseRight = V(
                center.x + cos * halfWidth - sin * width * 0.18f,
                center.y + sin * halfWidth + cos * width * 0.18f,
                center.z - height * 0.25f);

            var tip = V(
                center.x + cos * height * 0.35f,
                center.y + sin * height * 0.35f,
                center.z + height * 0.72f);

            leaves.Add(new TriangleMeshWithColor
            {
                Color = LeafColors[colorOffset % LeafColors.Length],
                noHidden = true,
                vert1 = baseLeft,
                vert2 = baseRight,
                vert3 = tip
            });
        }

        public static List<List<IVector3>> LeafTreeCrashBoxes()
        {
            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -11f, y = -11f, z = 0f },
                    new Vector3 { x = 11f, y = 11f, z = TrunkHeight }),
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -CrownRadius, y = -CrownRadius * 0.85f, z = TrunkHeight },
                    new Vector3 { x = CrownRadius, y = CrownRadius * 0.85f, z = TrunkHeight + CrownHeight * 0.65f }),
                _3dObjectHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -CrownRadius * 0.55f, y = -CrownRadius * 0.55f, z = TrunkHeight + CrownHeight * 0.55f },
                    new Vector3 { x = CrownRadius * 0.55f, y = CrownRadius * 0.55f, z = TrunkHeight + CrownHeight })
            };
        }

        private static List<ITriangleMeshWithColor> LeafTreeShadow()
        {
            var shadow = new List<ITriangleMeshWithColor>();
            const string sc = _3dObjectHelpers.ShadowColorHex;

            AddShadowQuad(shadow, V(-4f, 0f, 0f), V(4f, 0f, 0f), V(4f, 0f, 18f), V(-4f, 0f, 18f), sc);

            var basePoint = V(0f, 0f, 15f);
            AddShadowBranch(shadow, basePoint, V(-27f, 0f, 31f), sc);
            AddShadowBranch(shadow, basePoint, V(24f, 0f, 33f), sc);
            AddShadowBranch(shadow, basePoint, V(-17f, 0f, 41f), sc);
            AddShadowBranch(shadow, basePoint, V(18f, 0f, 43f), sc);

            shadow.Add(new TriangleMeshWithColor { Color = sc, vert1 = V(-34f, 0f, 25f), vert2 = V(-12f, 0f, 50f), vert3 = V(-4f, 0f, 34f) });
            shadow.Add(new TriangleMeshWithColor { Color = sc, vert1 = V(34f, 0f, 27f), vert2 = V(9f, 0f, 52f), vert3 = V(3f, 0f, 34f) });
            shadow.Add(new TriangleMeshWithColor { Color = sc, vert1 = V(-16f, 0f, 38f), vert2 = V(16f, 0f, 40f), vert3 = V(0f, 0f, 58f) });

            return shadow;
        }

        private static void AddShadowQuad(
            List<ITriangleMeshWithColor> shadow,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            string color)
        {
            shadow.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = b, vert3 = c });
            shadow.Add(new TriangleMeshWithColor { Color = color, vert1 = a, vert2 = c, vert3 = d });
        }

        private static void AddShadowBranch(List<ITriangleMeshWithColor> shadow, Vector3 start, Vector3 end, string color)
        {
            shadow.Add(new TriangleMeshWithColor
            {
                Color = color,
                vert1 = start,
                vert2 = V(end.x - 3f, 0f, end.z - 2f),
                vert3 = V(end.x + 3f, 0f, end.z + 2f)
            });
        }

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3 { x = x, y = y, z = z };
        }

        private static Vector3 Offset(Vector3 source, float x, float y, float z)
        {
            return new Vector3 { x = source.x + x, y = source.y + y, z = source.z + z };
        }
    }
}
