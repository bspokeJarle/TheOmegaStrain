using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class SandEmitter
    {
        public static _3dObject CreateSandEmitter(ISurface? parentSurface)
        {
            return new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SandEmitter",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 1500 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new SandDriftControls(),
                Particles = null,
                CrashBoxes = new List<List<IVector3>>(),
                CrashBoxesFollowRotation = false,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "SandEmitter" },
                HasShadow = false,
                IsActive = true,
                ObjectParts = new List<I3dObjectPart>
                {
                    new _3dObjectPart
                    {
                        PartName = "SandDust",
                        IsVisible = true,
                        Triangles = CreateDustBuffer()
                    }
                }
            };
        }

        private static List<ITriangleMeshWithColor> CreateDustBuffer()
        {
            var triangles = new List<ITriangleMeshWithColor>(SandDriftControls.TargetDustCount);
            for (int i = 0; i < SandDriftControls.TargetDustCount; i++)
            {
                triangles.Add(new TriangleMeshWithColor
                {
                    Color = "D8B66A",
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
