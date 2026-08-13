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
    public class SpaceSwan
    {
        private const float ZoomRatio = 1.9f;
        private const float CrashboxSize = 1.5f;

        // ----------------------------------------------------
        //  MAIN BODY
        // ----------------------------------------------------

        private static float noseTipX = 34f;
        private static float noseFrontX = 22f;
        private static float noseBaseX = 12f;

        private static float bodyFrontX = 12f;
        private static float bodyMidX = -4f;
        private static float bodyBackX = -26f;

        private static float bodyHalfWidthFront = 7.5f;
        private static float bodyHalfWidthMid = 10.5f;
        private static float bodyHalfWidthBack = 8.5f;

        private static float bodyTopFront = 4.8f;
        private static float bodyTopMid = 7.8f;
        private static float bodyTopBack = 5.6f;

        private static float bodyBottomFront = -3.8f;
        private static float bodyBottomMid = -5.0f;
        private static float bodyBottomBack = -4.1f;

        private static float keelDepthMid = -7.0f;

        // ----------------------------------------------------
        //  CANOPY / SPINE
        // ----------------------------------------------------

        private static float canopyFrontX = 16f;
        private static float canopyMidX = 4f;
        private static float canopyBackX = -8f;

        private static float canopyHalfWidthFront = 2.3f;
        private static float canopyHalfWidthMid = 3.5f;
        private static float canopyHalfWidthBack = 2.6f;

        private static float canopyBaseLiftFront = 1.4f;
        private static float canopyBaseLiftMid = 2.0f;
        private static float canopyBaseLiftBack = 1.2f;

        private static float canopyHeightFront = 1.8f;
        private static float canopyHeightMid = 2.6f;
        private static float canopyHeightBack = 1.4f;

        // ----------------------------------------------------
        //  CORE
        // ----------------------------------------------------

        private static float coreFrontX = 1.5f;
        private static float coreBackX = -3.5f;
        private static float coreHalfWidth = 1.8f;
        private static float coreHalfHeight = 1.6f;
        private static float coreCenterZ = -0.3f;

        // ----------------------------------------------------
        //  WINGS
        //  3 segments per side for elegant bird-like flap
        // ----------------------------------------------------

        private static float wingRootX = 8f;
        private static float wingSeg1TipX = -4f;
        private static float wingSeg2TipX = -16f;
        private static float wingSeg3TipX = -28f;

        private static float wingRootY = 9f;
        private static float wingSeg1TipY = 20f;
        private static float wingSeg2TipY = 31f;
        private static float wingSeg3TipY = 39f;

        private static float wingRootZ = 2.0f;
        private static float wingSeg1TipZ = 1.0f;
        private static float wingSeg2TipZ = 0.2f;
        private static float wingSeg3TipZ = -0.3f;

        private static float wingChordRoot = 8.0f;
        private static float wingChordSeg1 = 7.0f;
        private static float wingChordSeg2 = 5.8f;
        private static float wingChordSeg3 = 4.2f;

        private static float wingThicknessRoot = 0.95f;
        private static float wingThicknessSeg1 = 0.80f;
        private static float wingThicknessSeg2 = 0.62f;
        private static float wingThicknessSeg3 = 0.42f;

        // ----------------------------------------------------
        //  TAIL
        // ----------------------------------------------------

        private static float tailRootX = -24f;
        private static float tailMidX = -33f;
        private static float tailTipX = -40f;

        private static float tailRootY = 4.8f;
        private static float tailTipY = 10.8f;

        private static float tailRootZ = 2.2f;
        private static float tailTipZ = 4.8f;
        private static float tailBottomZ = -1.2f;

        private static float verticalFinFrontX = -26f;
        private static float verticalFinTipX = -35f;
        private static float verticalFinHeight = 8f;
        private static float verticalFinThickness = 1.4f;

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string bodyColorLight = "C0CCE0";
        private static string bodyColorMid = "8A9AB8";
        private static string bodyColorDark = "4E5A72";
        private static string undersideColor = "2A3040";
        private static string canopyColor = "1A3828";
        private static string canopySoft = "264A38";
        private static string wingColorLight = "D0DBEF";
        private static string wingColorMid = "A0B0CC";
        private static string wingColorDark = "5A6A88";
        private static string tailColor = "8A8050";
        private static string coreColor = "4FF7FF";
        private static string coreColorSoft = "1FA9C6";

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        public static _3dObject CreateSpaceSwan(ISurface parentSurface)
        {
            var nose = SwanBeakNose();
            var noseTransition = SwanNoseTransition();
            var bodyTop = SwanBodyTop();
            var bodyBottom = SwanBodyBottom();
            var bodySides = SwanBodySides();
            var canopy = SwanCanopy();
            var core = SwanCore();

            var leftWingInner = SwanWingSegment(isLeft: true, segmentIndex: 0);
            var leftWingMid = SwanWingSegment(isLeft: true, segmentIndex: 1);
            var leftWingOuter = SwanWingSegment(isLeft: true, segmentIndex: 2);

            var rightWingInner = SwanWingSegment(isLeft: false, segmentIndex: 0);
            var rightWingMid = SwanWingSegment(isLeft: false, segmentIndex: 1);
            var rightWingOuter = SwanWingSegment(isLeft: false, segmentIndex: 2);

            var tailPlanes = SwanTailPlanes();
            var verticalFin = SwanVerticalFin();

            var crashBoxes = SwanCrashBoxes();
            var guide = ParticlesDirectionGuide();
            var startGuide = ParticlesStartGuide();

            var swan = new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SpaceSwan"
            };

            AddPart(swan, "SpaceSwanBeakNose", nose, true);
            AddPart(swan, "SpaceSwanNoseTransition", noseTransition, true);
            AddPart(swan, "SpaceSwanBodyTop", bodyTop, true);
            AddPart(swan, "SpaceSwanBodyBottom", bodyBottom, true);
            AddPart(swan, "SpaceSwanBodySides", bodySides, true);
            AddPart(swan, "SpaceSwanCanopy", canopy, true);
            AddPart(swan, "SpaceSwanCore", core, true);

            AddPart(swan, "SpaceSwanLeftWingInner", leftWingInner, true);
            AddPart(swan, "SpaceSwanLeftWingMid", leftWingMid, true);
            AddPart(swan, "SpaceSwanLeftWingOuter", leftWingOuter, true);

            AddPart(swan, "SpaceSwanRightWingInner", rightWingInner, true);
            AddPart(swan, "SpaceSwanRightWingMid", rightWingMid, true);
            AddPart(swan, "SpaceSwanRightWingOuter", rightWingOuter, true);

            AddPart(swan, "SpaceSwanTailPlanes", tailPlanes, true);
            AddPart(swan, "SpaceSwanVerticalFin", verticalFin, true);

            AddPart(swan, "SpaceSwanParticlesGuide", guide, false);
            AddPart(swan, "SpaceSwanParticlesStartGuide", startGuide, false);

            swan.Particles = new ParticlesAI();
            swan.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            swan.ParentSurface = parentSurface;
            swan.HasShadow = true;

            if (crashBoxes != null)
                swan.CrashBoxes = crashBoxes;

            _3dObjectHelpers.ApplyScaleToObject(swan, ZoomRatio);

            _3dObjectHelpers.AddSimplifiedShadowPart(swan, useFlatQuad: true);

            return swan;
        }

        // ----------------------------------------------------
        //  SHARED RINGS
        // ----------------------------------------------------

        private static List<Vector3> GetFrontRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = bodyFrontX, y =  0f,                     z =  bodyTopFront + 0.5f },                  // 0 top
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.78f },              // 1 top-right
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront,    z =  0f },                                  // 2 right
                new Vector3 { x = bodyFrontX, y =  bodyHalfWidthFront*0.58f, z =  bodyBottomFront * 0.75f },           // 3 bottom-right
                new Vector3 { x = bodyFrontX, y =  0f,                     z =  bodyBottomFront - 0.35f },             // 4 bottom
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront*0.58f, z =  bodyBottomFront * 0.75f },           // 5 bottom-left
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront,    z =  0f },                                  // 6 left
                new Vector3 { x = bodyFrontX, y = -bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.78f }               // 7 top-left
            };
        }

        private static List<Vector3> GetMidRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = bodyMidX, y =  0f,                   z =  bodyTopMid + 0.5f },
                new Vector3 { x = bodyMidX, y =  bodyHalfWidthMid*0.6f, z =  bodyTopMid * 0.83f },
                new Vector3 { x = bodyMidX, y =  bodyHalfWidthMid,    z =  0f },
                new Vector3 { x = bodyMidX, y =  bodyHalfWidthMid*0.6f, z =  bodyBottomMid * 0.80f },
                new Vector3 { x = bodyMidX, y =  0f,                   z =  bodyBottomMid - 0.45f },
                new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid*0.6f, z =  bodyBottomMid * 0.80f },
                new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid,    z =  0f },
                new Vector3 { x = bodyMidX, y = -bodyHalfWidthMid*0.6f, z =  bodyTopMid * 0.83f }
            };
        }

        private static List<Vector3> GetBackRing()
        {
            return new List<Vector3>
            {
                new Vector3 { x = bodyBackX, y =  0f,                    z =  bodyTopBack + 0.35f },
                new Vector3 { x = bodyBackX, y =  bodyHalfWidthBack*0.6f, z =  bodyTopBack * 0.82f },
                new Vector3 { x = bodyBackX, y =  bodyHalfWidthBack,     z =  0f },
                new Vector3 { x = bodyBackX, y =  bodyHalfWidthBack*0.6f, z =  bodyBottomBack * 0.84f },
                new Vector3 { x = bodyBackX, y =  0f,                    z =  bodyBottomBack - 0.30f },
                new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack*0.6f, z =  bodyBottomBack * 0.84f },
                new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack,     z =  0f },
                new Vector3 { x = bodyBackX, y = -bodyHalfWidthBack*0.6f, z =  bodyTopBack * 0.82f }
            };
        }

        // ----------------------------------------------------
        //  NOSE / BEAK
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? SwanBeakNose()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var tipTop = new Vector3 { x = noseTipX, y = 0f, z = 1.2f };
            var tipBottom = new Vector3 { x = noseTipX - 1.5f, y = 0f, z = -0.8f };

            var topFront = new Vector3 { x = noseFrontX, y = 0f, z = 3.2f };
            var rightUpperFront = new Vector3 { x = noseFrontX, y = 3.4f, z = 1.8f };
            var rightMidFront = new Vector3 { x = noseFrontX, y = 4.8f, z = 0f };
            var rightLowerFront = new Vector3 { x = noseFrontX, y = 3.0f, z = -1.7f };
            var bottomFront = new Vector3 { x = noseFrontX, y = 0f, z = -2.7f };
            var leftLowerFront = new Vector3 { x = noseFrontX, y = -3.0f, z = -1.7f };
            var leftMidFront = new Vector3 { x = noseFrontX, y = -4.8f, z = 0f };
            var leftUpperFront = new Vector3 { x = noseFrontX, y = -3.4f, z = 1.8f };

            var topBase = new Vector3 { x = noseBaseX, y = 0f, z = bodyTopFront + 0.5f };
            var rightUpperBase = new Vector3 { x = noseBaseX, y = bodyHalfWidthFront * 0.55f, z = bodyTopFront * 0.78f };
            var rightMidBase = new Vector3 { x = noseBaseX, y = bodyHalfWidthFront, z = 0f };
            var rightLowerBase = new Vector3 { x = noseBaseX, y = bodyHalfWidthFront * 0.55f, z = bodyBottomFront * 0.72f };
            var bottomBase = new Vector3 { x = noseBaseX, y = 0f, z = bodyBottomFront - 0.35f };
            var leftLowerBase = new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront * 0.55f, z = bodyBottomFront * 0.72f };
            var leftMidBase = new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront, z = 0f };
            var leftUpperBase = new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront * 0.55f, z = bodyTopFront * 0.78f };

            var frontRing = new List<Vector3>
            {
                topFront,
                rightUpperFront,
                rightMidFront,
                rightLowerFront,
                bottomFront,
                leftLowerFront,
                leftMidFront,
                leftUpperFront
            };

            var baseRing = new List<Vector3>
            {
                topBase,
                rightUpperBase,
                rightMidBase,
                rightLowerBase,
                bottomBase,
                leftLowerBase,
                leftMidBase,
                leftUpperBase
            };

            for (int i = 0; i < frontRing.Count; i++)
            {
                int next = (i + 1) % frontRing.Count;
                string c1 = i < 2 || i == 7 ? bodyColorLight : i >= 3 && i <= 5 ? undersideColor : bodyColorMid;
                string c2 = i == 2 || i == 6 ? bodyColorDark : c1;

                tris.Add(CreateTriangleOutward(tipTop, frontRing[next], frontRing[i], BodyCenter, c1));
                tris.Add(CreateTriangleOutward(tipBottom, frontRing[i], frontRing[next], BodyCenter, c2));
                AddQuadOutward(tris, frontRing[i], frontRing[next], baseRing[next], baseRing[i], BodyCenter, c1);
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? SwanNoseTransition()
        {
            var tris = new List<ITriangleMeshWithColor>();
            var baseRing = new List<Vector3>
            {
                new Vector3 { x = noseBaseX, y =  0f,                   z =  bodyTopFront + 0.5f },
                new Vector3 { x = noseBaseX, y =  bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.78f },
                new Vector3 { x = noseBaseX, y =  bodyHalfWidthFront,   z =  0f },
                new Vector3 { x = noseBaseX, y =  bodyHalfWidthFront*0.58f, z =  bodyBottomFront * 0.75f },
                new Vector3 { x = noseBaseX, y =  0f,                   z =  bodyBottomFront - 0.35f },
                new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront*0.58f, z =  bodyBottomFront * 0.75f },
                new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront,   z =  0f },
                new Vector3 { x = noseBaseX, y = -bodyHalfWidthFront*0.55f, z =  bodyTopFront * 0.78f }
            };

            var frontRing = GetFrontRing();

            for (int i = 0; i < 8; i++)
            {
                int next = (i + 1) % 8;

                float avgZ = (baseRing[i].z + baseRing[next].z) * 0.5f;
                float avgY = (baseRing[i].y + baseRing[next].y) * 0.5f;

                string color;
                if (avgZ > Math.Abs(avgY) * 0.35f)
                    color = bodyColorLight;
                else if (avgZ < -Math.Abs(avgY) * 0.35f)
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

        public static List<ITriangleMeshWithColor>? SwanBodyTop()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var mid = GetMidRing();
            var back = GetBackRing();

            var centerFront = new Vector3 { x = bodyFrontX + 1f, y = 0f, z = bodyTopFront + 1.1f };
            var centerMid = new Vector3 { x = bodyMidX, y = 0f, z = bodyTopMid + 1.6f };
            var centerBack = new Vector3 { x = bodyBackX + 1f, y = 0f, z = bodyTopBack + 0.9f };

            tris.Add(CreateTriangleOutward(front[7], front[0], centerFront, BodyCenter, bodyColorLight));
            tris.Add(CreateTriangleOutward(front[0], front[1], centerFront, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(front[7], centerFront, mid[7], BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerFront, centerMid, mid[7], BodyCenter, bodyColorLight));
            tris.Add(CreateTriangleOutward(centerFront, front[1], mid[1], BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerFront, mid[1], centerMid, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(mid[7], centerMid, back[7], BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerMid, centerBack, back[7], BodyCenter, bodyColorDark));
            tris.Add(CreateTriangleOutward(centerMid, mid[1], back[1], BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(centerMid, back[1], centerBack, BodyCenter, bodyColorDark));

            tris.Add(CreateTriangleOutward(mid[7], mid[0], centerMid, BodyCenter, bodyColorLight));
            tris.Add(CreateTriangleOutward(mid[0], mid[1], centerMid, BodyCenter, bodyColorLight));

            tris.Add(CreateTriangleOutward(back[7], centerBack, back[0], BodyCenter, bodyColorMid));
            tris.Add(CreateTriangleOutward(back[0], centerBack, back[1], BodyCenter, bodyColorMid));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? SwanBodyBottom()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var mid = GetMidRing();
            var back = GetBackRing();

            var centerFront = new Vector3 { x = bodyFrontX + 1f, y = 0f, z = bodyBottomFront - 0.9f };
            var centerMid = new Vector3 { x = bodyMidX - 1f, y = 0f, z = keelDepthMid };
            var centerBack = new Vector3 { x = bodyBackX + 1f, y = 0f, z = bodyBottomBack - 0.6f };

            tris.Add(CreateTriangleOutward(front[5], centerFront, front[4], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[4], centerFront, front[3], BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(front[5], mid[5], centerMid, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[5], centerMid, centerFront, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerFront, centerMid, front[3], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(front[3], centerMid, mid[3], BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(mid[5], back[5], centerBack, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(mid[5], centerBack, centerMid, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(centerMid, centerBack, mid[3], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(mid[3], centerBack, back[3], BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(mid[5], centerMid, mid[4], BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(mid[4], centerMid, mid[3], BodyCenter, undersideColor));

            tris.Add(CreateTriangleOutward(back[5], back[4], centerBack, BodyCenter, undersideColor));
            tris.Add(CreateTriangleOutward(back[4], back[3], centerBack, BodyCenter, undersideColor));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? SwanBodySides()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = GetFrontRing();
            var mid = GetMidRing();
            var back = GetBackRing();

            AddQuadOutward(tris, front[7], mid[7], mid[6], front[6], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, front[6], mid[6], mid[5], front[5], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, mid[7], back[7], back[6], mid[6], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, mid[6], back[6], back[5], mid[5], BodyCenter, bodyColorDark);

            AddQuadOutward(tris, front[1], front[2], mid[2], mid[1], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, front[2], front[3], mid[3], mid[2], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, mid[1], mid[2], back[2], back[1], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, mid[2], mid[3], back[3], back[2], BodyCenter, bodyColorDark);

            AddQuadOutward(tris, back[7], back[1], back[2], back[6], BodyCenter, bodyColorDark);
            AddQuadOutward(tris, back[6], back[2], back[3], back[5], BodyCenter, bodyColorDark);

            return tris;
        }

        // ----------------------------------------------------
        //  CANOPY / CORE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? SwanCanopy()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float baseFrontZ = bodyTopFront + canopyBaseLiftFront;
            float baseMidZ = bodyTopMid + canopyBaseLiftMid;
            float baseBackZ = bodyTopBack + canopyBaseLiftBack;

            var lf = new Vector3 { x = canopyFrontX, y = -canopyHalfWidthFront, z = baseFrontZ };
            var rf = new Vector3 { x = canopyFrontX, y = canopyHalfWidthFront, z = baseFrontZ };

            var lm = new Vector3 { x = canopyMidX, y = -canopyHalfWidthMid, z = baseMidZ };
            var rm = new Vector3 { x = canopyMidX, y = canopyHalfWidthMid, z = baseMidZ };

            var lb = new Vector3 { x = canopyBackX, y = -canopyHalfWidthBack, z = baseBackZ };
            var rb = new Vector3 { x = canopyBackX, y = canopyHalfWidthBack, z = baseBackZ };

            var tf = new Vector3 { x = canopyFrontX + 0.5f, y = 0f, z = baseFrontZ + canopyHeightFront };
            var tm = new Vector3 { x = canopyMidX, y = 0f, z = baseMidZ + canopyHeightMid };
            var tb = new Vector3 { x = canopyBackX, y = 0f, z = baseBackZ + canopyHeightBack };

            tris.Add(CreateTriangleOutward(lf, tf, lm, BodyCenter, canopyColor));
            tris.Add(CreateTriangleOutward(tf, tm, lm, BodyCenter, canopyColor));

            tris.Add(CreateTriangleOutward(tf, rf, rm, BodyCenter, canopyColor));
            tris.Add(CreateTriangleOutward(tf, rm, tm, BodyCenter, canopyColor));

            tris.Add(CreateTriangleOutward(lm, tm, lb, BodyCenter, canopySoft));
            tris.Add(CreateTriangleOutward(tm, tb, lb, BodyCenter, canopySoft));

            tris.Add(CreateTriangleOutward(tm, rm, rb, BodyCenter, canopySoft));
            tris.Add(CreateTriangleOutward(tm, rb, tb, BodyCenter, canopySoft));

            tris.Add(CreateTriangleOutward(lf, rf, tf, BodyCenter, canopyColor));
            tris.Add(CreateTriangleOutward(lm, tm, rm, BodyCenter, canopySoft));
            tris.Add(CreateTriangleOutward(lb, tb, rb, BodyCenter, canopySoft));

            return tris;
        }

        public static List<ITriangleMeshWithColor>? SwanCore()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ftl = new Vector3 { x = coreFrontX, y = -coreHalfWidth, z = coreCenterZ + coreHalfHeight };
            var ftr = new Vector3 { x = coreFrontX, y = coreHalfWidth, z = coreCenterZ + coreHalfHeight };
            var fbl = new Vector3 { x = coreFrontX, y = -coreHalfWidth, z = coreCenterZ - coreHalfHeight };
            var fbr = new Vector3 { x = coreFrontX, y = coreHalfWidth, z = coreCenterZ - coreHalfHeight };

            var btl = new Vector3 { x = coreBackX, y = -coreHalfWidth, z = coreCenterZ + coreHalfHeight };
            var btr = new Vector3 { x = coreBackX, y = coreHalfWidth, z = coreCenterZ + coreHalfHeight };
            var bbl = new Vector3 { x = coreBackX, y = -coreHalfWidth, z = coreCenterZ - coreHalfHeight };
            var bbr = new Vector3 { x = coreBackX, y = coreHalfWidth, z = coreCenterZ - coreHalfHeight };

            AddQuadOutward(tris, ftl, ftr, btr, btl, BodyCenter, coreColorSoft);
            AddQuadOutward(tris, fbl, bbl, bbr, fbr, BodyCenter, coreColorSoft);
            AddQuadOutward(tris, ftr, fbr, bbr, btr, BodyCenter, coreColor);
            AddQuadOutward(tris, fbl, ftl, btl, bbl, BodyCenter, coreColor);
            AddQuadOutward(tris, btl, btr, bbr, bbl, BodyCenter, coreColor);
            AddQuadOutward(tris, ftl, fbl, fbr, ftr, BodyCenter, coreColorSoft);

            return tris;
        }

        // ----------------------------------------------------
        //  WINGS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? SwanWingSegment(bool isLeft, int segmentIndex)
        {
            var tris = new List<ITriangleMeshWithColor>();

            WingSegmentSpec spec = GetWingSegmentSpec(isLeft, segmentIndex);

            var rootLeadingTop = new Vector3 { x = spec.rootLeadingX, y = spec.rootY, z = spec.rootZ + spec.rootThickness };
            var rootLeadingBottom = new Vector3 { x = spec.rootLeadingX, y = spec.rootY, z = spec.rootZ - spec.rootThickness };
            var rootTrailingTop = new Vector3 { x = spec.rootTrailingX, y = spec.rootY, z = spec.rootZ + spec.rootThickness * 0.45f };
            var rootTrailingBottom = new Vector3 { x = spec.rootTrailingX, y = spec.rootY, z = spec.rootZ - spec.rootThickness * 0.45f };

            var tipLeadingTop = new Vector3 { x = spec.tipLeadingX, y = spec.tipY, z = spec.tipZ + spec.tipThickness };
            var tipLeadingBottom = new Vector3 { x = spec.tipLeadingX, y = spec.tipY, z = spec.tipZ - spec.tipThickness };
            var tipTrailingTop = new Vector3 { x = spec.tipTrailingX, y = spec.tipY, z = spec.tipZ + spec.tipThickness * 0.35f };
            var tipTrailingBottom = new Vector3 { x = spec.tipTrailingX, y = spec.tipY, z = spec.tipZ - spec.tipThickness * 0.35f };

            AddQuadOutward(tris, rootLeadingTop, tipLeadingTop, tipTrailingTop, rootTrailingTop, BodyCenter, spec.topColor);
            AddQuadOutward(tris, rootLeadingBottom, rootTrailingBottom, tipTrailingBottom, tipLeadingBottom, BodyCenter, undersideColor);

            AddQuadOutward(tris, rootLeadingTop, rootLeadingBottom, tipLeadingBottom, tipLeadingTop, BodyCenter, spec.edgeColor);
            AddQuadOutward(tris, rootTrailingBottom, rootTrailingTop, tipTrailingTop, tipTrailingBottom, BodyCenter, spec.edgeColor);

            AddQuadOutward(tris, rootLeadingTop, rootTrailingTop, rootTrailingBottom, rootLeadingBottom, BodyCenter, spec.rootColor);
            AddQuadOutward(tris, tipLeadingTop, tipLeadingBottom, tipTrailingBottom, tipTrailingTop, BodyCenter, spec.tipColor);

            var upperSpineRoot = new Vector3 { x = (spec.rootLeadingX + spec.rootTrailingX) * 0.5f, y = spec.rootY, z = spec.rootZ + spec.rootThickness * 1.5f };
            var upperSpineTip = new Vector3 { x = (spec.tipLeadingX + spec.tipTrailingX) * 0.5f, y = spec.tipY, z = spec.tipZ + spec.tipThickness * 1.4f };

            tris.Add(CreateTriangleOutward(rootLeadingTop, upperSpineRoot, tipLeadingTop, BodyCenter, spec.topColor));
            tris.Add(CreateTriangleOutward(upperSpineRoot, upperSpineTip, tipLeadingTop, BodyCenter, spec.topColor));
            tris.Add(CreateTriangleOutward(rootTrailingTop, tipTrailingTop, upperSpineRoot, BodyCenter, spec.topColor));
            tris.Add(CreateTriangleOutward(upperSpineRoot, tipTrailingTop, upperSpineTip, BodyCenter, spec.topColor));

            return tris;
        }

        private static WingSegmentSpec GetWingSegmentSpec(bool isLeft, int segmentIndex)
        {
            float side = isLeft ? -1f : 1f;

            float rootLeadingX;
            float rootTrailingX;
            float rootY;
            float rootZ;
            float rootChord;
            float rootThickness;

            float tipLeadingX;
            float tipTrailingX;
            float tipY;
            float tipZ;
            float tipChord;
            float tipThickness;

            string topColor;
            string edgeColor;
            string rootColor;
            string tipColor;

            if (segmentIndex == 0)
            {
                rootLeadingX = wingRootX;
                rootY = side * wingRootY;
                rootZ = wingRootZ;
                rootChord = wingChordRoot;
                rootThickness = wingThicknessRoot;

                tipLeadingX = wingSeg1TipX;
                tipY = side * wingSeg1TipY;
                tipZ = wingSeg1TipZ;
                tipChord = wingChordSeg1;
                tipThickness = wingThicknessSeg1;

                topColor = wingColorLight;
                edgeColor = wingColorMid;
                rootColor = wingColorMid;
                tipColor = wingColorMid;
            }
            else if (segmentIndex == 1)
            {
                rootLeadingX = wingSeg1TipX;
                rootY = side * wingSeg1TipY;
                rootZ = wingSeg1TipZ;
                rootChord = wingChordSeg1;
                rootThickness = wingThicknessSeg1;

                tipLeadingX = wingSeg2TipX;
                tipY = side * wingSeg2TipY;
                tipZ = wingSeg2TipZ;
                tipChord = wingChordSeg2;
                tipThickness = wingThicknessSeg2;

                topColor = wingColorLight;
                edgeColor = wingColorMid;
                rootColor = wingColorMid;
                tipColor = wingColorDark;
            }
            else
            {
                rootLeadingX = wingSeg2TipX;
                rootY = side * wingSeg2TipY;
                rootZ = wingSeg2TipZ;
                rootChord = wingChordSeg2;
                rootThickness = wingThicknessSeg2;

                tipLeadingX = wingSeg3TipX;
                tipY = side * wingSeg3TipY;
                tipZ = wingSeg3TipZ;
                tipChord = wingChordSeg3;
                tipThickness = wingThicknessSeg3;

                topColor = wingColorMid;
                edgeColor = wingColorDark;
                rootColor = wingColorDark;
                tipColor = wingColorDark;
            }

            rootTrailingX = rootLeadingX + rootChord;
            tipTrailingX = tipLeadingX + tipChord;

            return new WingSegmentSpec
            {
                rootLeadingX = rootLeadingX,
                rootTrailingX = rootTrailingX,
                rootY = rootY,
                rootZ = rootZ,
                rootThickness = rootThickness,

                tipLeadingX = tipLeadingX,
                tipTrailingX = tipTrailingX,
                tipY = tipY,
                tipZ = tipZ,
                tipThickness = tipThickness,

                topColor = topColor,
                edgeColor = edgeColor,
                rootColor = rootColor,
                tipColor = tipColor
            };
        }

        // ----------------------------------------------------
        //  TAIL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? SwanTailPlanes()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left tail plane
            {
                var a = new Vector3 { x = tailRootX, y = -tailRootY, z = tailRootZ };
                var b = new Vector3 { x = tailMidX, y = -tailTipY * 0.75f, z = tailTipZ };
                var c = new Vector3 { x = tailTipX, y = -tailTipY, z = tailTipZ - 0.2f };
                var d = new Vector3 { x = tailMidX + 2f, y = -tailRootY * 0.8f, z = tailBottomZ };

                tris.Add(CreateTriangleOutward(a, b, c, BodyCenter, tailColor, noHidden: true));
                tris.Add(CreateTriangleOutward(a, c, d, BodyCenter, tailColor, noHidden: true));

                var a2 = new Vector3 { x = a.x, y = a.y + 0.9f, z = a.z - 0.35f };
                var b2 = new Vector3 { x = b.x, y = b.y + 0.9f, z = b.z - 0.35f };
                var c2 = new Vector3 { x = c.x, y = c.y + 0.8f, z = c.z - 0.35f };
                var d2 = new Vector3 { x = d.x, y = d.y + 0.9f, z = d.z - 0.35f };

                // Inner face: use a reference on the outer side so normals point inward
                var leftOuterRef = new Vector3 { x = (a.x + c.x) * 0.5f, y = a.y - 5f, z = (a.z + c.z) * 0.5f };
                tris.Add(CreateTriangleOutward(a2, c2, b2, leftOuterRef, tailColor, noHidden: true));
                tris.Add(CreateTriangleOutward(a2, d2, c2, leftOuterRef, tailColor, noHidden: true));

                AddQuadOutward(tris, a, a2, b2, b, BodyCenter, tailColor);
                AddQuadOutward(tris, b, b2, c2, c, BodyCenter, tailColor);
                AddQuadOutward(tris, c, c2, d2, d, BodyCenter, tailColor);
                AddQuadOutward(tris, d, d2, a2, a, BodyCenter, tailColor);
            }

            // Right tail plane
            {
                var a = new Vector3 { x = tailRootX, y = tailRootY, z = tailRootZ };
                var b = new Vector3 { x = tailMidX, y = tailTipY * 0.75f, z = tailTipZ };
                var c = new Vector3 { x = tailTipX, y = tailTipY, z = tailTipZ - 0.2f };
                var d = new Vector3 { x = tailMidX + 2f, y = tailRootY * 0.8f, z = tailBottomZ };

                tris.Add(CreateTriangleOutward(a, c, b, BodyCenter, tailColor, noHidden: true));
                tris.Add(CreateTriangleOutward(a, d, c, BodyCenter, tailColor, noHidden: true));

                var a2 = new Vector3 { x = a.x, y = a.y - 0.9f, z = a.z - 0.35f };
                var b2 = new Vector3 { x = b.x, y = b.y - 0.9f, z = b.z - 0.35f };
                var c2 = new Vector3 { x = c.x, y = c.y - 0.8f, z = c.z - 0.35f };
                var d2 = new Vector3 { x = d.x, y = d.y - 0.9f, z = d.z - 0.35f };

                // Inner face: use a reference on the outer side so normals point inward
                var rightOuterRef = new Vector3 { x = (a.x + c.x) * 0.5f, y = a.y + 5f, z = (a.z + c.z) * 0.5f };
                tris.Add(CreateTriangleOutward(a2, b2, c2, rightOuterRef, tailColor, noHidden: true));
                tris.Add(CreateTriangleOutward(a2, c2, d2, rightOuterRef, tailColor, noHidden: true));

                AddQuadOutward(tris, a, b, b2, a2, BodyCenter, tailColor);
                AddQuadOutward(tris, b, c, c2, b2, BodyCenter, tailColor);
                AddQuadOutward(tris, c, d, d2, c2, BodyCenter, tailColor);
                AddQuadOutward(tris, d, a, a2, d2, BodyCenter, tailColor);
            }

            return tris;
        }

        public static List<ITriangleMeshWithColor>? SwanVerticalFin()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var a = new Vector3 { x = verticalFinFrontX, y = 0f, z = bodyTopBack + 0.9f };
            var b = new Vector3 { x = verticalFinTipX, y = 0f, z = bodyTopBack + verticalFinHeight };
            var c = new Vector3 { x = verticalFinTipX, y = 0f, z = bodyTopBack + 1.2f };

            var a2 = new Vector3 { x = verticalFinFrontX, y = verticalFinThickness, z = bodyTopBack + 0.9f };
            var b2 = new Vector3 { x = verticalFinTipX, y = verticalFinThickness, z = bodyTopBack + verticalFinHeight };
            var c2 = new Vector3 { x = verticalFinTipX, y = verticalFinThickness, z = bodyTopBack + 1.2f };

            var a3 = new Vector3 { x = verticalFinFrontX, y = -verticalFinThickness, z = bodyTopBack + 0.9f };
            var b3 = new Vector3 { x = verticalFinTipX, y = -verticalFinThickness, z = bodyTopBack + verticalFinHeight };
            var c3 = new Vector3 { x = verticalFinTipX, y = -verticalFinThickness, z = bodyTopBack + 1.2f };

            tris.Add(CreateTriangleOutward(a3, b3, c3, BodyCenter, tailColor));
            tris.Add(CreateTriangleOutward(a2, c2, b2, BodyCenter, tailColor));

            AddQuadOutward(tris, a3, a2, b2, b3, BodyCenter, tailColor);
            AddQuadOutward(tris, a3, c3, c2, a2, BodyCenter, tailColor);
            AddQuadOutward(tris, c3, b3, b2, c2, BodyCenter, tailColor);

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
                    new Vector3 { x = bodyBackX - 4f, y =  4f, z =  1f },
                    new Vector3 { x = bodyBackX - 4f, y = -4f, z =  1f },
                    new Vector3 { x = bodyBackX - 16f, y =  0f, z =  0f },
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
                    new Vector3 { x = bodyBackX + 2f, y =  3.5f, z =  0.8f },
                    new Vector3 { x = bodyBackX + 2f, y = -3.5f, z =  0.8f },
                    new Vector3 { x = bodyBackX - 7f, y =  0f, z =  0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        public static List<List<IVector3>>? SwanCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var noseBounds = ScaleCrashBoxBounds(
                new Vector3 { x = noseBaseX, y = -6.0f, z = -3.2f },
                new Vector3 { x = noseTipX, y = 6.0f, z = 4.0f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(noseBounds.min, noseBounds.max));

            var bodyBounds = ScaleCrashBoxBounds(
                new Vector3 { x = bodyBackX, y = -bodyHalfWidthMid, z = keelDepthMid },
                new Vector3 { x = bodyFrontX + 3f, y = bodyHalfWidthMid, z = bodyTopMid + canopyBaseLiftMid + canopyHeightMid });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(bodyBounds.min, bodyBounds.max));

            var rearBounds = ScaleCrashBoxBounds(
                new Vector3 { x = tailRootX, y = -tailTipY, z = bodyBottomBack - 1.0f },
                new Vector3 { x = tailTipX + 2f, y = tailTipY, z = bodyTopBack + verticalFinHeight });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rearBounds.min, rearBounds.max));

            var leftWingBounds = ScaleCrashBoxBounds(
                new Vector3 { x = wingSeg2TipX, y = -wingSeg3TipY - 2f, z = wingSeg3TipZ - 1.8f },
                new Vector3 { x = wingRootX + wingChordRoot, y = -wingRootY + 2f, z = wingRootZ + 2.2f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(leftWingBounds.min, leftWingBounds.max));

            var rightWingBounds = ScaleCrashBoxBounds(
                new Vector3 { x = wingSeg2TipX, y = wingRootY - 2f, z = wingSeg3TipZ - 1.8f },
                new Vector3 { x = wingRootX + wingChordRoot, y = wingSeg3TipY + 2f, z = wingRootZ + 2.2f });
            boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(rightWingBounds.min, rightWingBounds.max));

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

        private class WingSegmentSpec
        {
            public float rootLeadingX;
            public float rootTrailingX;
            public float rootY;
            public float rootZ;
            public float rootThickness;

            public float tipLeadingX;
            public float tipTrailingX;
            public float tipY;
            public float tipZ;
            public float tipThickness;

            public string topColor = "";
            public string edgeColor = "";
            public string rootColor = "";
            public string tipColor = "";
        }
    }
}