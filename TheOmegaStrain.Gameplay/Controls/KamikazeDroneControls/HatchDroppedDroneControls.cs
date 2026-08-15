using CommonUtilities.CommonGlobalState;
using Domain;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls.KamikazeDroneControls
{
    // A drone that was just dropped from the MotherShipLarge hatch.
    // For the first DropDurationSeconds it descends straight down without attacking.
    // After the drop phase it hands all logic to an inner KamikazeDroneControls instance
    // so the rest of the behaviour is identical to a normally-spawned kamikaze drone.
    public class HatchDroppedDroneControls : IObjectMovement
    {
        // -------------------------------------------------------
        //  Drop phase config
        // -------------------------------------------------------
        private const float DropDurationSeconds       = 1.0f;   // idle descent window per drone
        private const float DropDescentUnitsPerSecond = 180f;   // ObjectOffsets.y units/s (positive = downward on screen)

        // -------------------------------------------------------
        //  Interface
        // -------------------------------------------------------
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // -------------------------------------------------------
        //  State
        // -------------------------------------------------------
        private readonly KamikazeDroneControls _inner = new();
        private readonly float _initialDelaySeconds;   // stagger: drone stays frozen until this elapses
        private float _delayTimer    = 0f;
        private bool  _delayComplete = false;
        private bool  _dropComplete  = false;
        private float _dropTimer     = 0f;
        private DateTime _lastMoveTime = DateTime.MinValue;

        // Crash boxes are stashed during delay+drop and restored afterwards.
        private List<List<IVector3>>? _savedCrashBoxes;

        public HatchDroppedDroneControls(float initialDelaySeconds = 0f)
        {
            _initialDelaySeconds = initialDelaySeconds;
            // If there is no delay the drop phase starts immediately.
            _delayComplete = initialDelaySeconds <= 0f;
        }

        // -------------------------------------------------------
        //  MoveObject
        // -------------------------------------------------------
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (!_dropComplete)
            {
                var now = DateTime.Now;
                if (_lastMoveTime == DateTime.MinValue)
                    _lastMoveTime = now;

                float deltaSeconds = (float)(now - _lastMoveTime).TotalSeconds;
                _lastMoveTime = now;

                // Stash crash boxes on the very first frame so the drone is inert.
                if (_savedCrashBoxes == null && theObject.CrashBoxes != null && theObject.CrashBoxes.Count > 0)
                {
                    _savedCrashBoxes = theObject.CrashBoxes;
                    theObject.CrashBoxes = new List<List<IVector3>>();
                }

                // Configure audio as early as possible so the drone sound is ready.
                _inner.ConfigureAudio(audioPlayer, soundRegistry);

                // --- Initial delay: drone is frozen at its spawn position ---
                if (!_delayComplete)
                {
                    _delayTimer += deltaSeconds;
                    if (_delayTimer >= _initialDelaySeconds)
                        _delayComplete = true;
                    return theObject;
                }

                // --- Drop phase: descend straight down ---
                if (theObject.ObjectOffsets != null)
                    theObject.ObjectOffsets.y += DropDescentUnitsPerSecond * deltaSeconds;

                _dropTimer += deltaSeconds;

                if (_dropTimer >= DropDurationSeconds)
                {
                    // Restore crash boxes and hand off to the normal drone controls.
                    if (_savedCrashBoxes != null)
                    {
                        theObject.CrashBoxes = _savedCrashBoxes;
                        _savedCrashBoxes = null;
                    }
                    _dropComplete = true;
                    _inner.StartHuntDateTime = DateTime.Now;
                }

                return theObject;
            }

            // Drop phase complete — fully delegate to KamikazeDroneControls.
            _inner.StartCoordinates = StartCoordinates;
            _inner.GuideCoordinates = GuideCoordinates;
            _inner.ParentObject     = ParentObject;
            _inner.Physics          = Physics;
            return _inner.MoveObject(theObject, audioPlayer, soundRegistry);
        }

        // -------------------------------------------------------
        //  Interface pass-through
        // -------------------------------------------------------
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
            => _inner.SetParticleGuideCoordinates(StartCoord, GuideCoord);

        public void ReleaseParticles(I3dObject theObject)
            => _inner.ReleaseParticles(theObject);

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
            => _inner.SetRearEngineGuideCoordinates(StartCoord, GuideCoord);

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
            => _inner.SetWeaponGuideCoordinates(StartCoord, GuideCoord);

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
            => _inner.ConfigureAudio(audioPlayer, soundRegistry);

        public void Dispose()
        {
            _inner.Dispose();
            _delayTimer    = 0f;
            _delayComplete = _initialDelaySeconds <= 0f;
            _dropComplete  = false;
            _dropTimer     = 0f;
            _lastMoveTime  = DateTime.MinValue;
            _savedCrashBoxes = null;
        }
    }
}
