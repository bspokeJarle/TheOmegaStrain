using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class RainEmitter
    {
        public static OmegaObject3D CreateRainEmitter(ISurface? parentSurface)
        {
            return new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "RainEmitter",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 1500 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new RainfallControls(),
                Particles = null,
                CrashBoxes = new List<List<IVector3>>(),
                CrashBoxesFollowRotation = false,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "RainEmitter" },
                HasShadow = false,
                IsActive = true,
                ObjectParts = new List<I3dObjectPart>
                {
                    new OmegaObjectPart3D
                    {
                        PartName = "Raindrops",
                        IsVisible = true,
                        Triangles = CreateRaindropBuffer()
                    }
                }
            };
        }

        private static List<ITriangleMeshWithColor> CreateRaindropBuffer()
        {
            var triangles = new List<ITriangleMeshWithColor>(RainfallControls.TargetDropCount);
            for (int i = 0; i < RainfallControls.TargetDropCount; i++)
            {
                triangles.Add(new TriangleMeshWithColor
                {
                    Color = "BDEAFF",
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
