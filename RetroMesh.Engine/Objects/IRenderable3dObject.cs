using System.Collections.Generic;

namespace RetroMesh.Engine
{
    public interface IRenderable3dObject
    {
        int ObjectId { get; set; }
        string ObjectName { get; set; }
        int? RotationOffsetX { get; set; }
        int? RotationOffsetY { get; set; }
        int? RotationOffsetZ { get; set; }
        IVector3? WorldPosition { get; set; }
        List<I3dObjectPart> ObjectParts { get; set; }
        IVector3? ObjectOffsets { get; set; }
        IVector3? Rotation { get; set; }
        List<List<IVector3>> CrashBoxes { get; set; }
        List<string?>? CrashBoxNames { get; set; }
        bool CrashBoxesFollowRotation { get; set; }
        IVector3? CalculatedCrashOffset { get; set; }
        bool IsOnScreen { get; set; }
        bool HasShadow { get; set; }
        IVector3? ShadowOffset { get; set; }
        bool UseSurfaceFootprintPivot { get; set; }
        bool IsActive { get; set; }
        float ZSortBias { get; set; }
    }
}
