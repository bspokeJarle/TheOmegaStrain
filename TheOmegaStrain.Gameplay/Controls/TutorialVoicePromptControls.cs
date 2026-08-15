using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Audio.Services;
using System;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class TutorialVoicePromptControls : IObjectMovement
    {
        private enum TutorialPromptStage
        {
            Intro,
            Thrust,
            LeftPad,
            Weapons,
            SeederOneDown,
            Powerup,
            DecoySelect,
            DroneInbound,
            DecoyHint,
            DroneDestroyed,
            Complete,
            Done
        }

        private const double IntroSpeechSeconds = 5.8;
        private const double DelayAfterIntroSeconds = 3.0;
        private const double DelayAfterPowerupSeconds = 3.0;
        private const double DecoyHintDelaySeconds = 6.0;
        private const double CompleteDelaySeconds = 3.0;
        private const double InstructionOverlayAutoCloseSpeechMultiplier = 2.0;
        private const float PadExitDistance = 100f;
        private const float WeaponsHintDistance = 1600f;

        private readonly Func<DateTime> _now;
        private readonly ShipAiVoiceService _voiceService;

        private TutorialPromptStage _stage = TutorialPromptStage.Intro;
        private DateTime _introSpokenAt = DateTime.MinValue;
        private DateTime _powerupSpokenAt = DateTime.MinValue;
        private DateTime _droneActivatedAt = DateTime.MinValue;
        private DateTime _droneDestroyedAt = DateTime.MinValue;
        private Vector3? _padWorldPosition;
        private int? _initialSeederCount;
        private int? _initialPowerupsCollected;
        private bool _droneWasActive;

        public TutorialVoicePromptControls()
            : this(() => DateTime.Now, ShipAiVoiceService.Shared)
        {
        }

        public TutorialVoicePromptControls(Func<DateTime> now, ShipAiVoiceService? voiceService = null)
        {
            _now = now;
            _voiceService = voiceService ?? ShipAiVoiceService.Shared;
        }

        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            _voiceService.Update(audioPlayer);
            EnsureInitialState();

            if (GameState.TutorialState.InstructionOverlayPauseActive)
                return theObject;

            switch (_stage)
            {
                case TutorialPromptStage.Intro:
                    if (TrySpeak(ShipAiVoiceCue.TutorialIntro, audioPlayer, soundRegistry))
                    {
                        _introSpokenAt = _now();
                        _stage = TutorialPromptStage.Thrust;
                    }
                    break;

                case TutorialPromptStage.Thrust:
                    if ((_now() - _introSpokenAt).TotalSeconds >= IntroSpeechSeconds + DelayAfterIntroSeconds &&
                        TrySpeak(ShipAiVoiceCue.TutorialThrust, audioPlayer, soundRegistry))
                    {
                        _stage = TutorialPromptStage.LeftPad;
                    }
                    break;

                case TutorialPromptStage.LeftPad:
                    if (HasLeftPad() &&
                        TrySpeak(ShipAiVoiceCue.TutorialCheckpoint, audioPlayer, soundRegistry))
                    {
                        _stage = TutorialPromptStage.Weapons;
                    }
                    break;

                case TutorialPromptStage.Weapons:
                    if (IsNearAliveSeeder(WeaponsHintDistance) &&
                        TrySpeak(ShipAiVoiceCue.TutorialWeapons, audioPlayer, soundRegistry))
                    {
                        _stage = TutorialPromptStage.SeederOneDown;
                    }
                    break;

                case TutorialPromptStage.SeederOneDown:
                    if (HasKilledSeeder() &&
                        TrySpeak(ShipAiVoiceCue.TutorialSeederOneDown, audioPlayer, soundRegistry))
                    {
                        _stage = TutorialPromptStage.Powerup;
                    }
                    break;

                case TutorialPromptStage.Powerup:
                    if (HasActivePowerUp() &&
                        TrySpeak(ShipAiVoiceCue.TutorialPowerup, audioPlayer, soundRegistry))
                    {
                        _powerupSpokenAt = _now();
                        _stage = TutorialPromptStage.DecoySelect;
                    }
                    break;

                case TutorialPromptStage.DecoySelect:
                    if (_initialPowerupsCollected.HasValue &&
                        GameState.GamePlayState.PowerUpsCollected > _initialPowerupsCollected.Value &&
                        (_now() - _powerupSpokenAt).TotalSeconds >= DelayAfterPowerupSeconds &&
                        TrySpeak(ShipAiVoiceCue.TutorialDecoySelect, audioPlayer, soundRegistry))
                    {
                        GameState.TutorialState.DecoySelectCueSpoken = true;
                        _stage = TutorialPromptStage.DroneInbound;
                    }
                    break;

                case TutorialPromptStage.DroneInbound:
                    if (HasActiveDrone() &&
                        TrySpeak(ShipAiVoiceCue.TutorialDroneInbound, audioPlayer, soundRegistry))
                    {
                        _droneActivatedAt = _now();
                        _droneWasActive = true;
                        _stage = TutorialPromptStage.DecoyHint;
                    }
                    break;

                case TutorialPromptStage.DecoyHint:
                    if (HasDroneBeenDestroyed())
                    {
                        _stage = TutorialPromptStage.DroneDestroyed;
                        break;
                    }

                    if (HasActiveDecoy())
                    {
                        _stage = TutorialPromptStage.DroneDestroyed;
                        break;
                    }

                    if ((_now() - _droneActivatedAt).TotalSeconds >= DecoyHintDelaySeconds &&
                        TrySpeak(ShipAiVoiceCue.TutorialDecoyHint, audioPlayer, soundRegistry))
                    {
                        _stage = TutorialPromptStage.DroneDestroyed;
                    }
                    break;

                case TutorialPromptStage.DroneDestroyed:
                    if (HasDroneBeenDestroyed() &&
                        TrySpeak(ShipAiVoiceCue.TutorialDroneDestroyed, audioPlayer, soundRegistry))
                    {
                        _droneDestroyedAt = _now();
                        _stage = TutorialPromptStage.Complete;
                    }
                    break;

                case TutorialPromptStage.Complete:
                    if ((_now() - _droneDestroyedAt).TotalSeconds >= CompleteDelaySeconds &&
                        TrySpeak(ShipAiVoiceCue.TutorialComplete, audioPlayer, soundRegistry))
                    {
                        GameState.TutorialState.CompleteCueSpoken = true;
                        _stage = TutorialPromptStage.Done;
                    }
                    break;
            }

            return theObject;
        }

        private bool TrySpeak(ShipAiVoiceCue cue, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (!_voiceService.TrySpeak(cue, audioPlayer, soundRegistry))
                return false;

            ShowInstructionOverlayIfNeeded(cue);
            return true;
        }

        private static void ShowInstructionOverlayIfNeeded(ShipAiVoiceCue cue)
        {
            if (!TryGetInstructionOverlay(cue, out var title, out var body))
                return;

            var overlay = GameState.ScreenOverlayState;
            overlay.ResetToDefaults();
            overlay.Type = ScreenOverlayType.Tutorial;
            overlay.Anchor = ScreenOverlayAnchor.Center;
            overlay.IsModal = true;
            overlay.Header = "ASTERION // HAL-E TRAINING";
            overlay.Title = title;
            overlay.Body = body;
            overlay.Footer = "HAL-E SPEAKING - ESC TO SKIP";
            overlay.DimStrength = 0.62f;
            overlay.PanelFillStrength = 0.78f;
            overlay.BorderStrength = 0.92f;
            overlay.PanelWidthRatio = 0.68f;
            overlay.PanelHeightRatio = 0.34f;
            overlay.PanelYOffsetRatio = 0f;
            overlay.CenterText = false;
            overlay.AutoHide = false;
            overlay.ShowDebugOverlay = false;
            overlay.ShowOverlay = true;

            double autoCloseSeconds = TutorialRuntimeState.InstructionOverlayMinimumSeconds;
            if (ShipAiVoiceService.TryGetEstimatedSpeechSeconds(cue, out double speechSeconds))
                autoCloseSeconds = Math.Max(autoCloseSeconds, speechSeconds * InstructionOverlayAutoCloseSpeechMultiplier);

            GameState.TutorialState.ShowInstructionOverlay(cue.ToString(), DateTime.UtcNow, autoCloseSeconds);
        }

        private static bool TryGetInstructionOverlay(ShipAiVoiceCue cue, out string title, out string body)
        {
            switch (cue)
            {
                case ShipAiVoiceCue.TutorialIntro:
                    title = "HAL-E ONLINE";
                    body =
                        "Training mode is paused while HAL-E explains the next step.\n\n" +
                        "Clear the infected seeders, collect the powerup, then use the decoy system against the drone.";
                    return true;

                case ShipAiVoiceCue.TutorialThrust:
                    title = "THRUST AND CONTROL";
                    body =
                        "Use SPACE or right mouse for thrust.\n\n" +
                        "LEFT and RIGHT turn the ship. UP and DOWN control pitch. Mouse movement can steer too.\n\n" +
                        "The guidance arrow below the HUD points toward the closest seeder or objective. Keep it roughly ahead of you when you navigate.";
                    return true;

                case ShipAiVoiceCue.TutorialWeapons:
                    title = "WEAPONS";
                    body =
                        "Use RIGHT SHIFT or left mouse to fire.\n\n" +
                        "Approach seeders from the front and line up as straight as you can before firing. The aiming helper works best when your attack angle is clean.\n\n" +
                        "Keep distance after a kill. Exploding objects can throw debris that damages your ship.\n\n" +
                        "Use 1, 2 and 3 to switch available systems.";
                    return true;

                case ShipAiVoiceCue.TutorialDecoySelect:
                    title = "SELECT DECOY";
                    body =
                        "Press 2 now to select the decoy system.\n\n" +
                        "Use RIGHT SHIFT or left mouse to deploy the decoy before the drone engages.\n\n" +
                        "A decoy can pull the drone away long enough for you to regain control and counterattack.";
                    return true;

                case ShipAiVoiceCue.TutorialPowerup:
                    title = "POWERUP DETECTED";
                    body =
                        "A PowerUp has dropped from the seeder.\n\n" +
                        "Fly through it to collect it. In training, this temporarily unlocks the decoy system without changing your real mission progress.";
                    return true;

                case ShipAiVoiceCue.TutorialComplete:
                    title = "TRAINING COMPLETE";
                    body =
                        "You have cleared the training sequence.\n\n" +
                        "The main mission uses the same systems, but planets become more aggressive as infection pressure rises.";
                    return true;

                default:
                    title = "";
                    body = "";
                    return false;
            }
        }

        private void EnsureInitialState()
        {
            _padWorldPosition ??= GetShipWorldPosition();
            _initialSeederCount ??= CountAliveSeeders();
            _initialPowerupsCollected ??= GameState.GamePlayState.PowerUpsCollected;
        }

        private bool HasLeftPad()
        {
            if (_padWorldPosition == null)
                return false;

            return DistanceSquaredXZ(GetShipWorldPosition(), _padWorldPosition) >= PadExitDistance * PadExitDistance;
        }

        private static bool IsNearAliveSeeder(float distance)
        {
            var ship = GetShipWorldPosition();
            float distanceSquared = distance * distance;
            return GameState.SurfaceState.AiObjects.Any(obj =>
                IsAliveSeeder(obj) &&
                obj.WorldPosition != null &&
                DistanceSquaredXZ(ship, obj.WorldPosition) <= distanceSquared);
        }

        private bool HasKilledSeeder()
        {
            if (_initialSeederCount == null)
                return false;

            return CountAliveSeeders() < _initialSeederCount.Value;
        }

        private static bool HasActiveDrone() =>
            GameState.SurfaceState.AiObjects.Any(IsActiveDrone);

        private bool HasDroneBeenDestroyed()
        {
            if (!_droneWasActive)
                _droneWasActive = HasActiveDrone();

            return _droneWasActive && !HasActiveDrone();
        }

        private static bool HasActiveDecoy() =>
            GameState.SurfaceState.AiObjects.Any(obj =>
                obj.ObjectName == "DroneDecoy" &&
                obj.ImpactStatus?.HasExploded != true &&
                obj.ObjectParts?.Count > 0);

        private static bool HasActivePowerUp() =>
            GameState.SurfaceState.AiObjects.Any(obj =>
                obj.ObjectName == "PowerUp" &&
                obj.IsActive &&
                obj.ImpactStatus?.HasExploded != true &&
                obj.ObjectParts?.Count > 0);

        private static int CountAliveSeeders() =>
            GameState.SurfaceState.AiObjects.Count(IsAliveSeeder);

        private static bool IsAliveSeeder(I3dObject obj)
        {
            if (obj.ObjectName != "Seeder")
                return false;

            if (!obj.IsActive)
                return false;

            if (obj.ImpactStatus?.HasExploded == true)
                return false;

            if ((obj.ImpactStatus?.ObjectHealth ?? 1) <= 0)
                return false;

            return true;
        }

        private static bool IsActiveDrone(I3dObject obj)
        {
            if (obj.ObjectName != "KamikazeDrone")
                return false;

            if (!obj.IsActive)
                return false;

            if (obj.ImpactStatus?.HasExploded == true)
                return false;

            if ((obj.ImpactStatus?.ObjectHealth ?? 1) <= 0)
                return false;

            return true;
        }

        private static Vector3 GetShipWorldPosition()
        {
            if (GameState.ShipState?.ShipWorldPosition is Vector3 shipWorldPosition)
                return shipWorldPosition;

            var map = GameState.SurfaceState.GlobalMapPosition;
            return new Vector3 { x = map.x, y = map.y, z = map.z };
        }

        private static float DistanceSquaredXZ(IVector3 a, IVector3 b)
        {
            float dx = a.x - b.x;
            float dz = a.z - b.z;
            return dx * dx + dz * dz;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
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

        public void Dispose()
        {
            StartCoordinates = null;
            GuideCoordinates = null;
            _stage = TutorialPromptStage.Intro;
            _introSpokenAt = DateTime.MinValue;
            _powerupSpokenAt = DateTime.MinValue;
            _droneActivatedAt = DateTime.MinValue;
            _droneDestroyedAt = DateTime.MinValue;
            _padWorldPosition = null;
            _initialSeederCount = null;
            _initialPowerupsCollected = null;
            _droneWasActive = false;
        }
    }
}
