using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using GameAiAndControls.Controls.KamikazeDroneControls;
using static Domain._3dSpecificsImplementations;
using static _3dTesting.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public class KamikazeDrone
    {
        private const float ZoomRatio = 1f;

        // ----------------------------------------------------
        //  GEOMETRY PARAMETERS
        // ----------------------------------------------------

        private static float noseBaseX = 12f;
        private static float noseLength = 28f;
        private static float noseRadiusY = 7.0f;
        private static float noseRadiusZ = 7.5f;
        private static int noseSegments = 12;

        private static float bodyFrontX = 12f;
        private static float bodyMidX = -10f;
        private static float bodyBackX = -36f;

        private static float bodyHalfWidthFront = 10f;
        private static float bodyHalfWidthMid = 22f;
        private static float bodyHalfWidthBack = 34f;

        private static float bodyTopFront = 5.8f;
        private static float bodyTopMid = 7.8f;
        private static float bodyTopBack = 5.8f;

        private static float bodyBottomFront = -5.6f;
        private static float bodyBottomMid = -7.2f;
        private static float bodyBottomBack = -5.8f;

        private static float spineHalfWidthFront = 4.5f;
        private static float spineHalfWidthMid = 5.8f;
        private static float spineHalfWidthBack = 4.2f;

        private static float spineLiftFront = 1.9f;
        private static float spineLiftMid = 3.4f;
        private static float spineLiftBack = 1.6f;

        private static float canopyFrontX = 15f;
        private static float canopyMidX = 6f;
        private static float canopyBackX = -4f;

        private static float canopyHalfWidthFront = 2.8f;
        private static float canopyHalfWidthMid = 4.2f;
        private static float canopyHalfWidthBack = 3.2f;

        private static float canopyLiftFront = 0.9f;
        private static float canopyLiftMid = 1.1f;
        private static float canopyLiftBack = 0.8f;

        private static float canopyHeightFront = 2.0f;
        private static float canopyHeightMid = 1.8f;
        private static float canopyHeightBack = 1.0f;

        private static float engineFrontX = -36f;
        private static float engineBackX = -47f;
        private static float engineHalfWidth = 8.5f;
        private static float engineHalfHeight = 6.0f;

        private static float finBaseX = -30f;
        private static float finTipX = -36f;
        private static float finHeight = 10f;
        private static float finThickness = 2f;
        private static float finInset = 6f;
        private const float CrashboxSize = 6f;

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string bodyColorLight = "B8BDC6";
        private static string bodyColorMid = "8B919B";
        private static string bodyColorDark = "565C66";
        private static string undersideColor = "3E434B";
        private static string cockpitColor = "1C1F26";
        private static string cockpitColorSoft = "2B2F38";
        private static string engineColor = "FF9D2E";
        private static string engineColorDark = "B85E16";
        private static string finColor = "707781";

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        public static _3dObject CreateKamikazeDrone(ISurface parentSurface)
        {
            var nose = KamikazeRoundedNose();
            var noseTransition = KamikazeNoseTransition();
            var bodyTop = KamikazeBodyTop();
            var bodyBottom = KamikazeBodyBottom();
            var bodySides = KamikazeBodySides();
            var spine = KamikazeTopSpine();
            var canopy = KamikazeCanopy();
            var engine = KamikazeEngineBlock();
            var fins = KamikazeRearFins();

            var crashBoxes = KamikazeCrashBoxes();
            var guide = ParticlesDirectionGuide();
            var startGuide = ParticlesStartGuide();

            var drone = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "KamikazeDrone"
            };

            AddPart(drone, "KamikazeRoundedNose", nose, true);
            AddPart(drone, "KamikazeNoseTransition", noseTransition, true);
            AddPart(drone, "KamikazeBodyTop", bodyTop, true);
            AddPart(drone, "KamikazeBodyBottom", bodyBottom, true);
            AddPart(drone, "KamikazeBodySides", bodySides, true);
            AddPart(drone, "KamikazeTopSpine", spine, true);
            AddPart(drone, "KamikazeCanopy", canopy, true);
            AddPart(drone, "KamikazeEngineBlock", engine, true);
            AddPart(drone, "KamikazeRearFins", fins, true);

            AddPart(drone, "KamikazeParticlesGuide", guide, false);
            AddPart(drone, "KamikazeParticlesStartGuide", startGuide, false);

            drone.Movement = new KamikazeDroneControls();
            drone.Particles = new ParticlesAI();
            drone.Rotation = new Vector3 { x = 0, y = 0, z = 0 };

            if (crashBoxes != null)
                drone.CrashBoxes = crashBoxes;

            drone.ParentSurface = parentSurface;
            drone.HasShadow = true;

            _3dObjectHelpers.ApplyScaleToObject(drone, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(drone, useFlatQuad: true);

            return drone;
        }

        // ----------------------------------------------------
        //  SHARED FRONT RING
        // ----------------------------------------------------

        private static List<Vector3> GetFrontRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = bodyFrontX, y =  0f,                     z =  bodyTopFront + 0.9f },                     // 0 top
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.85f },                 // 1 top-right
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront,    z =  0f },                                     // 2 right
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront*0.55f, z =  bodyBottomFront * 0.85f },              // 3 bottom-right
                new Vector3 { x = bodyFrontX, y =  0f,                     z =  bodyBottomFront - 0.9f },                 // 4 bottom
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront*0.55f, z =  bodyBottomFront * 0.85f },              // 5 bottom-left
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront,    z =  0f },                                     // 6 left
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.85f }                  // 7 top-left
            };
        }

        // ----------------------------------------------------
        //  NOSE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? KamikazeRoundedNose()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float tipX = noseBaseX + noseLength;
            float x1 = noseBaseX + noseLength * 0.80f;
            float x2 = noseBaseX + noseLength * 0.52f;
            float x3 = noseBaseX + noseLength * 0.22f;

            var tip = new Vector3 { x = tipX, y = 0, z = 0 };

            var ring1 = GenerateEllipseRing(noseSegments, x1, noseRadiusY * 0.28f, noseRadiusZ * 0.28f);
            var ring2 = GenerateEllipseRing(noseSegments, x2, noseRadiusY * 0.55f, noseRadiusZ * 0.55f);
            var ring3 = GenerateEllipseRing(noseSegments, x3, noseRadiusY * 0.80f, noseRadiusZ * 0.80f);
            var baseRing = GenerateEllipseRing(noseSegments, noseBaseX, noseRadiusY, noseRadiusZ);

            for (int i = 0; i < noseSegments; i++)
            {
                int next = (i + 1) % noseSegments;

                string c1 = (i % 2 == 0) ? bodyColorLight : bodyColorMid;
                string c2 = (i % 2 == 0) ? bodyColorMid : bodyColorDark;

                tris.Add(CreateTriangleOutward(tip, ring1[next], ring1[i], BodyCenter, c1));
                AddQuadOutward(tris, ring1[i], ring1[next], ring2[next], ring2[i], BodyCenter, c1);
                AddQuadOutward(tris, ring2[i], ring2[next], ring3[next], ring3[i], BodyCenter, c1);
                AddQuadOutward(tris, ring3[i], ring3[next], baseRing[next], baseRing[i], BodyCenter, c2);
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? KamikazeNoseTransition()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Use 8 segments here so they map 1:1 with front ring
            var baseRing = GenerateEllipseRing(8, noseBaseX, noseRadiusY, noseRadiusZ);
            var frontRing = GetFrontRing();

            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;

                float avgZ = (baseRing[i].z + baseRing[next].z) * 0.5f;
                float avgY = (baseRing[i].y + baseRing[next].y) * 0.5f;

                string color;
                if (avgZ > Math.Abs(avgY) * 0.4f)
                    color = bodyColorLight;
                else if (avgZ < -Math.Abs(avgY) * 0.4f)
                    color = undersideColor;
                else
                    color = bodyColorDark;

                AddQuadOutward(tris, baseRing[i], baseRing[next], frontRing[next], frontRing[i], BodyCenter, color);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  BODY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? KamikazeBodyTop()
        {
            var tris = new List<ITriangleMeshWithColor>();
            var ring = GetFrontRing();

            var top = ring[0];
            var topRight = ring[1];
            var topLeft = ring[7];

            var midLeft = new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid, z = bodyTopMid };
            var midRight = new Vector3 { x = bodyMidX, y = bodyHalfWidthMid, z = bodyTopMid };
            var backLeft = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyTopBack };
            var backRight = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyTopBack };

            var centerFront = new Vector3 { x = bodyFrontX + 1f, y = 0, z = bodyTopFront + 1.1f };
            var centerMid = new Vector3 { x = bodyMidX + 1f, y = 0, z = bodyTopMid + 1.0f };
            var centerBack = new Vector3 { x = bodyBackX + 5f, y = 0, z = bodyTopBack + 0.5f };

            tris.Add(CreateTriangleOutward(topLeft, top, midLeft, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(top, centerFront, midLeft, BodyCenter, bodyColorLight));
            tris.Add(CreateTriangleOutward(centerFront, centerMid, midLeft, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(top, topRight, midRight, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(top, midRight, centerFront, BodyCenter, bodyColorLight));
            tris.Add(CreateTriangleOutward(centerFront, midRight, centerMid, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(midLeft, centerMid, backLeft, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerMid, centerBack, backLeft, BodyCenter, bodyColorDark));

            tris.Add(CreateTriangleOutward(centerMid, midRight, backRight, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerMid, backRight, centerBack, BodyCenter, bodyColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? KamikazeBodyBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();
            var ring = GetFrontRing();

            var bottom = ring[4];
            var bottomRight = ring[3];
            var bottomLeft = ring[5];

            var midLeft = new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid, z = bodyBottomMid };
            var midRight = new Vector3 { x = bodyMidX, y = bodyHalfWidthMid, z = bodyBottomMid };
            var backLeft = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyBottomBack };
            var backRight = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyBottomBack };

            var centerFront = new Vector3 { x = bodyFrontX + 1f, y = 0, z = bodyBottomFront - 1.0f };
            var centerMid = new Vector3 { x = bodyMidX + 1f, y = 0, z = bodyBottomMid - 1.0f };
            var centerBack = new Vector3 { x = bodyBackX + 6f, y = 0, z = bodyBottomBack - 0.5f };

            tris.Add(CreateTriangleOutward(bottomLeft, midLeft, bottom, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(bottom, midLeft, centerFront, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, midLeft, centerMid, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(bottom, midRight, bottomRight, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(bottom, centerFront, midRight, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, centerMid, midRight, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(midLeft, backLeft, centerMid, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerMid, backLeft, centerBack, BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(centerMid, backRight, midRight, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerMid, centerBack, backRight, BodyCenter, undersideColor));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? KamikazeBodySides()
        {
            var tris = new List<ITriangleMeshWithColor>();
            var ring = GetFrontRing();

            // Left side uses shared front points
            {
                var frontTop = ring[7];
                var frontMid = ring[6];
                var frontBottom = ring[5];

                var midTop = new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid, z = bodyTopMid };
                var midBottom = new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid, z = bodyBottomMid };

                var backTop = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyTopBack };
                var backBottom = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyBottomBack };

                tris.Add(CreateTriangleOutward(frontTop, frontMid, midTop, BodyCenter, bodyColorDark));
                tris.Add(CreateTriangleOutward(frontMid, midBottom, midTop, BodyCenter, bodyColorDark));
                tris.Add(CreateTriangleOutward(frontMid, frontBottom, midBottom, BodyCenter, bodyColorDark));

                AddQuadOutward(tris, midTop, midBottom, backBottom, backTop, BodyCenter, bodyColorDark);
            }

            // Right side uses shared front points
            {
                var frontTop = ring[1];
                var frontMid = ring[2];
                var frontBottom = ring[3];

                var midTop = new Vector3 { x = bodyMidX, y = bodyHalfWidthMid, z = bodyTopMid };
                var midBottom = new Vector3 { x = bodyMidX, y = bodyHalfWidthMid, z = bodyBottomMid };

                var backTop = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyTopBack };
                var backBottom = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyBottomBack };

                tris.Add(CreateTriangleOutward(frontTop, midTop, frontMid, BodyCenter, bodyColorDark));
                tris.Add(CreateTriangleOutward(frontMid, midTop, midBottom, BodyCenter, bodyColorDark));
                tris.Add(CreateTriangleOutward(frontMid, midBottom, frontBottom, BodyCenter, bodyColorDark));

                AddQuadOutward(tris, midTop, backTop, backBottom, midBottom, BodyCenter, bodyColorDark);
            }

            // Rear face
            {
                var topLeft = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyTopBack };
                var topRight = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyTopBack };
                var bottomLeft = new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack, z = bodyBottomBack };
                var bottomRight = new Vector3 { x = bodyBackX, y = bodyHalfWidthBack, z = bodyBottomBack };

                AddQuadOutward(tris, topLeft, topRight, bottomRight, bottomLeft, BodyCenter, bodyColorDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  SPINE + CANOPY
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? KamikazeTopSpine()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var lf = new Vector3 { x = bodyFrontX + 2f, y = -spineHalfWidthFront, z = bodyTopFront + 0.25f };
            var rf = new Vector3 { x = bodyFrontX + 2f, y = spineHalfWidthFront, z = bodyTopFront + 0.25f };

            var lm = new Vector3 { x = bodyMidX + 2f, y = -spineHalfWidthMid, z = bodyTopMid + 0.25f };
            var rm = new Vector3 { x = bodyMidX + 2f, y = spineHalfWidthMid, z = bodyTopMid + 0.25f };

            var lb = new Vector3 { x = bodyBackX + 6f, y = -spineHalfWidthBack, z = bodyTopBack + 0.2f };
            var rb = new Vector3 { x = bodyBackX + 6f, y = spineHalfWidthBack, z = bodyTopBack + 0.2f };

            var tf = new Vector3 { x = bodyFrontX + 3f, y = 0, z = bodyTopFront + spineLiftFront };
            var tm = new Vector3 { x = bodyMidX + 2f, y = 0, z = bodyTopMid + spineLiftMid };
            var tb = new Vector3 { x = bodyBackX + 7f, y = 0, z = bodyTopBack + spineLiftBack };

            tris.Add(CreateTriangleOutward(lf, tf, lm, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(tf, tm, lm, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(tf, rf, rm, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(tf, rm, tm, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(lm, tm, lb, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(tm, tb, lb, BodyCenter, bodyColorDark));

            tris.Add(CreateTriangleOutward(tm, rm, rb, BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(tm, rb, tb, BodyCenter, bodyColorDark));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? KamikazeCanopy()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float baseFrontZ = bodyTopFront + spineLiftFront + canopyLiftFront;
            float baseMidZ = bodyTopMid + spineLiftMid + canopyLiftMid;
            float baseBackZ = bodyTopMid + spineLiftMid * 0.55f + canopyLiftBack;

            var lf = new Vector3 { x = canopyFrontX, y = -canopyHalfWidthFront, z = baseFrontZ };
            var rf = new Vector3 { x = canopyFrontX, y = canopyHalfWidthFront, z = baseFrontZ };

            var lm = new Vector3 { x = canopyMidX, y = -canopyHalfWidthMid, z = baseMidZ };
            var rm = new Vector3 { x = canopyMidX, y = canopyHalfWidthMid, z = baseMidZ };

            var lb = new Vector3 { x = canopyBackX, y = -canopyHalfWidthBack, z = baseBackZ };
            var rb = new Vector3 { x = canopyBackX, y = canopyHalfWidthBack, z = baseBackZ };

            var tf = new Vector3 { x = canopyFrontX + 0.5f, y = 0, z = baseFrontZ + canopyHeightFront };
            var tm = new Vector3 { x = canopyMidX, y = 0, z = baseMidZ + canopyHeightMid };
            var tb = new Vector3 { x = canopyBackX, y = 0, z = baseBackZ + canopyHeightBack };

            tris.Add(CreateTriangleOutward(lf, tf, lm, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(tf, tm, lm, BodyCenter, cockpitColor));

            tris.Add(CreateTriangleOutward(tf, rf, rm, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(tf, rm, tm, BodyCenter, cockpitColor));

            tris.Add(CreateTriangleOutward(lm, tm, lb, BodyCenter, cockpitColorSoft));
            tris.Add(CreateTriangleOutward(tm, tb, lb, BodyCenter, cockpitColorSoft));

            tris.Add(CreateTriangleOutward(tm, rm, rb, BodyCenter, cockpitColorSoft));
            tris.Add(CreateTriangleOutward(tm, rb, tb, BodyCenter, cockpitColorSoft));

            tris.Add(CreateTriangleOutward(lf, rf, tf, BodyCenter, cockpitColor));
            tris.Add(CreateTriangleOutward(lm, tm, rm, BodyCenter, cockpitColorSoft));
            tris.Add(CreateTriangleOutward(lb, tb, rb, BodyCenter, cockpitColorSoft));

            return tris;
        }

        // ----------------------------------------------------
        //  ENGINE + FINS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? KamikazeEngineBlock()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ftl = new Vector3 { x = engineFrontX, y = -engineHalfWidth, z = engineHalfHeight };
            var ftr = new Vector3 { x = engineFrontX, y = engineHalfWidth, z = engineHalfHeight };
            var fbl = new Vector3 { x = engineFrontX, y = -engineHalfWidth, z = -engineHalfHeight };
            var fbr = new Vector3 { x = engineFrontX, y = engineHalfWidth, z = -engineHalfHeight };

            var btl = new Vector3 { x = engineBackX, y = -engineHalfWidth, z = engineHalfHeight };
            var btr = new Vector3 { x = engineBackX, y = engineHalfWidth, z = engineHalfHeight };
            var bbl = new Vector3 { x = engineBackX, y = -engineHalfWidth, z = -engineHalfHeight };
            var bbr = new Vector3 { x = engineBackX, y = engineHalfWidth, z = -engineHalfHeight };

            AddQuadOutward(tris, ftl, ftr, btr, btl, BodyCenter, engineColorDark);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, BodyCenter, bodyColorDark);
            AddQuadOutward(tris, ftr, fbr, bbr, btr, BodyCenter, engineColorDark);
            AddQuadOutward(tris, fbl, ftl, btl, bbl, BodyCenter, engineColorDark);
            AddQuadOutward(tris, btl, btr, bbr, bbl, BodyCenter, engineColor);

            return tris;
        }

        public static List<ITriangleMeshWithColor>? KamikazeRearFins()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left
            {
                float yOuter = -bodyHalfWidthBack + finInset;
                float yInner = yOuter + finThickness;

                var a = new Vector3 { x = finBaseX, y = yOuter, z = bodyTopBack + 0.2f };
                var b = new Vector3 { x = finTipX, y = yOuter, z = bodyTopBack + finHeight };
                var c = new Vector3 { x = finTipX, y = yOuter, z = bodyTopBack + 1.0f };

                var a2 = new Vector3 { x = finBaseX, y = yInner, z = bodyTopBack + 0.2f };
                var b2 = new Vector3 { x = finTipX, y = yInner, z = bodyTopBack + finHeight };
                var c2 = new Vector3 { x = finTipX, y = yInner, z = bodyTopBack + 1.0f };

                tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, finColor));
                tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, bodyColorDark));
                AddQuadOutward(tris, a, a2, b2, b, BodyCenter, finColor);
                AddQuadOutward(tris, a, c, c2, a2, BodyCenter, bodyColorDark);
                AddQuadOutward(tris, c, b, b2, c2, BodyCenter, bodyColorDark);
            }

            // Right
            {
                float yOuter = bodyHalfWidthBack - finInset;
                float yInner = yOuter - finThickness;

                var a = new Vector3 { x = finBaseX, y = yOuter, z = bodyTopBack + 0.2f };
                var b = new Vector3 { x = finTipX, y = yOuter, z = bodyTopBack + finHeight };
                var c = new Vector3 { x = finTipX, y = yOuter, z = bodyTopBack + 1.0f };

                var a2 = new Vector3 { x = finBaseX, y = yInner, z = bodyTopBack + 0.2f };
                var b2 = new Vector3 { x = finTipX, y = yInner, z = bodyTopBack + finHeight };
                var c2 = new Vector3 { x = finTipX, y = yInner, z = bodyTopBack + 1.0f };

                tris.Add(CreateTriangleOutward(a, c, b, BodyCenter, finColor));
                tris.Add(CreateTriangleOutward(a2, b2, c2, BodyCenter, bodyColorDark));
                AddQuadOutward(tris, a, b, b2, a2, BodyCenter, finColor);
                AddQuadOutward(tris, a, a2, c2, c, BodyCenter, bodyColorDark);
                AddQuadOutward(tris, c, c2, b2, b, BodyCenter, bodyColorDark);
            }

            return tris;
        }

        // ----------------------------------------------------
        //  GUIDES / COLLISION
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ParticlesDirectionGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = engineBackX - 2f, y =  6f, z =  2f },
                    new Vector3 { x = engineBackX - 2f, y = -6f, z =  2f },
                    new Vector3 { x = engineBackX - 14f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<ITriangleMeshWithColor>? ParticlesStartGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = engineFrontX - 2f, y =  5f, z =  2f },
                    new Vector3 { x = engineFrontX - 2f, y = -5f, z =  2f },
                    new Vector3 { x = engineBackX + 2f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<List<IVector3>>? KamikazeCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var noseBounds = ScaleCrashBoxBounds(
                new Vector3 { x = noseBaseX, y = -noseRadiusY, z = -noseRadiusZ },
                new Vector3 { x = noseBaseX + noseLength, y = noseRadiusY, z = noseRadiusZ });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(noseBounds.min, noseBounds.max));

            var bodyBounds = ScaleCrashBoxBounds(
                new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid, z = bodyBottomMid - 1.0f },
                new Vector3 { x = bodyFrontX + 3f, y = bodyHalfWidthMid, z = bodyTopMid + spineLiftMid + canopyLiftMid + canopyHeightMid });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bodyBounds.min, bodyBounds.max));

            var engineBounds = ScaleCrashBoxBounds(
                new Vector3 { x = engineBackX, y = -bodyHalfWidthBack, z = bodyBottomBack - 1.0f },
                new Vector3 { x = bodyBackX + 4f, y = bodyHalfWidthBack, z = bodyTopBack + finHeight });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(engineBounds.min, engineBounds.max));

            return boxes;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

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

        private static (Vector3 min, Vector3 max) ScaleCrashBoxBounds(Vector3 min, Vector3 max)
        {
            var center = new Vector3
            {
                x = (min.x + max.x) * 0.5f,
                y = (min.y + max.y) * 0.5f,
                z = (min.z + max.z) * 0.5f
            };

            var halfSize = new Vector3
            {
                x = (max.x - min.x) * 0.5f * CrashboxSize,
                y = (max.y - min.y) * 0.5f * CrashboxSize,
                z = (max.z - min.z) * 0.5f * CrashboxSize
            };

            return (
                new Vector3
                {
                    x = center.x - halfSize.x,
                    y = center.y - halfSize.y,
                    z = center.z - halfSize.z
                },
                new Vector3
                {
                    x = center.x + halfSize.x,
                    y = center.y + halfSize.y,
                    z = center.z + halfSize.z
                }
            );
        }

        private static List<Vector3> GenerateEllipseRing(int segments, float x, float radiusY, float radiusZ)
        {
            var points = new List<Vector3>(segments);
            float step = (float)(2 * Math.PI / segments);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * step;
                points.Add(new Vector3
                {
                    x = x,
                    y = radiusY * (float)Math.Cos(angle),
                    z = radiusZ * (float)Math.Sin(angle)
                });
            }

            return points;
        }
    }
}