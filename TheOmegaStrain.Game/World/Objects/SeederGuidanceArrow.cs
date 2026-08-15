using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;
using static _3dRotations.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public class SeederGuidanceArrow
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

        private static string cyanTop = "5FFBFF";
        private static string cyanLight = "3BE7F0";
        private static string cyanMid = "19C9D8";
        private static string cyanDark = "0D7F8D";
        private static string cyanDeep = "074B57";

        // ----------------------------------------------------
        //  OBJECT CREATION
        // ----------------------------------------------------

        public static _3dObject CreateSeederGuidanceArrow(ISurface parentSurface)
        {
            var body = ArrowBody();
            var head = ArrowHead();
            var bevels = ArrowBevels();

            var arrow = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                HasShadow = false
            };

            AddPart(arrow, "SeederGuidanceArrowBody", body, true);
            AddPart(arrow, "SeederGuidanceArrowHead", head, true);
            AddPart(arrow, "SeederGuidanceArrowBevels", bevels, true);

            arrow.Movement = new SeederGuidanceArrowControl();
            return arrow;
        }

        // ----------------------------------------------------
        //  SHAFT / BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ArrowBody()
        {
            var tris = new List<ITriangleMeshWithColor>();

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
            AddQuadOutward(tris, v1, v2, v3, v4, BodyCenter, cyanTop);

            // Bottom
            AddQuadOutward(tris, v8, v7, v6, v5, BodyCenter, cyanDeep);

            // Left
            AddQuadOutward(tris, v5, v1, v4, v8, BodyCenter, cyanDark);

            // Right
            AddQuadOutward(tris, v2, v6, v7, v3, BodyCenter, cyanMid);

            // Back
            AddQuadOutward(tris, v5, v6, v2, v1, BodyCenter, cyanDark);

            return tris;
        }

        // ----------------------------------------------------
        //  ARROW HEAD
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ArrowHead()
        {
            var tris = new List<ITriangleMeshWithColor>();

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
                cyanLight);

            // Bottom bridge
            AddQuadOutward(
                tris,
                headBottomLeft,
                headBottomRight,
                shaftBottomRight,
                shaftBottomLeft,
                BodyCenter,
                cyanDeep);

            // Left bridge
            AddQuadOutward(
                tris,
                shaftBottomLeft,
                shaftTopLeft,
                headTopLeft,
                headBottomLeft,
                BodyCenter,
                cyanDark);

            // Right bridge
            AddQuadOutward(
                tris,
                shaftTopRight,
                shaftBottomRight,
                headBottomRight,
                headTopRight,
                BodyCenter,
                cyanMid);

            // ----------------------------------------------------
            // Arrow head faces toward the tip
            // ----------------------------------------------------

            // Top
            tris.Add(CreateTriangleOutward(headTopLeft, headTopRight, tip, BodyCenter, cyanLight));

            // Bottom
            tris.Add(CreateTriangleOutward(headBottomRight, headBottomLeft, tip, BodyCenter, cyanDeep));

            // Left
            tris.Add(CreateTriangleOutward(headBottomLeft, headTopLeft, tip, BodyCenter, cyanDark));

            // Right
            tris.Add(CreateTriangleOutward(headTopRight, headBottomRight, tip, BodyCenter, cyanMid));

            // Back face of head base
            AddQuadOutward(
                tris,
                headBottomLeft,
                headBottomRight,
                headTopRight,
                headTopLeft,
                BodyCenter,
                cyanDark);

            return tris;
        }

        // ----------------------------------------------------
        //  BEVELS / EXTRA DEPTH
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ArrowBevels()
        {
            var tris = new List<ITriangleMeshWithColor>();

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
                cyanLight,
                cyanMid);

            // Bottom bevel ridge
            AddLongBevel(
                tris,
                new Vector3 { x = xBack, y = 0, z = -shaftHalfHeight },
                new Vector3 { x = xFront, y = 0, z = -shaftHalfHeight },
                new Vector3 { x = 0, y = 0, z = -1 },
                new Vector3 { x = 0, y = 1, z = 0 },
                shaftHalfWidth - bevelInset,
                bevelInset,
                cyanDark,
                cyanDeep);

            return tris;
        }

        private static void AddLongBevel(
            List<ITriangleMeshWithColor> tris,
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

        private static void AddPart(_3dObject obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
        {
            if (tris == null)
                return;

            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = name,
                Triangles = tris,
                IsVisible = visible
            });
        }

            }
        }