using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;

namespace TheOmegaStrain.Gameplay.Controls
{
    public class EarthObjectControls : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        private bool _initialized = false;
        private DateTime _startTime;

        // Rotation speed: slow majestic spin
        private const float SpinYDegreesPerSecond = 8f;
        private const float SpinZDegreesPerSecond = 2.8f;

        private float _rotY = 0f;
        private float _rotZ = 90f;

        private DateTime _lastFrame = DateTime.MinValue;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            if (theObject.ImpactStatus?.HasExploded == true) return theObject;

            if (!_initialized)
            {
                _initialized = true;
                _startTime = DateTime.Now;
                _lastFrame = DateTime.Now;

                if (theObject.Rotation != null)
                {
                    _rotY = theObject.Rotation.y;
                    _rotZ = theObject.Rotation.z;
                }
            }

            var now = DateTime.Now;
            float delta = (float)(now - _lastFrame).TotalSeconds;
            _lastFrame = now;
            if (delta <= 0f || delta > 0.5f) return theObject;

            _rotY += SpinYDegreesPerSecond * delta;
            _rotZ += SpinZDegreesPerSecond * delta;

            if (_rotY > 360f) _rotY -= 360f;
            if (_rotZ > 360f) _rotZ -= 360f;

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = WorldViewSetup.WorldPitchDegrees;
                theObject.Rotation.y = _rotY;
                theObject.Rotation.z = _rotZ;
            }

            theObject.ZSortBias = -300f;  // Earth always renders behind the Outro ship

            return theObject;
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }

        public void Dispose()
        {
            _initialized = false;
            _lastFrame = DateTime.MinValue;
        }
    }
}
