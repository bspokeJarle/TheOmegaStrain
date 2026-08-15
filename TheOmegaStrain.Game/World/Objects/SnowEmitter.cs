using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class SnowEmitter
    {
        public static OmegaObject3D CreateSnowEmitter(ISurface? parentSurface)
        {
            return new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "SnowEmitter",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 1500 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new SnowfallControls(),
                Particles = null,
                CrashBoxes = new List<List<IVector3>>(),
                CrashBoxesFollowRotation = false,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "SnowEmitter" },
                HasShadow = false,
                IsActive = true,
                ObjectParts = new List<I3dObjectPart>
                {
                    new OmegaObjectPart3D
                    {
                        PartName = "Snowflakes",
                        IsVisible = true,
                        Triangles = CreateSnowflakeBuffer()
                    }
                }
            };
        }

        private static List<ITriangleMeshWithColor> CreateSnowflakeBuffer()
        {
            var triangles = new List<ITriangleMeshWithColor>(SnowfallControls.TargetFlakeCount);
            for (int i = 0; i < SnowfallControls.TargetFlakeCount; i++)
            {
                triangles.Add(new TriangleMeshWithColor
                {
                    Color = "ffffff",
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
