using static Domain._3dSpecificsImplementations;

namespace Domain
{
    public interface IObjectMovement
    {
        I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry);
        void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry);
        ITriangleMeshWithColor? StartCoordinates { get; set; }
        ITriangleMeshWithColor? GuideCoordinates { get; set; }
        void ReleaseParticles(I3dObject theObject);
        void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord);
        void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord);
        void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord);
        IPhysics Physics { get; set; }
        void Dispose();
    }
}
