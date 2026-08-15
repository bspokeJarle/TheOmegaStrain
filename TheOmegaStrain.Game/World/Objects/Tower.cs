using System;
using System.Collections.Generic;
using _3dRotations.World;
using _3dRotations.Helpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using static Domain._3dSpecificsImplementations;
using static _3dRotations.Helpers._3dObjectHelpers;

namespace _3dRotations.World.Objects
{
    public class Tower
    {
        // ----------------------------------------------------
        //  DETAIL / TRIANGLE BUDGET CONTROLS
        // ----------------------------------------------------

        private static TowerDetailPreset DetailPreset = TowerDetailPreset.Medium;

        // 0 = use preset, 1 = Low, 2 = Medium, 3 = High
        private static int BaseDetailOverride = 0;
        private static int ShaftDetailOverride = 0;
        private static int HeadDetailOverride = 0;
        private static int RadarDetailOverride = 0;

        private enum TowerDetailPreset
        {
            Low = 0,
            Medium = 1,
            High = 2
        }

        private static int GetDetailLevelOrPreset(int overrideValue)
        {
            // Convert preset (0..2) -> detailLevel (1..3)
            return overrideValue > 0 ? overrideValue : ((int)DetailPreset + 1);
        }

        private static int GetSegmentsForPart(string partName, int detailLevel)
        {
            // detailLevel: 1=Low, 2=Medium, 3=High
            return partName switch
            {
                "Shaft" => detailLevel switch { 1 => 6, 2 => 8, 3 => 10, _ => 8 },
                "Head" => detailLevel switch { 1 => 6, 2 => 8, 3 => 10, _ => 8 },
                "Radar" => detailLevel switch { 1 => 6, 2 => 8, 3 => 10, _ => 8 },
                _ => 8
            };
        }

        // ----------------------------------------------------
        //  GEOMETRY PARAMETERS (local model space, Z is up)
        // ----------------------------------------------------

        // Base (box)
        private static float baseHalfSize = 32f;
        private static float baseHeight = 22f;

        // Decals must be pushed outward to avoid sorting / z-fighting
        // You tested: 6 looks best.
        private static float decalPushOut = 6.0f;

        // Shaft (tapered cylinder/frustum)
        private static float shaftBottomRadius = 16f;
        private static float shaftTopRadius = 12f;
        private static float shaftHeight = 90f;

        // Head frame (observation section)
        private static float headBottomRadius = 18f;
        private static float headTopRadius = 22f;
        private static float headHeight = 22f;

        // Glass ring inside head
        private static float glassInset = 1.5f;
        private static float glassHeight = 10f;

        // Roof
        private static float roofHeight = 6f;

        // Radar + mast
        private static float mastHeight = 12f;
        private static float mastHalfSize = 2.2f;
        private static float dishRadius = 10f;
        private static float dishThickness = 0.8f;

        // ----------------------------------------------------
        //  SHADING POLICY
        // ----------------------------------------------------
        // Only "regular surfaces" get shades. Decals do NOT get shades.
        // Glass is kept flat for readability (no shading).
        private static bool ShadeRegularSurfaces = true;

        // ----------------------------------------------------
        //  COLORS (hex without #)
        // ----------------------------------------------------
        private static string baseColor = "5A5A5A";
        private static string trimColor = "EDEDED";
        private static string shaftColor = "B8783C";
        private static string headFrameColor = "D9A26A";
        private static string glassColor = "88AEDD";
        private static string doorColor = "2F2F2F";
        private static string radarColor = "BFBFBF";
        private static string antennaTipColor = "FF3344";

        // ----------------------------------------------------
        //  PUBLIC FACTORY
        // ----------------------------------------------------

        public static _3dObject CreateTower(ISurface parentSurface)
        {
            var baseBlock = TowerBase();                      // shaded
            var baseDecals = TowerBaseDecals_DoorAndWindows();  // NOT shaded
            var shaft = TowerShaft();                     // shaded
            var headFrame = TowerHeadFrame();                 // shaded
            var headGlass = TowerHeadGlass();                 // NOT shaded (flat)
            var roof = TowerRoof();                      // shaded
            var radar = TowerRadar();                     // shaded

            var crashBoxes = TowerCrashBoxes();

            var tower = new _3dObject{ ObjectId = GameState.ObjectIdCounter++ };
            tower.HasShadow = true;

            AddPart(tower, "TowerBase", baseBlock, true);
            AddPart(tower, "TowerBaseDecals", baseDecals, true); // keep after base for stable sorting
            AddPart(tower, "TowerShaft", shaft, true);
            AddPart(tower, "TowerHeadFrame", headFrame, true);
            AddPart(tower, "TowerHeadGlass", headGlass, true);
            AddPart(tower, "TowerRoof", roof, true);
            AddPart(tower, "TowerRadar", radar, true);

            tower.ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 };
            tower.Rotation = new Vector3 { x = 0, y = 0, z = 0 };

            if (crashBoxes != null)
                tower.CrashBoxes = crashBoxes;

            tower.ParentSurface = parentSurface;

            _3dObjectHelpers.AddSimplifiedShadowPart(tower, useFlatQuad: false);
            _3dObjectHelpers.NormalizeSurfaceFootprintPivot(tower);

            return tower;
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

        // ----------------------------------------------------
        //  PARTS
        // ----------------------------------------------------

        // Base building: simple box from z=0..baseHeight.
        // Uses local center for correct outward normals (hidden-face).
        public static List<ITriangleMeshWithColor>? TowerBase()
        {
            float z0 = 0f;
            float z1 = baseHeight;

            var min = new Vector3 { x = -baseHalfSize, y = -baseHalfSize, z = z0 };
            var max = new Vector3 { x = baseHalfSize, y = baseHalfSize, z = z1 };

            Vector3 baseCenter = new Vector3 { x = 0, y = 0, z = (z0 + z1) * 0.5f };

            return CreateBox(min, max, baseCenter, baseColor, shaded: ShadeRegularSurfaces);
        }

        /// <summary>
        /// Door + windows pushed outward from the base walls to avoid z-fighting / sorting issues.
        /// Decals are NOT shaded.
        /// </summary>
        public static List<ITriangleMeshWithColor>? TowerBaseDecals_DoorAndWindows()
        {
            var tris = new List<ITriangleMeshWithColor>();

            float baseZ0 = 0f;
            float baseZ1 = baseHeight;

            Vector3 baseCenter = new Vector3 { x = 0, y = 0, z = (baseZ0 + baseZ1) * 0.5f };

            float WallOut(float wallCoord, float sign) => wallCoord + (decalPushOut * sign);

            void AddQuad_NoShade(Vector3 v1, Vector3 v2, Vector3 v3, Vector3 v4, string color)
            {
                // No shade: decals keep their base color
                tris.Add(CreateTriangleOutward(v1, v2, v3, baseCenter, color));
                tris.Add(CreateTriangleOutward(v1, v3, v4, baseCenter, color));
            }

            // Door on -Y wall
            {
                float doorWidth = 8f;
                float doorHeight = 9f;
                float halfDoorW = doorWidth * 0.5f;

                float doorZ0 = 0.8f;
                float doorZ1 = Math.Min(baseHeight - 1.0f, doorZ0 + doorHeight);

                float yDoor = WallOut(-baseHalfSize, -1f);

                var d1 = new Vector3 { x = halfDoorW, y = yDoor, z = doorZ0 };
                var d2 = new Vector3 { x = -halfDoorW, y = yDoor, z = doorZ0 };
                var d3 = new Vector3 { x = -halfDoorW, y = yDoor, z = doorZ1 };
                var d4 = new Vector3 { x = halfDoorW, y = yDoor, z = doorZ1 };

                AddQuad_NoShade(d2, d1, d4, d3, doorColor);
            }

            // Windows
            {
                float winW = 7f;
                float winH = 5f;
                float halfWinW = winW * 0.5f;

                float winZ0 = Math.Min(baseHeight - (winH + 2f), 10f);
                if (winZ0 < 3f) winZ0 = 3f;
                float winZ1 = winZ0 + winH;

                // +X window
                {
                    float x = WallOut(baseHalfSize, +1f);

                    var v1 = new Vector3 { x = x, y = -halfWinW, z = winZ0 };
                    var v2 = new Vector3 { x = x, y = halfWinW, z = winZ0 };
                    var v3 = new Vector3 { x = x, y = halfWinW, z = winZ1 };
                    var v4 = new Vector3 { x = x, y = -halfWinW, z = winZ1 };

                    AddQuad_NoShade(v1, v2, v3, v4, glassColor);
                }

                // -X window
                {
                    float x = WallOut(-baseHalfSize, -1f);

                    var v1 = new Vector3 { x = x, y = halfWinW, z = winZ0 };
                    var v2 = new Vector3 { x = x, y = -halfWinW, z = winZ0 };
                    var v3 = new Vector3 { x = x, y = -halfWinW, z = winZ1 };
                    var v4 = new Vector3 { x = x, y = halfWinW, z = winZ1 };

                    AddQuad_NoShade(v1, v2, v3, v4, glassColor);
                }

                // +Y window
                {
                    float y = WallOut(baseHalfSize, +1f);

                    var v1 = new Vector3 { x = -halfWinW, y = y, z = winZ0 };
                    var v2 = new Vector3 { x = halfWinW, y = y, z = winZ0 };
                    var v3 = new Vector3 { x = halfWinW, y = y, z = winZ1 };
                    var v4 = new Vector3 { x = -halfWinW, y = y, z = winZ1 };

                    AddQuad_NoShade(v1, v2, v3, v4, glassColor);
                }

                // -Y window above door
                {
                    float y = WallOut(-baseHalfSize, -1f);

                    float z0 = Math.Min(baseHeight - (winH + 2f), winZ0 + 3f);
                    float z1 = z0 + winH;

                    var v1 = new Vector3 { x = halfWinW, y = y, z = z0 };
                    var v2 = new Vector3 { x = -halfWinW, y = y, z = z0 };
                    var v3 = new Vector3 { x = -halfWinW, y = y, z = z1 };
                    var v4 = new Vector3 { x = halfWinW, y = y, z = z1 };

                    AddQuad_NoShade(v1, v2, v3, v4, glassColor);
                }
            }

            return tris;
        }

        // Shaft: tapered cylinder/frustum sitting on top of base.
        // Uses local center at shaft mid-height for correct hidden-face.
        public static List<ITriangleMeshWithColor>? TowerShaft()
        {
            int detailLevel = GetDetailLevelOrPreset(ShaftDetailOverride);
            int segments = GetSegmentsForPart("Shaft", detailLevel);

            float z0 = baseHeight;
            float z1 = baseHeight + shaftHeight;

            Vector3 shaftCenter = new Vector3 { x = 0, y = 0, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                segments,
                shaftBottomRadius,
                shaftTopRadius,
                z0,
                z1,
                shaftCenter,
                shaftColor,
                capBottom: false,
                capTop: false,
                shaded: ShadeRegularSurfaces);
        }

        // Head outer frame: wider frustum section.
        // Uses local center at head mid-height for correct hidden-face.
        public static List<ITriangleMeshWithColor>? TowerHeadFrame()
        {
            int detailLevel = GetDetailLevelOrPreset(HeadDetailOverride);
            int segments = GetSegmentsForPart("Head", detailLevel);

            float z0 = baseHeight + shaftHeight;
            float z1 = z0 + headHeight;

            Vector3 headCenter = new Vector3 { x = 0, y = 0, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                segments,
                headBottomRadius,
                headTopRadius,
                z0,
                z1,
                headCenter,
                headFrameColor,
                capBottom: false,
                capTop: false,
                shaded: ShadeRegularSurfaces);
        }

        // Head glass ring: inset frustum band.
        // Glass kept FLAT (no shading) for readability.
        public static List<ITriangleMeshWithColor>? TowerHeadGlass()
        {
            int detailLevel = GetDetailLevelOrPreset(HeadDetailOverride);
            int segments = GetSegmentsForPart("Head", detailLevel);

            float headBaseZ = baseHeight + shaftHeight;
            float z0 = headBaseZ + (headHeight - glassHeight) * 0.5f;
            float z1 = z0 + glassHeight;

            float r0 = Math.Max(1f, headBottomRadius - glassInset);
            float r1 = Math.Max(1f, headTopRadius - glassInset);

            Vector3 glassCenter = new Vector3 { x = 0, y = 0, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                segments,
                r0,
                r1,
                z0,
                z1,
                glassCenter,
                glassColor,
                capBottom: false,
                capTop: false,
                shaded: false);
        }

        // Roof cap: frustum to smaller radius with top cap.
        // Uses local center at roof mid-height for correct hidden-face.
        public static List<ITriangleMeshWithColor>? TowerRoof()
        {
            int detailLevel = GetDetailLevelOrPreset(HeadDetailOverride);
            int segments = GetSegmentsForPart("Head", detailLevel);

            float z0 = baseHeight + shaftHeight + headHeight;
            float z1 = z0 + roofHeight;

            float r0 = headTopRadius * 0.98f;
            float r1 = headTopRadius * 0.35f;

            Vector3 roofCenter = new Vector3 { x = 0, y = 0, z = (z0 + z1) * 0.5f };

            return CreateFrustum(
                segments,
                r0,
                r1,
                z0,
                z1,
                roofCenter,
                trimColor,
                capBottom: false,
                capTop: true,
                shaded: ShadeRegularSurfaces);
        }

        // Radar: mast box + dish disc.
        // Uses local centers for mast/tip/dish to keep outward normals correct.
        public static List<ITriangleMeshWithColor>? TowerRadar()
        {
            int detailLevel = GetDetailLevelOrPreset(RadarDetailOverride);
            int segments = GetSegmentsForPart("Radar", detailLevel);

            var tris = new List<ITriangleMeshWithColor>();

            float roofTopZ = baseHeight + shaftHeight + headHeight + roofHeight;

            // Mast (shaded)
            {
                var mastMin = new Vector3 { x = -mastHalfSize, y = -mastHalfSize, z = roofTopZ };
                var mastMax = new Vector3 { x = mastHalfSize, y = mastHalfSize, z = roofTopZ + mastHeight };

                Vector3 mastCenter = new Vector3 { x = 0, y = 0, z = (mastMin.z + mastMax.z) * 0.5f };
                tris.AddRange(CreateBox(mastMin, mastMax, mastCenter, radarColor, shaded: ShadeRegularSurfaces));
            }

            // Antenna tip (shaded - still looks fine)
            {
                float tipZ0 = roofTopZ + mastHeight;
                float tipZ1 = tipZ0 + 2.5f;

                var tipMin = new Vector3 { x = -1.2f, y = -1.2f, z = tipZ0 };
                var tipMax = new Vector3 { x = 1.2f, y = 1.2f, z = tipZ1 };

                Vector3 tipCenter = new Vector3 { x = 0, y = 0, z = (tipZ0 + tipZ1) * 0.5f };
                tris.AddRange(CreateBox(tipMin, tipMax, tipCenter, antennaTipColor, shaded: ShadeRegularSurfaces));
            }

            // Dish (shaded)
            {
                float dishZ0 = roofTopZ + mastHeight * 0.65f;
                float dishZ1 = dishZ0 + dishThickness;

                Vector3 dishCenter = new Vector3 { x = 0, y = 0, z = (dishZ0 + dishZ1) * 0.5f };

                tris.AddRange(CreateFrustum(
                    segments,
                    dishRadius,
                    dishRadius,
                    dishZ0,
                    dishZ1,
                    dishCenter,
                    radarColor,
                    capBottom: false,
                    capTop: true,
                    shaded: ShadeRegularSurfaces));
            }

            return tris;
        }

        // ----------------------------------------------------
        //  CRASH BOXES (8-point boxes)
        // ----------------------------------------------------

        public static List<List<IVector3>>? TowerCrashBoxes()
        {
            var boxes = new List<List<IVector3>>();

            // Base
            {
                var min = new Vector3 { x = -baseHalfSize, y = -baseHalfSize, z = 0f };
                var max = new Vector3 { x = baseHalfSize, y = baseHalfSize, z = baseHeight };
                boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(min, max));
            }

            // Shaft (rough bounding box)
            {
                float r = Math.Max(shaftBottomRadius, shaftTopRadius) + 2.0f;
                var min = new Vector3 { x = -r, y = -r, z = baseHeight };
                var max = new Vector3 { x = r, y = r, z = baseHeight + shaftHeight };
                boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(min, max));
            }

            // Head + roof (rough bounding box)
            {
                float r = Math.Max(headBottomRadius, headTopRadius) + 2.0f;
                float z0 = baseHeight + shaftHeight;
                float z1 = z0 + headHeight + roofHeight;
                var min = new Vector3 { x = -r, y = -r, z = z0 };
                var max = new Vector3 { x = r, y = r, z = z1 };
                boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(min, max));
            }

            // Radar (rough bounding box)
            {
                float roofTopZ = baseHeight + shaftHeight + headHeight + roofHeight;
                float r = dishRadius + 3f;
                var min = new Vector3 { x = -r, y = -r, z = roofTopZ + mastHeight * 0.4f };
                var max = new Vector3 { x = r, y = r, z = roofTopZ + mastHeight + 3.0f };
                boxes.Add(_3dObjectHelpers.GenerateCrashBoxCorners(min, max));
            }

            return boxes;
        }

        // ----------------------------------------------------
        //  GEOMETRY HELPERS (shaded vs flat)
        // ----------------------------------------------------

        private static List<ITriangleMeshWithColor> CreateBox(Vector3 min, Vector3 max, Vector3 center, string baseHexColor, bool shaded)
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

            // Face-specific shades (stable)
            AddQuadOutward(tris, p001, p101, p111, p011, center, shaded ? ShadeByIndex(baseHexColor, 0) : baseHexColor); // +Z
            AddQuadOutward(tris, p100, p000, p010, p110, center, shaded ? ShadeByIndex(baseHexColor, 1) : baseHexColor); // -Z
            AddQuadOutward(tris, p101, p100, p110, p111, center, shaded ? ShadeByIndex(baseHexColor, 2) : baseHexColor); // +X
            AddQuadOutward(tris, p000, p001, p011, p010, center, shaded ? ShadeByIndex(baseHexColor, 3) : baseHexColor); // -X
            AddQuadOutward(tris, p011, p111, p110, p010, center, shaded ? ShadeByIndex(baseHexColor, 4) : baseHexColor); // +Y
            AddQuadOutward(tris, p100, p101, p001, p000, center, shaded ? ShadeByIndex(baseHexColor, 5) : baseHexColor); // -Y

            return tris;
        }

        private static List<ITriangleMeshWithColor> CreateFrustum(
            int segments,
            float radiusBottom,
            float radiusTop,
            float zBottom,
            float zTop,
            Vector3 center,
            string baseHexColor,
            bool capBottom,
            bool capTop,
            bool shaded)
        {
            var tris = new List<ITriangleMeshWithColor>();

            var ringBottom = GenerateCirclePoints(segments, radiusBottom, zBottom);
            var ringTop = GenerateCirclePoints(segments, radiusTop, zTop);

            // Sides
            for (int i = 0; i < segments; i++)
            {
                int next = (i + 1) % segments;

                var b1 = ringBottom[i];
                var b2 = ringBottom[next];
                var t1 = ringTop[i];
                var t2 = ringTop[next];

                string c = shaded ? ShadeByIndex(baseHexColor, i) : baseHexColor;

                tris.Add(CreateTriangleOutward(b1, b2, t2, center, c));
                tris.Add(CreateTriangleOutward(b1, t2, t1, center, c));
            }

            // Bottom cap
            if (capBottom)
            {
                var cCenter = new Vector3 { x = 0, y = 0, z = zBottom };
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    string c = shaded ? ShadeByIndex(baseHexColor, 100 + i) : baseHexColor;
                    tris.Add(CreateTriangleOutward(cCenter, ringBottom[next], ringBottom[i], center, c));
                }
            }

            // Top cap
            if (capTop)
            {
                var cCenter = new Vector3 { x = 0, y = 0, z = zTop };
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    string c = shaded ? ShadeByIndex(baseHexColor, 200 + i) : baseHexColor;
                    tris.Add(CreateTriangleOutward(cCenter, ringTop[i], ringTop[next], center, c));
                }
            }

            return tris;
        }

        private static List<Vector3> GenerateCirclePoints(int segments, float radius, float z)
        {
            var points = new List<Vector3>(segments);
            float angleStep = (float)(2 * Math.PI / segments);

            for (int i = 0; i < segments; i++)
            {
                float angle = i * angleStep;
                float x = radius * (float)Math.Cos(angle);
                float y = radius * (float)Math.Sin(angle);

                points.Add(new Vector3 { x = x, y = y, z = z });
            }

            return points;
        }

        // ----------------------------------------------------
        //  SHADING HELPERS (stable, deterministic)
        // ----------------------------------------------------

        private static string ShadeByIndex(string baseHex, int index)
        {
            // 4-step stable pattern (no flicker):
            // dark -> slightly dark -> slightly light -> light
            float amt = (index & 3) switch
            {
                0 => -0.10f,
                1 => -0.04f,
                2 => +0.06f,
                _ => +0.10f
            };

            return ShadeHex(baseHex, amt);
        }

        // Slightly lightens/darkens a hex RGB color. amount: -1..+1 (typical -0.15..+0.15)
        private static string ShadeHex(string hexRgb, float amount)
        {
            if (string.IsNullOrWhiteSpace(hexRgb) || hexRgb.Length != 6)
                return hexRgb;

            int r = Convert.ToInt32(hexRgb.Substring(0, 2), 16);
            int g = Convert.ToInt32(hexRgb.Substring(2, 2), 16);
            int b = Convert.ToInt32(hexRgb.Substring(4, 2), 16);

            int Adjust(int c)
            {
                if (amount >= 0)
                    return ClampToByte((int)(c + (255 - c) * amount));
                return ClampToByte((int)(c * (1.0f + amount)));
            }

            r = Adjust(r);
            g = Adjust(g);
            b = Adjust(b);

            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static int ClampToByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return v;
        }

        // ----------------------------------------------------
        //  LOCAL GEOMETRY HELPERS (ensure consistent winding)
        // ----------------------------------------------------

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
            return new Vector3
            {
                x = a.x - b.x,
                y = a.y - b.y,
                z = a.z - b.z
            };
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

            float invLen = 1.0f / (float)Math.Sqrt(lenSq);
            return new Vector3
            {
                x = v.x * invLen,
                y = v.y * invLen,
                z = v.z * invLen
            };
        }

            }
        }
