using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    // Same shape as SeederGuidanceArrow, recolored yellow and driven by
    // PowerUpGuidanceArrowControl to point at the closest live powerup.
    public class PowerUpGuidanceArrow
    {
        // ----------------------------------------------------
        //  GEOMETRY PARAMETERS
        // ----------------------------------------------------

        // Arrow points in +X direction
        private static float shaftLength = 20f;
        private static float shaftHalfWidth = 4.5f;
        private static float shaftHalfHeight = 2.5f;

        private static float headLength = 16f;
        private static float headHalfWidth = 11f;
        private static float headHalfHeight = 5.5f;

        private static float tailInset = 4f;
        private static float bevelInset = 2.2f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string yellowTop = "FFF98A";
        private static string yellowLight = "FFEA4D";
        private static string yellowMid = "FFD400";
        private static string yellowDark = "C79A00";
        private static string yellowDeep = "7A5C00";

        // ----------------------------------------------------
        //  OBJECT CREATION
        // ----------------------------------------------------

        public static OmegaObject3D CreatePowerUpGuidanceArrow(ISurface parentSurface)
        {
            var body = ArrowBody();
            var head = ArrowHead();
            var bevels = ArrowBevels();

            var arrow = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                HasShadow = false
            };

            AddPart(arrow, "PowerUpGuidanceArrowBody", body, true);
            AddPart(arrow, "PowerUpGuidanceArrowHead", head, true);
            AddPart(arrow, "PowerUpGuidanceArrowBevels", bevels, true);

            arrow.Movement = new PowerUpGuidanceArrowControl();
            return arrow;
        }

        // ----------------------------------------------------
        //  SHAFT / BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColorAndTexture>? ArrowBody()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            float xBack = -shaftLength * 0.5f;
            float xFront = shaftLength * 0.5f;

            var v1 = new Vector3 { x = xBack, y = -shaftHalfWidth, z = shaftHalfHeight };
            var v2 = new Vector3 { x = xBack, y = shaftHalfWidth, z = shaftHalfHeight };
            var v3 = new Vector3 { x = xFront, y = shaftHalfWidth, z = shaftHalfHeight };
            var v4 = new Vector3 { x = xFront, y = -shaftHalfWidth, z = shaftHalfHeight };

            var v5 = new Vector3 { x = xBack, y = -shaftHalfWidth, z = -shaftHalfHeight };
            var v6 = new Vector3 { x = xBack, y = shaftHalfWidth, z = -shaftHalfHeight };
            var v7 = new Vector3 { x = xFront, y = shaftHalfWidth, z = -shaftHalfHeight };
            var v8 = new Vector3 { x = xFront, y = -shaftHalfWidth, z = -shaftHalfHeight };

            // Top
            AddQuadOutward(tris, v1, v2, v3, v4, BodyCenter, yellowTop);

            // Bottom
            AddQuadOutward(tris, v8, v7, v6, v5, BodyCenter, yellowDeep);

            // Left
            AddQuadOutward(tris, v5, v1, v4, v8, BodyCenter, yellowDark);

            // Right
            AddQuadOutward(tris, v2, v6, v7, v3, BodyCenter, yellowMid);

            // Back
            AddQuadOutward(tris, v5, v6, v2, v1, BodyCenter, yellowDark);

            return tris;
        }

        // ----------------------------------------------------
        //  ARROW HEAD
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColorAndTexture>? ArrowHead()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            float shaftFrontX = shaftLength * 0.5f;
            float headBaseX = shaftFrontX - 2f;
            float tipX = shaftFrontX + headLength;

            // Shaft front rectangle
            var shaftTopLeft = new Vector3 { x = shaftFrontX, y = -shaftHalfWidth, z = shaftHalfHeight };
            var shaftTopRight = new Vector3 { x = shaftFrontX, y = shaftHalfWidth, z = shaftHalfHeight };
            var shaftBottomRight = new Vector3 { x = shaftFrontX, y = shaftHalfWidth, z = -shaftHalfHeight };
            var shaftBottomLeft = new Vector3 { x = shaftFrontX, y = -shaftHalfWidth, z = -shaftHalfHeight };

            // Head base rectangle
            var headTopLeft = new Vector3 { x = headBaseX, y = -headHalfWidth, z = headHalfHeight };
            var headTopRight = new Vector3 { x = headBaseX, y = headHalfWidth, z = headHalfHeight };
            var headBottomRight = new Vector3 { x = headBaseX, y = headHalfWidth, z = -headHalfHeight };
            var headBottomLeft = new Vector3 { x = headBaseX, y = -headHalfWidth, z = -headHalfHeight };

            // Tip
            var tip = new Vector3 { x = tipX, y = 0, z = 0 };

            // ----------------------------------------------------
            // Close the transition between shaft and head base
            // ----------------------------------------------------

            // Top bridge
            AddQuadOutward(
                tris,
                shaftTopLeft,
                shaftTopRight,
                headTopRight,
                headTopLeft,
                BodyCenter,
                yellowLight);

            // Bottom bridge
            AddQuadOutward(
                tris,
                headBottomLeft,
                headBottomRight,
                shaftBottomRight,
                shaftBottomLeft,
                BodyCenter,
                yellowDeep);

            // Left bridge
            AddQuadOutward(
                tris,
                shaftBottomLeft,
                shaftTopLeft,
                headTopLeft,
                headBottomLeft,
                BodyCenter,
                yellowDark);

            // Right bridge
            AddQuadOutward(
                tris,
                shaftTopRight,
                shaftBottomRight,
                headBottomRight,
                headTopRight,
                BodyCenter,
                yellowMid);

            // ----------------------------------------------------
            // Arrow head faces toward the tip
            // ----------------------------------------------------

            // Top
            tris.Add(CreateTriangleOutward(headTopLeft, headTopRight, tip, BodyCenter, yellowLight));

            // Bottom
            tris.Add(CreateTriangleOutward(headBottomRight, headBottomLeft, tip, BodyCenter, yellowDeep));

            // Left
            tris.Add(CreateTriangleOutward(headBottomLeft, headTopLeft, tip, BodyCenter, yellowDark));

            // Right
            tris.Add(CreateTriangleOutward(headTopRight, headBottomRight, tip, BodyCenter, yellowMid));

            // Back face of head base
            AddQuadOutward(
                tris,
                headBottomLeft,
                headBottomRight,
                headTopRight,
                headTopLeft,
                BodyCenter,
                yellowDark);

            return tris;
        }

        // ----------------------------------------------------
        //  BEVELS / EXTRA DEPTH
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColorAndTexture>? ArrowBevels()
        {
            var tris = new List<ITriangleMeshWithColorAndTexture>();

            float xBack = -shaftLength * 0.5f + tailInset;
            float xFront = shaftLength * 0.5f - 1.5f;

            // Top bevel ridge
            AddLongBevel(
                tris,
                new Vector3 { x = xBack, y = 0, z = shaftHalfHeight },
                new Vector3 { x = xFront, y = 0, z = shaftHalfHeight },
                new Vector3 { x = 0, y = 0, z = 1 },
                new Vector3 { x = 0, y = 1, z = 0 },
                shaftHalfWidth - bevelInset,
                bevelInset,
                yellowLight,
                yellowMid);

            // Bottom bevel ridge
            AddLongBevel(
                tris,
                new Vector3 { x = xBack, y = 0, z = -shaftHalfHeight },
                new Vector3 { x = xFront, y = 0, z = -shaftHalfHeight },
                new Vector3 { x = 0, y = 0, z = -1 },
                new Vector3 { x = 0, y = 1, z = 0 },
                shaftHalfWidth - bevelInset,
                bevelInset,
                yellowDark,
                yellowDeep);

            return tris;
        }

        private static void AddLongBevel(
            List<ITriangleMeshWithColorAndTexture> tris,
            Vector3 start,
            Vector3 end,
            Vector3 outward,
            Vector3 sideAxis,
            float halfSpan,
            float thickness,
            string topColor,
            string sideColor)
        {
            var left1 = Add(Add(start, Scale(sideAxis, -halfSpan)), Scale(outward, thickness));
            var right1 = Add(Add(start, Scale(sideAxis, halfSpan)), Scale(outward, thickness));
            var left2 = Add(Add(end, Scale(sideAxis, -halfSpan)), Scale(outward, thickness));
            var right2 = Add(Add(end, Scale(sideAxis, halfSpan)), Scale(outward, thickness));

            var baseLeft1 = Add(start, Scale(sideAxis, -halfSpan));
            var baseRight1 = Add(start, Scale(sideAxis, halfSpan));
            var baseLeft2 = Add(end, Scale(sideAxis, -halfSpan));
            var baseRight2 = Add(end, Scale(sideAxis, halfSpan));

            AddQuadOutward(tris, left1, right1, right2, left2, BodyCenter, topColor);
            AddQuadOutward(tris, baseLeft1, left1, left2, baseLeft2, BodyCenter, sideColor);
            AddQuadOutward(tris, right1, baseRight1, baseRight2, right2, BodyCenter, sideColor);
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColorAndTexture>? tris, bool visible)
        {
            if (tris == null)
                return;

            obj.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }
    }
}
