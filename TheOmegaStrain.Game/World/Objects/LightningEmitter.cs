using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class LightningEmitter
    {
        public static OmegaObject3D CreateLightningEmitter(ISurface? parentSurface)
        {
            return new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "LightningEmitter",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 1500 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                Movement = new LightningControls(),
                Particles = null,
                CrashBoxes = new List<List<IVector3>>(),
                CrashBoxesFollowRotation = false,
                CrashBoxDebugMode = false,
                ImpactStatus = new ImpactStatus { ObjectName = "LightningEmitter" },
                HasShadow = false,
                IsActive = true,
                ObjectParts = new List<I3dObjectPart>
                {
                    new OmegaObjectPart3D
                    {
                        PartName = "LightningBolts",
                        IsVisible = true,
                        Triangles = CreateLightningBuffer()
                    }
                }
            };
        }

        private static List<ITriangleMeshWithColor> CreateLightningBuffer()
        {
            var triangles = new List<ITriangleMeshWithColor>(LightningControls.TargetTriangleCount);
            for (int i = 0; i < LightningControls.TargetTriangleCount; i++)
            {
                triangles.Add(new TriangleMeshWithColor
                {
                    Color = "000000",
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
