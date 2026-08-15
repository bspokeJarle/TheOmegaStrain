using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class LeafEmitter
    {
        public static _3dObject CreateLeafEmitter(ISurface? parentSurface)
        {
            return new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "LeafEmitter",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 1500 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new LeafDriftControls(),
                Particles = null,
                CrashBoxes = new List<List<IVector3>>(),
                CrashBoxesFollowRotation = false,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "LeafEmitter" },
                HasShadow = false,
                IsActive = true,
                ObjectParts = new List<I3dObjectPart>
                {
                    new _3dObjectPart
                    {
                        PartName = "Leaves",
                        IsVisible = true,
                        Triangles = CreateLeafBuffer()
                    }
                }
            };
        }

        private static List<ITriangleMeshWithColor> CreateLeafBuffer()
        {
            var triangles = new List<ITriangleMeshWithColor>(LeafDriftControls.TargetLeafCount);
            for (int i = 0; i < LeafDriftControls.TargetLeafCount; i++)
            {
                triangles.Add(new TriangleMeshWithColor
                {
                    Color = LeafTree.LeafColors[i % LeafTree.LeafColors.Length],
                    noHidden = true,
                    angle = 1f,
                    vert1 = new Vector3(),
                    vert2 = new Vector3(),
                    vert3 = new Vector3()
                });
            }

            return triangles;
        }
    }
}
