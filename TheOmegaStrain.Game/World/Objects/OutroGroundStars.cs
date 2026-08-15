using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class OutroGroundStars
    {
        public const int StarCount = 34;

        private static readonly (float x, float y, float size)[] Stars =
        [
            (-650f, -410f, 5f), (-590f, -275f, 3f), (-540f, -350f, 4f), (-480f, -220f, 3f),
            (-420f, -465f, 4f), (-365f, -315f, 5f), (-310f, -240f, 3f), (-255f, -385f, 4f),
            (-190f, -285f, 3f), (-140f, -455f, 5f), (-85f, -330f, 3f), (-35f, -235f, 4f),
            (20f, -420f, 3f), (75f, -300f, 4f), (130f, -500f, 3f), (185f, -255f, 5f),
            (235f, -375f, 4f), (290f, -225f, 3f), (345f, -445f, 5f), (395f, -315f, 3f),
            (450f, -260f, 4f), (505f, -405f, 3f), (560f, -310f, 5f), (625f, -475f, 3f),
            (-700f, -190f, 4f), (-575f, -525f, 3f), (-235f, -520f, 3f), (5f, -545f, 4f),
            (250f, -535f, 3f), (650f, -245f, 4f), (-30f, -165f, 3f), (330f, -165f, 3f),
            (-390f, -160f, 3f), (570f, -165f, 3f)
        ];

        public static _3dObject CreateStarField()
        {
            var starField = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            starField.ObjectName = "OutroGroundStars";
            starField.WorldPosition = new Vector3();
            starField.ObjectOffsets = new Vector3();
            starField.Rotation = new Vector3();
            starField.CrashBoxes = new List<List<IVector3>>();
            starField.CrashBoxesFollowRotation = false;
            starField.CrashBoxDebugMode = false;
            starField.ImpactStatus = new ImpactStatus();
            starField.Movement = new OutroGroundStarsControls();
            starField.ZSortBias = -60f;

            starField.ObjectParts.Add(new _3dObjectPart
            {
                PartName = "OutroGroundStarField",
                Triangles = CreateStars(),
                IsVisible = true
            });

            return starField;
        }

        private static List<ITriangleMeshWithColor> CreateStars()
        {
            var tris = new List<ITriangleMeshWithColor>(StarCount * 4);
            for (int i = 0; i < Stars.Length; i++)
            {
                var (x, y, size) = Stars[i];
                string color = i % 3 == 0 ? "FFF8D8" : i % 3 == 1 ? "D8ECFF" : "FFFFFF";
                AddDiamond(tris, x, y, size, color);
            }

            return tris;
        }

        private static void AddDiamond(List<ITriangleMeshWithColor> tris, float x, float y, float size, string color)
        {
            var top = new Vector3(x, y - size, 0f);
            var right = new Vector3(x + size, y, 0f);
            var bottom = new Vector3(x, y + size, 0f);
            var left = new Vector3(x - size, y, 0f);
            var center = new Vector3(x, y, 0f);

            tris.Add(CreateTri(top, right, center, color));
            tris.Add(CreateTri(right, bottom, center, color));
            tris.Add(CreateTri(bottom, left, center, color));
            tris.Add(CreateTri(left, top, center, color));
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
