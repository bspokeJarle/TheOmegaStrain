using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public class Engine3dObject : IRenderable3dObject
    {
        public required int ObjectId { get; set; }
        public List<I3dObjectPart> ObjectParts { get; set; } = new();
        public int? RotationOffsetY { get; set; }
        public int? RotationOffsetX { get; set; }
        public int? RotationOffsetZ { get; set; }
        public IVector3? WorldPosition { get; set; }
        public IVector3? Rotation { get; set; }
        public IVector3? ObjectOffsets { get; set; }
        public List<List<IVector3>> CrashBoxes { get; set; } = null!;
        public List<string?>? CrashBoxNames { get; set; }
        public bool CrashBoxesFollowRotation { get; set; } = true;
        public string ObjectName { get; set; } = null!;
        public IVector3? CalculatedCrashOffset { get; set; }
        public bool IsOnScreen { get; set; } = false;
        public bool HasShadow { get; set; } = false;
        public IVector3? ShadowOffset { get; set; }
        public bool UseSurfaceFootprintPivot { get; set; } = false;
        public bool IsActive { get; set; } = true;
        public float ZSortBias { get; set; } = 0f;
    }
}
