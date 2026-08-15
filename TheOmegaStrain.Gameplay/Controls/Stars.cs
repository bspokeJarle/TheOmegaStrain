using CommonUtilities.CommonGlobalState;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class StarsControl : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; }

        // Optional: could be used for slow spin later
        private float _yRotation = 0f;
        private readonly float _xRotation = 0f;
        private readonly float _zRotation = 0f;

        private bool _syncInitialized = false;
        private float _syncY = 0f;

        // Factor to stay in sync with surface movement (similar to SeederControls)
        // Tweak as desired – lower value = calmer star movement in Y
        private float _syncFactor = 2.5f;

        private readonly bool _enableLogging = false;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            // If you want some rotation later:
            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = _xRotation;
                theObject.Rotation.y = _yRotation;
                theObject.Rotation.z = _zRotation;
            }

            SyncMovement(theObject);
            return theObject;
        }

        private void SyncMovement(I3dObject theObject)
        {
            if (theObject.ParentSurface == null)
                return;

            var surfacePos = GameState.SurfaceState.GlobalMapPosition;

            if (!_syncInitialized)
            {
                _syncInitialized = true;
                _syncY = theObject.ObjectOffsets.y; // base Y-offset for this star
            }

            theObject.ObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets.x,                           // keep X
                y = (surfacePos.y * _syncFactor) + _syncY,               // only sync Y with the surface
                z = theObject.ObjectOffsets.z                            // keep Z
            };

            if (Logger.ShouldLog(_enableLogging))
            {
                Logger.Log(
                    $"[StarsControl] Star Y={theObject.ObjectOffsets.y:0.0}, " +
                    $"SurfaceY={surfacePos.y:0.0}, syncFactor={_syncFactor:0.00}, baseY={_syncY:0.0}"
                );
            }
        }

        public void ReleaseParticles()
        {
            // Stars do not release particles (for now).
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            // No weapon logic needed for stars.
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            // Stars are silent for now.
        }

        public void Dispose()
        {
            // Nothing to dispose yet.
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new System.NotImplementedException();
        }
    }
}
