using Domain;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class SealControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public SealControls()
        {
        }

        public SealControls(float jumpHorizontalSpan)
        {
        }

        public SealControls(float jumpHorizontalSpan, float minPathOffsetX, float maxPathOffsetX, int initialJumpDirection = -1)
        {
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            return theObject;
        }

        public void ReleaseParticles()
        {
        }

        public void ReleaseParticles(I3dObject theObject)
        {
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
        }

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
        }
    }
}
