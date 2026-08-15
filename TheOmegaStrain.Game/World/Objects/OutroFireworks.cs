using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.World.Objects
{
    public static class OutroFireworks
    {
        public const string ObjectName = "OutroFireworks";
        public const string ParticlePartName = "OutroFireworksParticles";

        public static OmegaObject3D CreateFireworks()
        {
            var fireworks = new OmegaObject3D { ObjectId = GameState.ObjectIdCounter++ };
            fireworks.ObjectName = ObjectName;
            fireworks.WorldPosition = new Vector3();
            fireworks.ObjectOffsets = new Vector3();
            fireworks.Rotation = new Vector3();
            fireworks.SurfaceBasedId = null;
            fireworks.CrashBoxes = new List<List<IVector3>>();
            fireworks.CrashBoxesFollowRotation = false;
            fireworks.CrashBoxDebugMode = false;
            fireworks.ImpactStatus = new ImpactStatus();
            fireworks.Movement = new OutroFireworksControls();
            fireworks.ZSortBias = 520f;

            fireworks.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = ParticlePartName,
                Triangles = new List<ITriangleMeshWithColor>(),
                IsVisible = true
            });

            return fireworks;
        }
    }
}
