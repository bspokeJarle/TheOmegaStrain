using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class SandEmitter
    {
        public static OmegaObject3D CreateSandEmitter(ISurface? parentSurface)
        {
            return new OmegaObject3D
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
                    new OmegaObjectPart3D
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
