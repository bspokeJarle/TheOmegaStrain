using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Gameplay.Controls;

// A cloud has a fixed world X/Z position. Only its render height follows the
// same surface/camera offset used by other airborne objects.
public sealed class CloudControls : IObjectMovement
{
    private float? _initialOffsetY;

    public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
    public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
    public IPhysics Physics { get; set; } = new Physics.Physics();

    public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
    {
        _initialOffsetY ??= theObject.ObjectOffsets.y;
        theObject.ObjectOffsets = SurfacePositionSyncHelpers.GetSurfaceSyncedObjectOffsets(
            theObject, _initialOffsetY.Value);
        return theObject;
    }

    public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
    public void ReleaseParticles(I3dObject theObject) { }
    public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture start, ITriangleMeshWithColorAndTexture guide) { }
    public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture start, ITriangleMeshWithColorAndTexture guide) { }
    public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture start, ITriangleMeshWithColorAndTexture guide) { }
    public void Dispose() { }
}
