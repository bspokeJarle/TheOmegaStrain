using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class RainEmitter
    {
        public static _3dObject CreateRainEmitter(ISurface? parentSurface)
        {
            return new _3dObject
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
                    new _3dObjectPart
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
