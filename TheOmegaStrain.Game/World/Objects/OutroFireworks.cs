using CommonUtilities.CommonGlobalState;
using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class OutroFireworks
    {
        public const string ObjectName = "OutroFireworks";
        public const string ParticlePartName = "OutroFireworksParticles";

        public static _3dObject CreateFireworks()
        {
            var fireworks = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
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

            fireworks.ObjectParts.Add(new _3dObjectPart
            {
                PartName = ParticlePartName,
                Triangles = new List<ITriangleMeshWithColor>(),
                IsVisible = true
            });

            return fireworks;
        }
    }
}
