using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using System.Collections.Generic;
using System.Linq;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class Tree
    {
        private static float trunkRadius = 2.5f; // Defined trunk radius
        private static float trunkHeight = 12f;
        private static float foliageBaseRadius = 8.5f; // Increased foliage mass
        private static float foliageHeight = 3.5f; // Squashed foliage into a denser form

        public static OmegaObject3D CreateTree(ISurface parentSurface)
        {
            var trunkTriangles = TrunkTriangles();
            var foliageTriangles = FoliageTriangles();
            var treeCrashBox = TreeCrashBoxes();
            var tree = new OmegaObject3D{ ObjectId = GameState.ObjectIdCounter++ };
            tree.HasShadow = true;

            if (trunkTriangles != null)
                tree.ObjectParts.Add(new OmegaObjectPart3D { PartName = "TreeTrunk", Triangles = trunkTriangles, IsVisible = true });

            if (foliageTriangles != null)
                tree.ObjectParts.Add(new OmegaObjectPart3D { PartName = "TreeFoliage", Triangles = foliageTriangles, IsVisible = true });

            tree.ObjectOffsets = new Vector3 { };
            tree.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            tree.ParentSurface = parentSurface;

            // Shadow fine-tuning: nudge shadow 10 units to the left and 10 units
            // closer to the camera so it sits tight against the trunk base.
            tree.ShadowOffset = new Vector3 { x = -10, y = 0, z = -10 };

            tree.CrashBoxes = treeCrashBox;

            // Hand-authored low-poly shadow silhouette.
            // Side-on profile (X–Z plane, Y = 0) that the shadow projector
            // skews along the ground. Use as many triangles as needed to make
            // the outline read — no hard limit. Tree here = 8 tris: a narrow
            // trunk + a rounded, slightly asymmetric crown.
            var shadowTris = new List<ITriangleMeshWithColor>(8);
            const string sc = OmegaObject3DHelpers.ShadowColorHex;

            // --- Trunk (2 tris) ---
            var tA = new Vector3 { x = -4f, y = 0f, z = 0f };
            var tB = new Vector3 { x = 4f, y = 0f, z = 0f };
            var tC = new Vector3 { x = 4f, y = 0f, z = 16f };
            var tD = new Vector3 { x = -4f, y = 0f, z = 16f };
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = tA, vert2 = tB, vert3 = tC });
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = tA, vert2 = tC, vert3 = tD });

            // --- Canopy (6 tris, fan-ish around a central axis) ---
            // Central "core" of the crown — tall slim diamond.
            var cBot = new Vector3 { x = 0f, y = 0f, z = 14f };
            var cTop = new Vector3 { x = 0f, y = 0f, z = 55f };
            var cL1 = new Vector3 { x = -22f, y = 0f, z = 22f };  // lower-left shoulder
            var cL2 = new Vector3 { x = -17f, y = 0f, z = 38f };  // upper-left shoulder
            var cR1 = new Vector3 { x = 24f, y = 0f, z = 20f };   // lower-right (slightly wider — asymmetric)
            var cR2 = new Vector3 { x = 15f, y = 0f, z = 40f };   // upper-right shoulder

            // Left side: 2 tris
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = cBot, vert2 = cL1, vert3 = cL2 });
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = cBot, vert2 = cL2, vert3 = cTop });
            // Right side: 2 tris
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = cBot, vert2 = cR2, vert3 = cR1 });
            shadowTris.Add(new TriangleMeshWithColor { Color = sc, vert1 = cBot, vert2 = cTop, vert3 = cR2 });

            // Small lower "bulge" to round out the crown base.
            shadowTris.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = new Vector3 { x = -18f, y = 0f, z = 16f },
                vert2 = new Vector3 { x = 20f, y = 0f, z = 16f },
                vert3 = new Vector3 { x = 0f, y = 0f, z = 26f }
            });
            // Mid crown connector so the profile reads as one blob, not spokes.
            shadowTris.Add(new TriangleMeshWithColor
            {
                Color = sc,
                vert1 = cL1,
                vert2 = cR1,
                vert3 = new Vector3 { x = 0f, y = 0f, z = 32f }
            });

            OmegaObject3DHelpers.AddCustomShadowPart(tree, shadowTris);
            OmegaObject3DHelpers.NormalizeSurfaceFootprintPivot(tree);

            return tree;
        }

        public static List<ITriangleMeshWithColor>? TrunkTriangles()
        {
            var trunk = new List<ITriangleMeshWithColor>();
            int trunkSegments = 10;
            float radius = 5;
            float height = 15;
            string[] trunkColors = { "8B4513", "A0522D", "6D4C41" }; // Different shades of brown

            for (int i = 0; i < trunkSegments; i++)
            {
                float angle1 = (float)(i * (2 * System.Math.PI / trunkSegments));
                float angle2 = (float)((i + 1) * (2 * System.Math.PI / trunkSegments));
                string color = trunkColors[i % trunkColors.Length];

                var v1 = new Vector3 { x = radius * System.MathF.Cos(angle1), y = radius * System.MathF.Sin(angle1), z = 0 };
                var v2 = new Vector3 { x = radius * System.MathF.Cos(angle2), y = radius * System.MathF.Sin(angle2), z = 0 };
                var v3 = new Vector3 { x = radius * System.MathF.Cos(angle1), y = radius * System.MathF.Sin(angle1), z = height };
                var v4 = new Vector3 { x = radius * System.MathF.Cos(angle2), y = radius * System.MathF.Sin(angle2), z = height };

                trunk.Add(new TriangleMeshWithColor { Color = color, vert1 = v1, vert2 = v2, vert3 = v3 });
                trunk.Add(new TriangleMeshWithColor { Color = color, vert1 = v2, vert2 = v4, vert3 = v3 });
            }
            return trunk;
        }

        public static List<ITriangleMeshWithColor>? FoliageTriangles()
        {
            var foliage = new List<ITriangleMeshWithColor>();
            int foliageLayers = 4;
            float baseRadius = 24;
            float heightStep = 8;
            float topHeight = 45;
            float overlap = 4;
            float initialFoliageHeight = 15;
            string[] foliageColors = { "007700", "008800", "009900" }; // Different shades of green

            for (int layer = 0; layer < foliageLayers; layer++)
            {
                float layerHeight = initialFoliageHeight + (layer * (heightStep - 2));
                float layerRadius = baseRadius * (1.0f - (layer * 0.3f));
                int segments = 10 + (layer * 3);
                string color = foliageColors[layer % foliageColors.Length];

                for (int i = 0; i < segments; i++)
                {
                    float angle1 = (float)(i * (2 * System.Math.PI / segments));
                    float angle2 = (float)((i + 1) * (2 * System.Math.PI / segments));

                    var v1 = new Vector3 { x = layerRadius * System.MathF.Cos(angle1), y = layerRadius * System.MathF.Sin(angle1), z = layerHeight - overlap };
                    var v2 = new Vector3 { x = layerRadius * System.MathF.Cos(angle2), y = layerRadius * System.MathF.Sin(angle2), z = layerHeight - overlap };
                    var tip = new Vector3 { x = 0, y = 0, z = layerHeight + heightStep };

                    foliage.Add(new TriangleMeshWithColor { Color = color, vert1 = v1, vert2 = v2, vert3 = tip });
                }
            }
            return foliage;
        }

        public static List<List<IVector3>> TreeCrashBoxes()
        {
            float trunkExpand = trunkRadius * 2.5f;
            float foliageExpand = foliageBaseRadius * 2.5f;
            float foliageHeightExpanded = foliageHeight * 2.5f;

            return new List<List<IVector3>>
            {
                // Trunk lower half
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -trunkExpand, y = -trunkExpand, z = 0 },
                    new Vector3 { x = trunkExpand, y = trunkExpand, z = trunkHeight / 2 }
                ),

                // Trunk upper half
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -trunkExpand - 1, y = -trunkExpand - 1, z = trunkHeight / 2 },
                    new Vector3 { x = trunkExpand + 1, y = trunkExpand + 1, z = trunkHeight }
                ),

                // Middle foliage
                OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -foliageExpand, y = -foliageExpand, z = trunkHeight },
                    new Vector3 { x = foliageExpand, y = foliageExpand, z = trunkHeight + foliageHeightExpanded }
                ),

                // Top foliage
                 OmegaObject3DHelpers.GenerateCrashBoxCorners(
                    new Vector3 { x = -foliageExpand / 2, y = -foliageExpand / 2, z = trunkHeight + foliageHeightExpanded },
                    new Vector3 { x = foliageExpand / 2, y = foliageExpand / 2, z = trunkHeight + foliageHeightExpanded * 1.5f }
                )
            };
        }
    }
}
