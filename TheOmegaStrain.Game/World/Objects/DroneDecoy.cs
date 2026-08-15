using System;
using System.Collections.Generic;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using static TheOmegaStrain.Game.Helpers.OmegaObject3DHelpers;

namespace TheOmegaStrain.Game.World.Objects
{
    public class DecoyBeacon
    {
        private const float ZoomRatio = 1f;

        // ----------------------------------------------------
        //  GEOMETRY PARAMETERS
        // ----------------------------------------------------

        private static float coreHalfWidth = 10f;
        private static float coreHalfHeight = 7f;
        private static float coreHalfDepth = 10f;

        private static float finLength = 18f;
        private static float finWidth = 8f;
        private static float finThickness = 2.5f;

        private static float antennaHeight = 12f;
        private static float antennaWidth = 2.2f;

        private static float pulseInset = 1.2f;
        private static float pulseSize = 5.2f;
        private const float CrashboxSize = 3.5f;

        private static readonly Vector3 BodyCenter = new Vector3 { x = 0, y = 0, z = 0 };

        // ----------------------------------------------------
        //  COLORS
        // ----------------------------------------------------

        private static string shellLight = "8B919B";
        private static string shellMid = "565C66";
        private static string shellDark = "2B2F38";
        private static string finLight = "707781";
        private static string finDark = "3E434B";
        private static string pulseCore = "33DDEE";
        private static string pulseGlow = "FF9D2E";
        private static string antennaColor = "7C828C";
        private static string antennaTipColor = "FFCC33";

        public static OmegaObject3D CreateDecoyBeacon(ISurface parentSurface)
        {
            var core = DecoyCore();
            var frontPulse = DecoyFrontPulsePanel();
            var fins = DecoyFins();
            var antenna = DecoyAntenna();

            var crashBoxes = DecoyCrashBoxes();
            var guide = ParticlesDirectionGuide();
            var startGuide = ParticlesStartGuide();

            var decoy = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++
            };

            AddPart(decoy, "DecoyCore", core, true);
            AddPart(decoy, "DecoyFrontPulsePanel", frontPulse, true);
            AddPart(decoy, "DecoyFins", fins, true);
            AddPart(decoy, "DecoyAntenna", antenna, true);

            AddPart(decoy, "DecoyParticlesGuide", guide, false);
            AddPart(decoy, "DecoyParticlesStartGuide", startGuide, false);

            // Replace later with a dedicated DecoyBeaconControls if you want.
            decoy.Movement = new DecoyBeaconControls();
            decoy.Particles = new ParticlesAI();

            decoy.ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 };
            decoy.Rotation = new Vector3 { x = 0, y = 0, z = 0 };

            if (crashBoxes != null)
                decoy.CrashBoxes = crashBoxes;

            decoy.ParentSurface = parentSurface;
            decoy.HasShadow = true;

            OmegaObject3DHelpers.ApplyScaleToObject(decoy, ZoomRatio);

            OmegaObject3DHelpers.AddSimplifiedShadowPart(decoy, useFlatQuad: true);

            return decoy;
        }

        // ----------------------------------------------------
        //  CORE
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DecoyCore()
        {
            var tris = new List<ITriangleMeshWithColor>();

            var front = new Vector3 { x = coreHalfDepth, y = 0, z = 0 };
            var back = new Vector3 { x = -coreHalfDepth, y = 0, z = 0 };
            var left = new Vector3 { x = 0, y = -coreHalfWidth, z = 0 };
            var right = new Vector3 { x = 0, y = coreHalfWidth, z = 0 };
            var top = new Vector3 { x = 0, y = 0, z = coreHalfHeight };
            var bottom = new Vector3 { x = 0, y = 0, z = -coreHalfHeight };

            // Upper half
            tris.Add(CreateTriangleOutward(front, top, right, BodyCenter, shellLight));
            tris.Add(CreateTriangleOutward(front, left, top, BodyCenter, shellMid));
            tris.Add(CreateTriangleOutward(back, right, top, BodyCenter, shellMid));
            tris.Add(CreateTriangleOutward(back, top, left, BodyCenter, shellDark));

            // Lower half
            tris.Add(CreateTriangleOutward(front, right, bottom, BodyCenter, shellMid));
            tris.Add(CreateTriangleOutward(front, bottom, left, BodyCenter, shellDark));
            tris.Add(CreateTriangleOutward(back, bottom, right, BodyCenter, shellDark));
            tris.Add(CreateTriangleOutward(back, left, bottom, BodyCenter, shellMid));

            return tris;
        }

        // ----------------------------------------------------
        //  FRONT PULSE PANEL
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DecoyFrontPulsePanel()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float x = coreHalfDepth + pulseInset;

            var top = new Vector3 { x = x, y = 0, z = pulseSize };
            var topRight = new Vector3 { x = x, y = pulseSize * 0.86f, z = pulseSize * 0.5f };
            var bottomRight = new Vector3 { x = x, y = pulseSize * 0.86f, z = -pulseSize * 0.5f };
            var bottom = new Vector3 { x = x, y = 0, z = -pulseSize };
            var bottomLeft = new Vector3 { x = x, y = -pulseSize * 0.86f, z = -pulseSize * 0.5f };
            var topLeft = new Vector3 { x = x, y = -pulseSize * 0.86f, z = pulseSize * 0.5f };

            var center = new Vector3 { x = x + 0.2f, y = 0, z = 0 };

            tris.Add(CreateTriangleOutward(top, topRight, center, BodyCenter, pulseGlow));
            tris.Add(CreateTriangleOutward(topRight, bottomRight, center, BodyCenter, pulseCore));
            tris.Add(CreateTriangleOutward(bottomRight, bottom, center, BodyCenter, pulseGlow));
            tris.Add(CreateTriangleOutward(bottom, bottomLeft, center, BodyCenter, pulseCore));
            tris.Add(CreateTriangleOutward(bottomLeft, topLeft, center, BodyCenter, pulseGlow));
            tris.Add(CreateTriangleOutward(topLeft, top, center, BodyCenter, pulseCore));

            return tris;
        }

        // ----------------------------------------------------
        //  FINS
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DecoyFins()
        {
            var tris = new List<ITriangleMeshWithColor>();

            // Left fin
            AddFin(
                tris,
                new Vector3 { x = 0, y = -(coreHalfWidth - 1f), z = 0 },
                new Vector3 { x = 0, y = -1, z = 0 },
                new Vector3 { x = 1, y = 0, z = 0 },
                finLength,
                finWidth,
                finThickness,
                finLight,
                finDark);

            // Right fin
            AddFin(
                tris,
                new Vector3 { x = 0, y = (coreHalfWidth - 1f), z = 0 },
                new Vector3 { x = 0, y = 1, z = 0 },
                new Vector3 { x = 1, y = 0, z = 0 },
                finLength,
                finWidth,
                finThickness,
                finLight,
                finDark);

            // Top rear fin
            AddFin(
                tris,
                new Vector3 { x = -2f, y = 0, z = coreHalfHeight - 0.5f },
                new Vector3 { x = 0, y = 0, z = 1 },
                new Vector3 { x = -1, y = 0, z = 0 },
                finLength * 0.75f,
                finWidth * 0.7f,
                finThickness,
                finLight,
                finDark);

            // Bottom rear fin
            AddFin(
                tris,
                new Vector3 { x = -2f, y = 0, z = -(coreHalfHeight - 0.5f) },
                new Vector3 { x = 0, y = 0, z = -1 },
                new Vector3 { x = -1, y = 0, z = 0 },
                finLength * 0.75f,
                finWidth * 0.7f,
                finThickness,
                finLight,
                finDark);

            return tris;
        }

        private static void AddFin(
            List<ITriangleMeshWithColor> tris,
            Vector3 root,
            Vector3 outward,
            Vector3 forward,
            float length,
            float width,
            float thickness,
            string topColor,
            string sideColor)
        {
            var side = Normalize(Cross(outward, forward));

            var rootLeft = Add(root, Scale(side, -width * 0.5f));
            var rootRight = Add(root, Scale(side, width * 0.5f));
            var tip = Add(Add(root, Scale(outward, length)), Scale(forward, length * 0.15f));

            var rootLeftBack = Add(rootLeft, Scale(forward, -thickness));
            var rootRightBack = Add(rootRight, Scale(forward, -thickness));
            var tipBack = Add(tip, Scale(forward, -thickness));

            tris.Add(CreateTriangleOutward(rootLeft, rootRight, tip, BodyCenter, topColor));
            tris.Add(CreateTriangleOutward(rootLeftBack, tipBack, rootRightBack, BodyCenter, sideColor));

            AddQuadOutward(tris, rootLeft, rootLeftBack, rootRightBack, rootRight, BodyCenter, sideColor);
            AddQuadOutward(tris, rootRight, rootRightBack, tipBack, tip, BodyCenter, sideColor);
            AddQuadOutward(tris, tip, tipBack, rootLeftBack, rootLeft, BodyCenter, sideColor);
        }

        // ----------------------------------------------------
        //  ANTENNA
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? DecoyAntenna()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float baseZ = coreHalfHeight;
            float topZ = coreHalfHeight + antennaHeight;
            float halfW = antennaWidth * 0.5f;

            var p1 = new Vector3 { x = -halfW, y = -halfW, z = baseZ + 2f };
            var p2 = new Vector3 { x = -halfW, y = halfW, z = baseZ + 2f };
            var p3 = new Vector3 { x = halfW, y = halfW, z = baseZ + 2f };
            var p4 = new Vector3 { x = halfW, y = -halfW, z = baseZ + 2f };

            var tip = new Vector3 { x = 0, y = 0, z = topZ };

            tris.Add(CreateTriangleOutward(p1, tip, p2, BodyCenter, antennaColor));
            tris.Add(CreateTriangleOutward(p2, tip, p3, BodyCenter, antennaColor));
            tris.Add(CreateTriangleOutward(p3, tip, p4, BodyCenter, antennaColor));
            tris.Add(CreateTriangleOutward(p4, tip, p1, BodyCenter, antennaColor));

            // Bright tip
            float tipBaseZ = topZ - 2f;
            var t1 = new Vector3 { x = -1f, y = 0, z = tipBaseZ };
            var t2 = new Vector3 { x = 0, y = 1f, z = tipBaseZ };
            var t3 = new Vector3 { x = 1f, y = 0, z = tipBaseZ };
            var t4 = new Vector3 { x = 0, y = -1f, z = tipBaseZ };
            var top = new Vector3 { x = 0, y = 0, z = topZ + 1.5f };

            tris.Add(CreateTriangleOutward(top, t1, t2, BodyCenter, antennaTipColor));
            tris.Add(CreateTriangleOutward(top, t2, t3, BodyCenter, antennaTipColor));
            tris.Add(CreateTriangleOutward(top, t3, t4, BodyCenter, antennaTipColor));
            tris.Add(CreateTriangleOutward(top, t4, t1, BodyCenter, antennaTipColor));

            return tris;
        }

        // ----------------------------------------------------
        //  PARTICLE GUIDES
        // ----------------------------------------------------

        public static List<ITriangleMeshWithColor>? ParticlesDirectionGuide()
        {
            return new List<ITriangleMeshWithColor>
            {
                CreateTriangleOutward(
                    new Vector3 { x = -14f, y =  4f, z = 0f },
                    new Vector3 { x = -14f, y = -4f, z = 0f },
                    new Vector3 { x = -24f, y =  0f, z = 0f },
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
                    new Vector3 { x = -6f, y =  3f, z = 0f },
                    new Vector3 { x = -6f, y = -3f, z = 0f },
                    new Vector3 { x = -12f, y = 0f, z = 0f },
                    BodyCenter,
                    "ffffff",
                    noHidden: true)
            };
        }

        // ----------------------------------------------------
        //  CRASH BOXES
        // ----------------------------------------------------

        public static List<List<IVector3>>? DecoyCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            var bounds = ScaleCrashBoxBounds(
                new Vector3
                {
                    x = -coreHalfDepth,
                    y = -coreHalfWidth,
                    z = -coreHalfHeight
                },
                new Vector3
                {
                    x = coreHalfDepth + 2f,
                    y = coreHalfWidth,
                    z = coreHalfHeight + antennaHeight
                });

            boxes.Add(OmegaObject3DHelpers.GenerateCrashBoxCorners(bounds.min, bounds.max));

            return boxes;
        }

        // ----------------------------------------------------
        //  HELPERS
        // ----------------------------------------------------

        private static void AddPart(OmegaObject3D obj, string name, List<ITriangleMeshWithColor>? tris, bool visible)
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
                });
        }

            }
        }