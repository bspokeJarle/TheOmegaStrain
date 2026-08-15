using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class OutroLandingBanner
    {
        public const int SegmentCount = 8;
        public const string Line1 = "Welcome back";
        public const string Line2 = "Glad you did not die!";

        private const float BannerWidth = 420f;
        private const float BannerHeight = 110f;
        private const float BannerTop = -155f;
        private const float PoleHeight = 170f;
        private const float PoleWidth = 12f;
        private const float BannerZ = 8f;
        private const float TextZ = 14f;
        private const string BannerColor = "F2E7B8";
        private const string BannerShadeColor = "D8C982";
        private const string PoleColor = "5A3518";
        private const string TextColor = "15331F";

        public static _3dObject CreateBanner(ISurface parentSurface)
        {
            var banner = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            banner.ObjectName = "OutroLandingBanner";
            banner.ParentSurface = parentSurface;
            banner.ObjectOffsets = new Vector3();
            banner.Rotation = new Vector3();
            banner.WorldPosition = new Vector3();
            banner.CrashBoxes = new List<List<IVector3>>();
            banner.CrashBoxesFollowRotation = false;
            banner.CrashBoxDebugMode = false;
            banner.ImpactStatus = new ImpactStatus();
            banner.HasShadow = false;
            banner.Movement = new OutroLandingBannerControls();

            AddPart(banner, "BannerPoleLeft", CreatePole(-BannerWidth / 2f - PoleWidth, 0f));
            AddPart(banner, "BannerPoleRight", CreatePole(BannerWidth / 2f, 0f));

            var segmentTriangles = new List<ITriangleMeshWithColor>[SegmentCount];
            for (int i = 0; i < SegmentCount; i++)
            {
                segmentTriangles[i] = new List<ITriangleMeshWithColor>();
                AddBannerSegment(segmentTriangles[i], i);
            }

            AddText(segmentTriangles, Line1, y: BannerTop + 24f, glyphHeight: 19f);
            AddText(segmentTriangles, Line2, y: BannerTop + 62f, glyphHeight: 15f);

            for (int i = 0; i < SegmentCount; i++)
            {
                AddPart(banner, $"BannerSegment_{i + 1:00}", segmentTriangles[i]);
            }

            return banner;
        }

        private static void AddBannerSegment(List<ITriangleMeshWithColor> triangles, int segmentIndex)
        {
            float segmentWidth = BannerWidth / SegmentCount;
            float left = (-BannerWidth / 2f) + (segmentIndex * segmentWidth);
            float right = left + segmentWidth;
            string color = segmentIndex % 2 == 0 ? BannerColor : BannerShadeColor;

            AddRect(triangles, left, BannerTop, right, BannerTop + BannerHeight, BannerZ, color);
        }

        private static List<ITriangleMeshWithColor> CreatePole(float x, float y)
        {
            var triangles = new List<ITriangleMeshWithColor>();
            AddRect(triangles, x, y - PoleHeight, x + PoleWidth, y, BannerZ - 2f, PoleColor);
            AddRect(triangles, x - 8f, y - PoleHeight - 5f, x + PoleWidth + 8f, y - PoleHeight + 7f, BannerZ - 1f, PoleColor);
            return triangles;
        }

        private static void AddText(List<ITriangleMeshWithColor>[] segmentTriangles, string text, float y, float glyphHeight)
        {
            string upper = text.ToUpperInvariant();
            const int glyphWidthUnits = 5;
            const int glyphHeightUnits = 7;
            float cell = glyphHeight / glyphHeightUnits;
            float glyphWidth = glyphWidthUnits * cell;
            float glyphSpacing = cell;
            float spaceWidth = glyphWidth * 0.7f;
            float totalWidth = MeasureTextWidth(upper, glyphWidth, glyphSpacing, spaceWidth);
            float x = -totalWidth / 2f;

            for (int i = 0; i < upper.Length; i++)
            {
                char c = upper[i];
                if (c == ' ')
                {
                    x += spaceWidth;
                    continue;
                }

                var pattern = GetGlyph(c);
                if (pattern == null)
                {
                    x += glyphWidth + glyphSpacing;
                    continue;
                }

                AddGlyph(segmentTriangles, pattern, x, y, cell);
                x += glyphWidth + glyphSpacing;
            }
        }

        private static float MeasureTextWidth(string text, float glyphWidth, float glyphSpacing, float spaceWidth)
        {
            float width = 0f;
            for (int i = 0; i < text.Length; i++)
            {
                width += text[i] == ' ' ? spaceWidth : glyphWidth;
                if (i < text.Length - 1)
                    width += glyphSpacing;
            }

            return width;
        }

        private static void AddGlyph(List<ITriangleMeshWithColor>[] segmentTriangles, string[] pattern, float x, float y, float cell)
        {
            for (int row = 0; row < pattern.Length; row++)
            {
                string line = pattern[row];
                for (int col = 0; col < line.Length; col++)
                {
                    if (line[col] != '#')
                        continue;

                    float left = x + (col * cell);
                    float top = y + (row * cell);
                    float right = left + (cell * 0.82f);
                    float bottom = top + (cell * 0.82f);
                    int segmentIndex = GetSegmentIndex((left + right) * 0.5f);
                    AddRect(segmentTriangles[segmentIndex], left, top, right, bottom, TextZ, TextColor);
                }
            }
        }

        private static int GetSegmentIndex(float x)
        {
            float normalized = (x + (BannerWidth / 2f)) / BannerWidth;
            return Math.Clamp((int)(normalized * SegmentCount), 0, SegmentCount - 1);
        }

        private static string[]? GetGlyph(char c)
        {
            return c switch
            {
                'A' => [" ### ", "#   #", "#   #", "#####", "#   #", "#   #", "#   #"],
                'B' => ["#### ", "#   #", "#   #", "#### ", "#   #", "#   #", "#### "],
                'C' => [" ####", "#    ", "#    ", "#    ", "#    ", "#    ", " ####"],
                'D' => ["#### ", "#   #", "#   #", "#   #", "#   #", "#   #", "#### "],
                'E' => ["#####", "#    ", "#    ", "#### ", "#    ", "#    ", "#####"],
                'G' => [" ####", "#    ", "#    ", "#  ##", "#   #", "#   #", " ####"],
                'I' => ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "#####"],
                'K' => ["#   #", "#  # ", "# #  ", "##   ", "# #  ", "#  # ", "#   #"],
                'L' => ["#    ", "#    ", "#    ", "#    ", "#    ", "#    ", "#####"],
                'M' => ["#   #", "## ##", "# # #", "#   #", "#   #", "#   #", "#   #"],
                'N' => ["#   #", "##  #", "# # #", "#  ##", "#   #", "#   #", "#   #"],
                'O' => [" ### ", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
                'T' => ["#####", "  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "  #  "],
                'U' => ["#   #", "#   #", "#   #", "#   #", "#   #", "#   #", " ### "],
                'W' => ["#   #", "#   #", "#   #", "# # #", "# # #", "## ##", "#   #"],
                'Y' => ["#   #", "#   #", " # # ", "  #  ", "  #  ", "  #  ", "  #  "],
                '!' => ["  #  ", "  #  ", "  #  ", "  #  ", "  #  ", "     ", "  #  "],
                _ => null
            };
        }

        private static void AddPart(_3dObject obj, string partName, List<ITriangleMeshWithColor> triangles)
        {
            obj.ObjectParts.Add(new _3dObjectPart
            {
                PartName = partName,
                Triangles = triangles,
                IsVisible = true
            });
        }

        private static void AddRect(List<ITriangleMeshWithColor> triangles, float left, float top, float right, float bottom, float z, string color)
        {
            var topLeft = new Vector3(left, top, z);
            var topRight = new Vector3(right, top, z);
            var bottomRight = new Vector3(right, bottom, z);
            var bottomLeft = new Vector3(left, bottom, z);

            triangles.Add(CreateTri(topLeft, topRight, bottomRight, color));
            triangles.Add(CreateTri(topLeft, bottomRight, bottomLeft, color));
        }

        private static TriangleMeshWithColor CreateTri(Vector3 a, Vector3 b, Vector3 c, string color)
        {
            return new TriangleMeshWithColor
            {
                Color = color,
                vert1 = a,
                vert2 = b,
                vert3 = c,
                normal1 = new Vector3 { z = 1 },
                normal2 = new Vector3 { z = 1 },
                normal3 = new Vector3 { z = 1 },
                noHidden = true
            };
        }
    }
}
