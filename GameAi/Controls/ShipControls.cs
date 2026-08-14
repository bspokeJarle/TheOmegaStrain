using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.Events;
using CommonUtilities.GamePlayHelpers;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Audio.Services;
using CommonUtilities.Input;
using Gma.System.MouseKeyHook;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using static Domain.WeaponHelpers;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    public class ShipControls : IObjectMovement
    {
        private bool logging = false;
        private const bool enableMovementDiagnostics = false;
        private int movementLogCounter = 0;
        private const float MaxThrust = 10.0f;
        private const float ThrustIncreaseRate = 0.5f;
        private const float RotationAcceleration = 1000f;
        private const float MaxRotationSpeed = 160f;
        private const float XboxRotationAccelerationMultiplier = 1.35f;
        private const float XboxMaxRotationSpeedMultiplier = 1.35f;
        private const float RotationDrag = 0.90f;
        private const float DEG2RAD = MathF.PI / 180f;
        private const float SurfaceLandingDamageSpeedThreshold = 5f;
        private const int MinSurfaceLandingDamage = 2;
        private const float LowSpeedSurfaceLandingDamageMultiplier = 2f;
        private const float HighSpeedSurfaceLandingDamageMultiplier = 12f;
        private const float SurfaceBounceTopTolerance = 0.5f;
        private const double UnsafeSurfaceHitResetSeconds = 2.0;
        private const double OverlayResumeCrashDetectionSuppressionSeconds = 2.0;
        private const double OverlayResumeGravityPauseSeconds = 2.0;
        private static float ShipRestingScreenY => ScreenSetup.screenSizeY * 0.195f;

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

        // Engine glow colors: idle (dark red) when no thrust, active (yellow) at full thrust.
        private static readonly (int r, int g, int b) EngineActiveRgb = (0xFF, 0xFF, 0x00);
        private static readonly (int r, int g, int b) EngineIdleRgb = (0x88, 0x11, 0x00);
        private static readonly Random _flickerRng = new();
        private static readonly Random _emitterRng = new();
        private const float EngineFlickerAmount = 0.10f;
        private const float BalancedThrustParticleCapMultiplier = 1.2f;
        private const int HighThrustParticleCap = 145;
        private const int HighThrustParticleMaxCap = 220;
        private const long DefaultThrustParticleVariedStartMaxTicks = 500_000;
        private const int HighThrustBurstMaxParticlesPerEmission = 14;
        private const long HighThrustParticleVariedStartMaxTicks = 1_250_000;
        private const float ThrustBurstTravelBoostAmount = 0.06f;
        private const float ThrustBurstTravelBoostSeconds = 0.28f;
        private const double DecoyDeployCooldownSeconds = 1.0;

        // Cannon recoil animation state
        private float _cannonRecoilOffset = 0f;
        private float _cannonRecoilReturnSpeed = 0f;
        private float _muzzleFlashSecondsLeft = 0f;
        private float _thrustBurstBoostSecondsLeft = 0f;
        private bool _thrustBurstBoostArmed = true;
        private string _muzzleFlashColor = BulletMuzzleFlashColor;
        private const float CannonRecoilDistance = 20f;
        private const float CannonRecoilReturnFraction = 0.6f;
        private const string MuzzleFlashPartName = "MuzzleFlash";
        private const float MuzzleFlashDurationSeconds = 0.08f;
        private const string BulletMuzzleFlashColor = "fff8c8";
        private const string LazerMuzzleFlashColor = "bfffff";

        // Audio references are initialized lazily from ConfigureAudio.
        private IAudioPlayer? _audio;
        private ISoundRegistry? _soundRegistry;
        private SoundDefinition? _rocketSound;
        private SoundDefinition? _explosionSound;
        private SoundDefinition? _releaseDecoySound;
        private SoundDefinition? _changeWeaponSound;
        private SoundDefinition? _bulletSound;
            private SoundDefinition? _powerupSound;
                private SoundDefinition? _impactThudSound;
                private SoundDefinition? _surfaceThudSound;
        private IAudioInstance? _rocketInstance;
        private DateTime _lastDecoyDeployUtc = DateTime.MinValue;

        private float _yawVelocity = 0f;
        private float _pitchVelocity = 0f;
        private float _yawAccumulator = 0f;
        private float _pitchAccumulator = 0f;
        private bool landed = false;
        private bool _surfaceBounceWaitingForGravity = false;
        private bool _unsafeSurfaceHitArmed = false;
        private DateTime _unsafeSurfaceHitAt = DateTime.MinValue;
        private float _lowAltitudeRunSeconds = 0f;
        private readonly ShipLoopTracker _loopTracker = new();
        private readonly ShipAiVoiceService _shipAiVoiceService = ShipAiVoiceService.Shared;

        private DateTime lastUpdateTime = DateTime.Now;

        public int FormerMouseX = 0;
        public int FormerMouseY = 0;
        public int rotationX = WorldViewSetup.CameraPitchDegreesInt;
        public int rotationY = 0;
        public int rotationZ = 0;
        public int tilt = 0;

        public int shipY = 0;
        public int zoom = 400;

        public I3dObject ParentObject { get; set; }
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public ITriangleMeshWithColor? WeaponStartCoordinates { get; set; }
        public ITriangleMeshWithColor? WeaponGuideCoordinates { get; set; }
        public ITriangleMeshWithColor? RearStartCoordinates { get; set; }
        public ITriangleMeshWithColor? RearGuideCoordinates { get; set; }

        public float Thrust { get; set; } = 0;
        public bool ThrustOn { get; set; } = false;
        public IPhysics Physics { get; set; } = new Physics.Physics();
        public ShipLoopStatus LastLoopStatus => _loopTracker.LastStatus;
        public int CleanLoopCount => _loopTracker.CleanLoopCount;
        public int CollisionLoopCount => _loopTracker.CollisionLoopCount;
        private bool hasInitialized = false;
        private bool isExploding = false;
        private DateTime _motherShipCollisionCooldown = DateTime.MinValue;
        private const float MotherShipCollisionCooldownSeconds = 2.0f;
        private bool _fireKeyHeld = false;
        private bool _leftHeld = false;
        private bool _rightHeld = false;
        private bool _upHeld = false;
        private bool _downHeld = false;
        private readonly HashSet<int> _collectedPowerUpIds = new();
        private DateTime ExplosionDeltaTime = DateTime.Now;
        private bool _hasExplosionTransformSnapshot = false;
        private Vector3? _explosionWorldPosition;
        private Vector3? _explosionObjectOffsets;
        private Vector3? _explosionRotation;
        private Vector3? _explosionCalculatedCrashOffset;
        private OverlayPauseTransformSnapshot? _overlayPauseSnapshot;

        private sealed class OverlayPauseTransformSnapshot
        {
            public Vector3? WorldPosition { get; init; }
            public Vector3? ObjectOffsets { get; init; }
            public Vector3? Rotation { get; init; }
            public int RotationX { get; init; }
            public int RotationY { get; init; }
            public int RotationZ { get; init; }
            public int Tilt { get; init; }
            public int ShipY { get; init; }
            public int Zoom { get; init; }
        }

        public ShipControls()
        {
            var hook = InputManager.SharedHook;

            hook.KeyDown += GlobalHookKeyDown;
            hook.KeyUp += GlobalHookKeyUp;
            hook.MouseMove += GlobalHookMouseMovement;
            hook.MouseDownExt += GlobalHookMouseDownExt;
            hook.MouseUpExt += GlobalHookMouseUpExt;
            hook.MouseWheelExt += GlobalHookMouseWheel;
            hook.MouseHWheelExt += GlobalHookMouseWheel;
        }

        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            // Already configured, so nothing else is required.
            if (_audio != null && _soundRegistry != null && _rocketSound != null && _explosionSound != null && _releaseDecoySound != null && _changeWeaponSound != null && _bulletSound != null && _powerupSound != null && _impactThudSound != null && _surfaceThudSound != null)
                return;

            if (audioPlayer == null || soundRegistry == null)
                return;

            _audio = audioPlayer;
            _soundRegistry = soundRegistry;
            _rocketSound = soundRegistry.Get("rocket_main");
            _explosionSound = soundRegistry.Get("explosion_main");
            _releaseDecoySound = soundRegistry.Get("release_decoy");
            _changeWeaponSound = soundRegistry.Get("change_weapon");
            _bulletSound = soundRegistry.Get("bullet_main");
            _powerupSound = soundRegistry.Get("powerup_collect");
            _impactThudSound = soundRegistry.Get("lazer_thud");
            _surfaceThudSound = soundRegistry.Get("ship_thud");
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) StartCoordinates = StartCoord;
            if (GuideCoord != null) GuideCoordinates = GuideCoord;
        }

        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) RearStartCoordinates = StartCoord;
            if (GuideCoord != null) RearGuideCoordinates = GuideCoord;
        }

        private void GlobalHookKeyDown(object sender, KeyEventArgs e)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return;
            }

            if (TryHandleKeyboardWeaponSelection(e.KeyCode))
                return;

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Keyboard)
                return;

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardTurnLeftKey)) _leftHeld = true;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardTurnRightKey)) _rightHeld = true;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardPitchUpKey)) _upHeld = true;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardPitchDownKey)) _downHeld = true;

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardThrustKey))
                BeginThrust();

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardFireKey))
            {
                _fireKeyHeld = true;
                FireWeapon();
            }
            //Prevent further processing of this key event
            //#if DEBUG
            //    e.SuppressKeyPress = true;
            //#endif  
        }

        private void GlobalHookKeyUp(object sender, KeyEventArgs e)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return;
            }

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Keyboard)
                return;

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardFireKey))
                _fireKeyHeld = false;

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardTurnLeftKey)) _leftHeld = false;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardTurnRightKey)) _rightHeld = false;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardPitchUpKey)) _upHeld = false;
            if (IsConfiguredKey(e.KeyCode, settings.KeyboardPitchDownKey)) _downHeld = false;

            if (IsConfiguredKey(e.KeyCode, settings.KeyboardThrustKey))
                EndThrust();
        }

        // Delta-based mouse input: each mouse movement accumulates pixel deltas.
        // Raw input is preferred because it is not clamped by monitor edges; the
        // global mouse hook path remains as a fallback if raw input is unavailable.
        private bool _mouseActive = false;
        private int _lastMouseX;
        private int _lastMouseY;
        private const float MouseYawTargetSensitivity = 0.25f;
        private const float MousePitchTargetSensitivity = 0.12f;
        private const float MouseTargetFollowPer90Frame = 0.42f;
        private const float MouseDeadZonePixels = 2f;
        private const float MaxMouseDeltaPerEvent = 80f;
        private static readonly TimeSpan RawMouseFallbackWindow = TimeSpan.FromMilliseconds(250);
        private bool _mouseTargetInitialized = false;
        private float _mouseTargetRotationZ = 0f;
        private float _mouseTargetTilt = 0f;
        private float _mouseSmoothedRotationZ = 0f;
        private float _mouseSmoothedTilt = 0f;

        public void GlobalHookMouseMovement(object sender, MouseEventArgs e)
        {
            HandleGlobalHookMouseMovement(e, requireForeground: true);
        }

        public void HandleRawMouseDelta(int deltaX, int deltaY)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return;
            }

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
            {
                ResetMouseControlState();
                return;
            }

            _mouseActive = true;
            ApplyMouseDelta(deltaX, deltaY, applyDeadZone: false);
        }

        private void HandleGlobalHookMouseMovement(MouseEventArgs e, bool requireForeground)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return;
            }

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
            {
                ResetMouseControlState();
                return;
            }

            if (requireForeground && !IsGameProcessForeground())
            {
                ClearBlockedGameplayInputState();
                return;
            }

            if (RawMouseInput.HasRecentMouseDelta(RawMouseFallbackWindow))
            {
                _mouseActive = true;
                _lastMouseX = e.X;
                _lastMouseY = e.Y;
                EnsureMouseTargetInitialized();
                return;
            }

            if (!_mouseActive)
            {
                BeginMouseControl(e.X, e.Y);
                return;
            }

            float dx = e.X - _lastMouseX;
            float dy = e.Y - _lastMouseY;
            _lastMouseX = e.X;
            _lastMouseY = e.Y;

            ApplyMouseDelta(dx, dy, applyDeadZone: true);
        }

        private void BeginMouseControl(int x, int y)
        {
            _lastMouseX = x;
            _lastMouseY = y;
            _mouseActive = true;
            EnsureMouseTargetInitialized();
        }

        private void ResetMouseControlState()
        {
            _mouseActive = false;
            _mouseTargetInitialized = false;
        }

        private void ApplyMouseDelta(float deltaX, float deltaY, bool applyDeadZone)
        {
            EnsureMouseTargetInitialized();

            if (applyDeadZone)
            {
                if (MathF.Abs(deltaX) < MouseDeadZonePixels) deltaX = 0f;
                if (MathF.Abs(deltaY) < MouseDeadZonePixels) deltaY = 0f;
            }

            deltaX = ClampMouseDelta(deltaX);
            deltaY = ClampMouseDelta(deltaY);

            _mouseTargetRotationZ += deltaX * MouseYawTargetSensitivity;
            _mouseTargetTilt += deltaY * MousePitchTargetSensitivity;
        }

        private static float ClampMouseDelta(float delta)
        {
            return Math.Clamp(delta, -MaxMouseDeltaPerEvent, MaxMouseDeltaPerEvent);
        }

        private void EnsureMouseTargetInitialized()
        {
            if (_mouseTargetInitialized)
                return;

            _mouseTargetRotationZ = rotationZ;
            _mouseTargetTilt = tilt;
            _mouseSmoothedRotationZ = rotationZ;
            _mouseSmoothedTilt = tilt;
            _mouseTargetInitialized = true;
        }

        private void ApplyMouseTargetRotation()
        {
            EnsureMouseTargetInitialized();

            float follow = 1f - MathF.Pow(1f - MouseTargetFollowPer90Frame, GameState.FrameScale90);
            _mouseSmoothedRotationZ += (_mouseTargetRotationZ - _mouseSmoothedRotationZ) * follow;
            _mouseSmoothedTilt += (_mouseTargetTilt - _mouseSmoothedTilt) * follow;

            rotationZ = (int)MathF.Round(_mouseSmoothedRotationZ);
            tilt = (int)MathF.Round(_mouseSmoothedTilt);

            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _yawAccumulator = 0f;
            _pitchAccumulator = 0f;
        }

        private float _xboxYawInput = 0f;
        private float _xboxPitchInput = 0f;
        private bool _xboxBulletWasPressed = false;
        private bool _xboxDecoyWasPressed = false;
        private bool _xboxLazerWasPressed = false;
        private bool _xboxPowerup4WasPressed = false;
        private const int XboxControllerIndex = 0;

        private void GlobalHookMouseDown(object sender, MouseEventArgs e)
        {
            HandleGlobalHookMouseDown(e, requireForeground: false);
        }

        private void GlobalHookMouseDownExt(object sender, MouseEventExtArgs e)
        {
            if (HandleGlobalHookMouseDown(e, requireForeground: true))
                e.Handled = true;
        }

        private bool HandleGlobalHookMouseDown(MouseEventArgs e, bool requireForeground)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return false;
            }

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
                return false;

            if (requireForeground && !IsGameProcessForeground())
            {
                ClearBlockedGameplayInputState();
                return false;
            }

            if (e.Button == ToMouseButton(settings.MouseThrustButton))
                BeginThrust();

            if (e.Button == ToMouseButton(settings.MouseFireButton))
            {
                _fireKeyHeld = true;
                FireWeapon();
            }

            return true;
        }

        private void GlobalHookMouseUp(object sender, MouseEventArgs e)
        {
            HandleGlobalHookMouseUp(e, requireForeground: false);
        }

        private void GlobalHookMouseUpExt(object sender, MouseEventExtArgs e)
        {
            if (HandleGlobalHookMouseUp(e, requireForeground: true))
                e.Handled = true;
        }

        private bool HandleGlobalHookMouseUp(MouseEventArgs e, bool requireForeground)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
                return false;
            }

            var settings = GetNormalizedSettings();
            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
                return false;

            if (requireForeground && !IsGameProcessForeground())
            {
                ClearBlockedGameplayInputState();
                return false;
            }

            if (e.Button == ToMouseButton(settings.MouseThrustButton))
                EndThrust();

            if (e.Button == ToMouseButton(settings.MouseFireButton))
                _fireKeyHeld = false;

            return true;
        }

        private void GlobalHookMouseWheel(object? sender, MouseEventExtArgs e)
        {
            if (ShouldSwallowGameplayMouseEvent())
                e.Handled = true;
        }

        private bool TryHandleKeyboardWeaponSelection(Keys keyCode)
        {
            if (keyCode == Keys.D1 || keyCode == Keys.NumPad1)
            {
                bool weaponChanged = GameState.GamePlayState.SelectedWeapon != WeaponType.Bullet ||
                    !string.Equals(GameState.GamePlayState.ActivePowerup, "BULLET", StringComparison.OrdinalIgnoreCase);

                GameState.GamePlayState.SelectedWeapon = WeaponType.Bullet;
                GameState.GamePlayState.ActivePowerup = "BULLET";
                PlayWeaponChangedSound(weaponChanged);
                return true;
            }

            if (keyCode == Keys.D2 || keyCode == Keys.NumPad2)
            {
                if (!GameState.GamePlayState.IsDecoyUnlocked)
                    return true;

                bool weaponChanged = !string.Equals(GameState.GamePlayState.ActivePowerup, "DECOY", StringComparison.OrdinalIgnoreCase);

                GameState.GamePlayState.ActivePowerup = "DECOY";
                PlayWeaponChangedSound(weaponChanged);
                return true;
            }

            if (keyCode == Keys.D3 || keyCode == Keys.NumPad3)
            {
                if (!GameState.GamePlayState.IsLazerUnlocked)
                    return true;

                bool weaponChanged = GameState.GamePlayState.SelectedWeapon != WeaponType.Lazer ||
                    !string.Equals(GameState.GamePlayState.ActivePowerup, "LAZER", StringComparison.OrdinalIgnoreCase);

                GameState.GamePlayState.SelectedWeapon = WeaponType.Lazer;
                GameState.GamePlayState.ActivePowerup = "LAZER";
                PlayWeaponChangedSound(weaponChanged);
                return true;
            }

            if (keyCode == Keys.D4 || keyCode == Keys.NumPad4)
                return true;

            return false;
        }

        private void PlayWeaponChangedSound(bool weaponChanged)
        {
            if (!weaponChanged || _audio == null || _changeWeaponSound == null || ParentObject == null)
                return;

            var audioPosition = ((_3dObject)ParentObject).GetAudioPosition();
            _audio.Play(
                _changeWeaponSound,
                AudioPlayMode.OneShot,
                new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private void BeginThrust()
        {
            if (ThrustOn == false)
            {
                if (_rocketInstance != null)
                {
                    if (Logger.ShouldLog(logging)) Logger.Log("Audio: Force-stopping previous rocket instance before starting new.");
                    _rocketInstance.Stop(playEndSegment: false);
                    _rocketInstance = null;
                }

                if (_audio != null && _rocketSound != null && ParentObject != null)
                {
                    var audioPosition = ((_3dObject)ParentObject).GetAudioPosition();
                    if (Logger.ShouldLog(logging)) Logger.Log("Audio: Starting new rocket segmented loop.");
                    _rocketInstance = _audio.Play(
                        _rocketSound,
                        AudioPlayMode.SegmentedLoop,
                        new AudioPlayOptions
                        {
                            WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                        });
                }
            }

            ThrustOn = true;
        }

        private void EndThrust()
        {
            ThrustOn = false;
            Thrust = 0;
            Physics.ThrustEffect = 0f;
            Physics.VerticalLiftFactor = 0f;
            ResetThrustParticleStyle();
            ResetThrustBurstBoost();

            if (_rocketInstance != null)
            {
                _rocketInstance.Stop(playEndSegment: true);
                _rocketInstance = null;
            }
        }

        private static GameSettingsState GetNormalizedSettings()
        {
            GameState.SettingsState ??= new GameSettingsState();
            GameState.SettingsState.Normalize();
            return GameState.SettingsState;
        }

        private static bool IsConfiguredKey(Keys keyCode, string configuredKey)
        {
            return !string.IsNullOrWhiteSpace(configuredKey) &&
                   Enum.TryParse(configuredKey, ignoreCase: true, out Keys configuredKeyCode) &&
                   keyCode == configuredKeyCode;
        }

        private static MouseButtons ToMouseButton(MouseControlButton button) =>
            button switch
            {
                MouseControlButton.Right => MouseButtons.Right,
                MouseControlButton.Middle => MouseButtons.Middle,
                _ => MouseButtons.Left
            };

        private static bool ShouldSwallowGameplayMouseEvent()
        {
            var settings = GetNormalizedSettings();
            return settings.ActiveControlScheme == ControlInputMode.Mouse &&
                   !IsGameplayInputBlocked() &&
                   IsGameProcessForeground();
        }

        private static bool IsGameProcessForeground()
        {
            IntPtr foregroundWindow = GetForegroundWindow();
            if (foregroundWindow == IntPtr.Zero)
                return false;

            GetWindowThreadProcessId(foregroundWindow, out uint foregroundProcessId);
            return foregroundProcessId == (uint)Environment.ProcessId;
        }

        private void ApplyActiveControlSchemeInputGuards(GameSettingsState settings)
        {
            if (settings.ActiveControlScheme != ControlInputMode.Keyboard)
            {
                _leftHeld = false;
                _rightHeld = false;
                _upHeld = false;
                _downHeld = false;
            }

            if (settings.ActiveControlScheme != ControlInputMode.Mouse)
            {
                ResetMouseControlState();
            }

            if (settings.ActiveControlScheme != ControlInputMode.XboxController)
            {
                _xboxYawInput = 0f;
                _xboxPitchInput = 0f;
            }
        }

        private void ApplyXboxControllerInput(GameSettingsState settings)
        {
            if (settings.ActiveControlScheme != ControlInputMode.XboxController)
            {
                _xboxYawInput = 0f;
                _xboxPitchInput = 0f;
                ResetXboxButtonEdges();
                return;
            }

            if (!XboxControllerInput.TryGetState(XboxControllerIndex, out var controllerState))
            {
                ClearXboxControllerInputState();
                return;
            }

            float turnLeft = XboxControllerInput.GetControlStrength(controllerState, settings.XboxTurnLeftButton);
            float turnRight = XboxControllerInput.GetControlStrength(controllerState, settings.XboxTurnRightButton);
            float pitchUp = XboxControllerInput.GetControlStrength(controllerState, settings.XboxPitchUpButton);
            float pitchDown = XboxControllerInput.GetControlStrength(controllerState, settings.XboxPitchDownButton);

            _xboxYawInput = turnRight - turnLeft;
            _xboxPitchInput = pitchUp - pitchDown;

            if (XboxControllerInput.IsControlPressed(controllerState, settings.XboxThrustButton))
                BeginThrust();
            else if (ThrustOn)
                EndThrust();

            _fireKeyHeld = XboxControllerInput.IsControlPressed(controllerState, settings.XboxFireButton);

            UpdateXboxWeaponSelection(
                XboxControllerInput.IsControlPressed(controllerState, settings.XboxBulletButton),
                ref _xboxBulletWasPressed,
                Keys.D1);
            UpdateXboxWeaponSelection(
                XboxControllerInput.IsControlPressed(controllerState, settings.XboxDecoyButton),
                ref _xboxDecoyWasPressed,
                Keys.D2);
            UpdateXboxWeaponSelection(
                XboxControllerInput.IsControlPressed(controllerState, settings.XboxLazerButton),
                ref _xboxLazerWasPressed,
                Keys.D3);
            UpdateXboxWeaponSelection(
                XboxControllerInput.IsControlPressed(controllerState, settings.XboxPowerup4Button),
                ref _xboxPowerup4WasPressed,
                Keys.D4);
        }

        private void UpdateXboxWeaponSelection(bool isPressed, ref bool wasPressed, Keys keyboardEquivalent)
        {
            if (isPressed && !wasPressed)
                TryHandleKeyboardWeaponSelection(keyboardEquivalent);

            wasPressed = isPressed;
        }

        private void ClearXboxControllerInputState()
        {
            _leftHeld = false;
            _rightHeld = false;
            _upHeld = false;
            _downHeld = false;
            _xboxYawInput = 0f;
            _xboxPitchInput = 0f;
            _fireKeyHeld = false;
            ResetXboxButtonEdges();

            if (ThrustOn)
                EndThrust();
        }

        private void ResetXboxButtonEdges()
        {
            _xboxBulletWasPressed = false;
            _xboxDecoyWasPressed = false;
            _xboxLazerWasPressed = false;
            _xboxPowerup4WasPressed = false;
        }

        private static bool IsGameplayInputBlocked()
        {
            var overlay = GameState.ScreenOverlayState;
            var gameplay = GameState.GamePlayState;
            bool isGameplayScene =
                gameplay.CurrentSceneType == SceneTypes.Game ||
                gameplay.CurrentSceneType == SceneTypes.Simulation ||
                gameplay.CurrentSceneType == SceneTypes.Tutorial;

            // Modal overlays and explicit reward pauses silence gameplay input.
            // Ordinary non-modal in-game overlays can still leave the ship controllable.
            return !isGameplayScene ||
                   gameplay.IsVictoryRewardPauseActive ||
                   GameState.TutorialState.InstructionOverlayPauseActive ||
                   overlay.BlocksGameplayInput ||
                   gameplay.IsPaused;
        }

        public void CaptureOverlayPauseTransform(I3dObject? ship = null)
        {
            var target = ship ?? ParentObject;
            if (target == null)
                return;

            var snapshotSource = ParentObject ?? target;
            _overlayPauseSnapshot = new OverlayPauseTransformSnapshot
            {
                WorldPosition = CloneVector(snapshotSource.WorldPosition),
                ObjectOffsets = CloneVector(snapshotSource.ObjectOffsets),
                Rotation = CloneVector(snapshotSource.Rotation),
                RotationX = rotationX,
                RotationY = rotationY,
                RotationZ = rotationZ,
                Tilt = tilt,
                ShipY = snapshotSource.ObjectOffsets != null ? (int)snapshotSource.ObjectOffsets.y : shipY,
                Zoom = zoom
            };
        }

        public void RestoreOverlayPauseTransformAndSuppressCrashDetection(I3dObject? ship = null)
        {
            var target = ship ?? ParentObject;
            if (_overlayPauseSnapshot != null && target != null)
            {
                ApplyOverlayPauseSnapshot(target, _overlayPauseSnapshot);

                if (ParentObject != null && !ReferenceEquals(ParentObject, target))
                    ApplyOverlayPauseSnapshot(ParentObject, _overlayPauseSnapshot);

                UpdateShipWorldPosition(target);
            }

            _overlayPauseSnapshot = null;
            lastUpdateTime = DateTime.Now;
            ClearTransientResumeInputState();
            ClearPendingShipCrash(target);
            var resumeUtc = DateTime.UtcNow;
            GameState.ShipState.ShipCrashDetectionDisabledUntilUtc =
                resumeUtc.AddSeconds(OverlayResumeCrashDetectionSuppressionSeconds);
            GameState.ShipState.ShipGravityDisabledUntilUtc =
                resumeUtc.AddSeconds(OverlayResumeGravityPauseSeconds);
        }

        private void ApplyOverlayPauseSnapshot(I3dObject target, OverlayPauseTransformSnapshot snapshot)
        {
            target.WorldPosition = CloneVector(snapshot.WorldPosition);
            target.ObjectOffsets = CloneVector(snapshot.ObjectOffsets);
            if (target.ObjectOffsets != null)
                target.ObjectOffsets.z = snapshot.Zoom;
            target.Rotation = CloneVector(snapshot.Rotation);

            rotationX = snapshot.RotationX;
            rotationY = snapshot.RotationY;
            rotationZ = snapshot.RotationZ;
            tilt = snapshot.Tilt;
            shipY = snapshot.ShipY;
            zoom = snapshot.Zoom;
        }

        private void ClearTransientResumeInputState()
        {
            _leftHeld = false;
            _rightHeld = false;
            _upHeld = false;
            _downHeld = false;
            _fireKeyHeld = false;
            ResetMouseControlState();
            _xboxYawInput = 0f;
            _xboxPitchInput = 0f;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _yawAccumulator = 0f;
            _pitchAccumulator = 0f;
        }

        public void ClearGameplayInputForPause()
        {
            ClearBlockedGameplayInputState();
            ClearTransientResumeInputState();
        }

        public void ResumeFromGameplayPause(I3dObject? ship = null)
        {
            lastUpdateTime = DateTime.Now;
            ClearGameplayInputForPause();
            ClearPendingShipCrash(ship ?? ParentObject);
            GameState.ShipState.ShipGravityDisabledUntilUtc =
                DateTime.UtcNow.AddSeconds(OverlayResumeGravityPauseSeconds);
        }

        private void ClearBlockedGameplayInputState()
        {
            _leftHeld = false;
            _rightHeld = false;
            _upHeld = false;
            _downHeld = false;
            _fireKeyHeld = false;
            ResetMouseControlState();
            _xboxYawInput = 0f;
            _xboxPitchInput = 0f;
            ThrustOn = false;
            Thrust = 0f;
            Physics.ThrustEffect = 0f;
            Physics.VerticalLiftFactor = 0f;
            ResetThrustParticleStyle();
            ResetThrustBurstBoost();
            HideMuzzleFlash(ParentObject);

            if (_rocketInstance != null)
            {
                _rocketInstance.Stop(playEndSegment: true);
                _rocketInstance = null;
            }
        }

        private static void ClearPendingShipCrash(I3dObject? ship)
        {
            if (ship?.ImpactStatus == null)
                return;

            if (!ship.ImpactStatus.HasCrashed)
                return;

            ship.ImpactStatus.HasCrashed = false;
            ship.ImpactStatus.ImpactDirection = null;
            ship.ImpactStatus.CrashBoxName = null;
            ship.ImpactStatus.ObjectName = "";
        }

        private static Vector3? CloneVector(IVector3? vector)
        {
            if (vector == null)
                return null;

            return new Vector3 { x = vector.x, y = vector.y, z = vector.z };
        }

        public void ResetRotationForTutorialResume(I3dObject? ship = null)
        {
            _leftHeld = false;
            _rightHeld = false;
            _upHeld = false;
            _downHeld = false;
            _fireKeyHeld = false;
            ResetMouseControlState();
            _xboxYawInput = 0f;
            _xboxPitchInput = 0f;
            _yawVelocity = 0f;
            _pitchVelocity = 0f;
            _yawAccumulator = 0f;
            _pitchAccumulator = 0f;
            rotationX = WorldViewSetup.CameraPitchDegreesInt;
            rotationY = 0;
            rotationZ = 0;
            tilt = 0;
            _loopTracker.CancelActiveLoop();

            ApplyRotationReset(ship);
            if (ParentObject != null && !ReferenceEquals(ParentObject, ship))
                ApplyRotationReset(ParentObject);
        }

        private void ApplyRotationReset(I3dObject? ship)
        {
            if (ship?.Rotation == null)
                return;

            ship.Rotation.x = rotationX;
            ship.Rotation.y = rotationY;
            ship.Rotation.z = rotationZ;
        }

        private bool FireWeapon()
        {
            if (string.Equals(GameState.GamePlayState.ActivePowerup, "DECOY", StringComparison.OrdinalIgnoreCase))
            {
                bool decoyDeployed = DeployDecoy();
                _fireKeyHeld = false;
                return decoyDeployed;
            }

            if (!CanDispatchWeapon())
                return false;

            var selectedWeapon = GameState.GamePlayState.SelectedWeapon;
            if (!GameState.GamePlayState.TryFireSelectedWeapon())
                return false;

            // Fire weapon from ship
            var rot = new Vector3
            {
                x = rotationX,
                y = rotationY,
                z = rotationZ
            };
            ParentObject.Rotation = rot;
            int activeWeaponsBefore = ParentObject.WeaponSystems.ActiveWeapons.Count;
            ParentObject.WeaponSystems.FireWeapon(
                WeaponGuideCoordinates?.vert1,
                WeaponStartCoordinates?.vert1,
                GameState.SurfaceState.GlobalMapPosition,
                selectedWeapon,
                ParentObject,
                tilt);

            bool weaponWasActivated = ParentObject.WeaponSystems.ActiveWeapons.Count > activeWeaponsBefore;
            if (!weaponWasActivated)
                return false;

            TriggerMuzzleFlash(selectedWeapon);

            if (selectedWeapon == WeaponType.Bullet)
                PlayBulletOneShot();

            // Trigger cannon recoil for lazer and bullet
            if (selectedWeapon == WeaponType.Lazer ||
                selectedWeapon == WeaponType.Bullet)
            {
                _cannonRecoilOffset = CannonRecoilDistance;
                float cooldown = selectedWeapon == WeaponType.Bullet
                    ? GameState.GamePlayState.BulletCooldownSeconds
                    : GameState.GamePlayState.LaserCooldownSeconds;
                _cannonRecoilReturnSpeed = CannonRecoilDistance / (cooldown * CannonRecoilReturnFraction);
            }

            return true;
        }

        private bool CanDispatchWeapon()
        {
            return ParentObject?.WeaponSystems != null &&
                   WeaponGuideCoordinates?.vert1 != null &&
                   WeaponStartCoordinates?.vert1 != null;
        }

        private void PlayBulletOneShot()
        {
            if (_audio == null || _bulletSound == null || ParentObject is not _3dObject ship)
                return;

            var audioPosition = ship.GetAudioPosition();
            _audio.PlayOneShot(
                _bulletSound,
                new AudioPlayOptions
                {
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private bool DeployDecoy()
        {
            if (ParentObject?.ParentSurface == null)
            {
                return false;
            }

            var nowUtc = DateTime.UtcNow;
            if (nowUtc - _lastDecoyDeployUtc < TimeSpan.FromSeconds(DecoyDeployCooldownSeconds))
            {
                return false;
            }

            int activeDecoyCount = GameState.SurfaceState.AiObjects.Count(obj =>
                obj.ObjectName == "DroneDecoy" &&
                obj.ImpactStatus?.HasExploded != true &&
                obj.ObjectParts?.Count > 0);

            if (activeDecoyCount >= GameSetup.MaxActiveDecoys)
            {
                return false;
            }

            var decoy = CreateDecoyBeaconObject(ParentObject.ParentSurface);
            if (decoy == null)
            {
                return false;
            }

            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            var shipOffsets = ParentObject.ObjectOffsets ?? new Vector3();

            // The viewport center offset converts map position (top-left) to the
            // ship's actual world position (center of viewport) for correct minimap placement.
            // ObjectOffsets compensate so the decoy renders at the ship's screen position.
            // Screen formula: screenZ = (GlobalMapPos.z - WorldPos.z) + offsets.z
            //                 screenX = -(GlobalMapPos.x - WorldPos.x) + offsets.x
            int vc = (SurfaceSetup.viewPortSize * SurfaceSetup.tileSize) / 2;

            decoy.WorldPosition = new Vector3
            {
                x = mapPosition.x + vc + shipOffsets.x,
                y = mapPosition.y - 50f,
                z = mapPosition.z + vc
            };
            decoy.ObjectOffsets = new Vector3
            {
                x = -(vc + shipOffsets.x),
                y = shipOffsets.y - (mapPosition.y * SurfacePositionSyncHelpers.DefaultEnemySurfaceSyncFactorY),
                z = shipOffsets.z + vc
            };
            decoy.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            decoy.ObjectName = "DroneDecoy";
            decoy.ImpactStatus = new ImpactStatus();
            decoy.CrashBoxDebugMode = false;
            decoy.WeaponSystems = null;

            GameState.SurfaceState.AiObjects.Add(decoy);
            GameState.PendingWorldObjects.Add(decoy);
            _lastDecoyDeployUtc = nowUtc;

            if (_audio != null && _releaseDecoySound != null)
            {
                var audioPosition = ((_3dObject)ParentObject).GetAudioPosition();
                _audio.Play(
                    _releaseDecoySound,
                    AudioPlayMode.OneShot,
                    new AudioPlayOptions
                    {
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }

            return true;
        }

        private static _3dObject? CreateDecoyBeaconObject(ISurface parentSurface)
        {
            const string decoyBeaconTypeName = "_3dRotations.World.Objects.DecoyBeacon";

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                var decoyType = assembly.GetType(decoyBeaconTypeName, throwOnError: false, ignoreCase: false);
                if (decoyType == null)
                {
                    continue;
                }

                var createMethod = decoyType.GetMethod("CreateDecoyBeacon", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                if (createMethod?.Invoke(null, new object[] { parentSurface }) is _3dObject decoy)
                {
                    return decoy;
                }
            }

            return null;
        }

        private void IncreaseThrustAndRelease()
        {
            if (Thrust < MaxThrust)
                Thrust = MathF.Min(MaxThrust, Thrust + ThrustIncreaseRate * GameState.FrameScale90);

            int totalThrust = (int)Thrust;

            if (totalThrust == 0)
            {
                _thrustBurstBoostArmed = true;
                ParentObject?.Particles?.ReleaseParticles(
                    GuideCoordinates,
                    StartCoordinates,
                    GameState.SurfaceState.GlobalMapPosition,
                    this,
                    0,
                    false);
                ResetThrustParticleStyle();
                return;
            }

            StartThrustBurstBoostIfNeeded();

            var (vertical, forward) = GetThrustComponents();
            bool hasRear = forward > 0.01f && RearStartCoordinates != null && RearGuideCoordinates != null;

            // Each frame, pick which engine emits based on thrust component weights.
            // Over time this distributes particles proportionally between engines
            // while avoiding shared-pool contention from multiple calls.
            ITriangleMeshWithColor? emitStart = StartCoordinates;
            ITriangleMeshWithColor? emitGuide = GuideCoordinates;

            if (hasRear)
            {
                float rearProbability = forward / (vertical + forward);
                if (_emitterRng.NextDouble() < rearProbability)
                {
                    emitStart = RearStartCoordinates;
                    emitGuide = RearGuideCoordinates;
                }
            }

            ConfigureThrustParticleStyle();

            ParentObject?.Particles?.ReleaseParticles(
                emitGuide,
                emitStart,
                GameState.SurfaceState.GlobalMapPosition,
                this,
                totalThrust,
                false);
        }

        private void ConfigureThrustParticleStyle()
        {
            if (ParentObject?.Particles is not ParticlesAI particles)
                return;

            if (!ShouldUseEnhancedThrustParticles())
            {
                ResetThrustParticleStyle();
                if (ShouldUseBalancedThrustParticleBoost())
                    particles.DynamicCapMultiplier = BalancedThrustParticleCapMultiplier;
                return;
            }

            particles.DynamicCapMultiplier = 1.0f;
            particles.MaxParticlesOverride = GetThrustParticleCap();
            particles.BurstMaxParticlesPerEmission = HighThrustBurstMaxParticlesPerEmission;
            particles.VariedStartMaxTicks = HighThrustParticleVariedStartMaxTicks;
            particles.LifeMultiplier = 0.72f;
            particles.SizeMultiplier = 1.55f;
            particles.ThrottleDurationFactor = 0.2f;
            particles.GravityStrength = 34f;
            particles.ColorStartOverride = "fff8c8";
            particles.ColorMidOverride = "ff8a20";
            particles.ColorEndOverride = "5a1800";
        }

        private void ResetThrustParticleStyle()
        {
            if (ParentObject?.Particles is not ParticlesAI particles)
                return;

            particles.MaxParticlesOverride = 0;
            particles.DynamicCapMultiplier = 1.0f;
            particles.BurstMaxParticlesPerEmission = 0;
            particles.VariedStartMaxTicks = DefaultThrustParticleVariedStartMaxTicks;
            particles.LifeMultiplier = 1.0f;
            particles.SizeMultiplier = 1.0f;
            particles.ThrottleDurationFactor = 0.3f;
            particles.GravityStrength = 50f;
            particles.ColorStartOverride = null;
            particles.ColorMidOverride = null;
            particles.ColorEndOverride = null;
        }

        private void StartThrustBurstBoostIfNeeded()
        {
            if (!_thrustBurstBoostArmed)
                return;

            _thrustBurstBoostSecondsLeft = ThrustBurstTravelBoostSeconds;
            _thrustBurstBoostArmed = false;
        }

        private void ResetThrustBurstBoost()
        {
            _thrustBurstBoostSecondsLeft = 0f;
            _thrustBurstBoostArmed = true;
        }

        private float ConsumeThrustBurstTravelBoost(float deltaTime)
        {
            if (_thrustBurstBoostSecondsLeft <= 0f)
                return 1f;

            float progress = Math.Clamp(_thrustBurstBoostSecondsLeft / ThrustBurstTravelBoostSeconds, 0f, 1f);
            float multiplier = 1f + ThrustBurstTravelBoostAmount * progress;
            _thrustBurstBoostSecondsLeft = MathF.Max(0f, _thrustBurstBoostSecondsLeft - MathF.Max(0f, deltaTime));
            return multiplier;
        }

        private static int GetThrustParticleCap()
        {
            var settings = GameState.SettingsState;
            if (settings?.GraphicsQuality != GraphicsQualityPreset.High)
                return 0;

            return Math.Clamp(
                (int)MathF.Round(HighThrustParticleCap * MathF.Max(1f, settings.ParticleDensityMultiplier)),
                HighThrustParticleCap,
                HighThrustParticleMaxCap);
        }

        private static bool ShouldUseEnhancedThrustParticles()
        {
            return GameState.SettingsState?.GraphicsQuality == GraphicsQualityPreset.High;
        }

        private static bool ShouldUseBalancedThrustParticleBoost()
        {
            var quality = GameState.SettingsState?.GraphicsQuality ?? GraphicsQualityPreset.Balanced;
            return quality == GraphicsQualityPreset.Balanced;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void UpdateShipWorldPosition(I3dObject theObject)
        {
            float shipOffsetY = theObject.ObjectOffsets?.y ?? shipY;

            GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(shipOffsetY, zoom);
            GameState.ShipState.ShipCrashCenterWorldPosition = SurfacePositionSyncHelpers.GetObjectCrashCenterWorldPosition(theObject);
            GameState.ShipState.ShipImpactStatus = theObject.ImpactStatus;
            GameState.ShipState.ShipObjectOffsets = new Vector3
            {
                x = theObject.ObjectOffsets?.x ?? 0f,
                y = shipOffsetY,
                z = theObject.ObjectOffsets?.z ?? zoom
            };
            GameState.ShipState.ShipHasShadow = theObject.HasShadow;
        }

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            if (IsGameplayInputBlocked())
            {
                ClearBlockedGameplayInputState();
            }

            //Update gamestate with thrust
            GameState.GamePlayState.Thrust = Thrust;

            // Lazily initialize audio the first time MoveObject runs.
            ConfigureAudio(audioPlayer, soundRegistry);
            _shipAiVoiceService.Update(_audio);

            ParentObject ??= theObject;

            if (!hasInitialized)
            {
                hasInitialized = true;
                Thrust = 0;
                ThrustOn = false;
                landed = true;
                ParentObject.ObjectOffsets.x = 0;
                ParentObject.ObjectOffsets.y = ShipRestingScreenY;
                ParentObject.ObjectOffsets.z = zoom;
            }

            if (GameState.GamePlayState.IsVictoryRewardPauseActive)
            {
                return theObject;
            }

            var now = DateTime.Now;
            float deltaTime = GameState.ClampedDeltaTime;
            lastUpdateTime = now;

            GameState.GamePlayState.Update(deltaTime);
            ExpireUnsafeSurfaceHit(now);

            bool shipCrashDetectionSuppressed = IsShipCrashDetectionSuppressed();
            if (shipCrashDetectionSuppressed)
                ClearPendingShipCrash(theObject);

            if (IsLoopCollision(theObject.ImpactStatus))
                _loopTracker.MarkCollision();

            if (isExploding)
                return UpdateExplodingShip(theObject);

            var inputSettings = GetNormalizedSettings();
            ApplyActiveControlSchemeInputGuards(inputSettings);
            ApplyXboxControllerInput(inputSettings);

            if (inputSettings.ActiveControlScheme == ControlInputMode.Mouse)
            {
                ApplyMouseTargetRotation();
            }
            else
            {
                var biomePhysics = BiomePhysicsSetup.CurrentProfile;
                float rotationAcceleration = RotationAcceleration * biomePhysics.RotationAccelerationMultiplier;
                if (_leftHeld) _yawVelocity -= rotationAcceleration * deltaTime;
                if (_rightHeld) _yawVelocity += rotationAcceleration * deltaTime;
                if (_upHeld) _pitchVelocity += rotationAcceleration * deltaTime;
                if (_downHeld) _pitchVelocity -= rotationAcceleration * deltaTime;
                float xboxRotationAcceleration = rotationAcceleration * XboxRotationAccelerationMultiplier;
                if (_xboxYawInput != 0f) _yawVelocity += _xboxYawInput * xboxRotationAcceleration * deltaTime;
                if (_xboxPitchInput != 0f) _pitchVelocity += _xboxPitchInput * xboxRotationAcceleration * deltaTime;

                float rotationDragBase = Math.Clamp(
                    RotationDrag * biomePhysics.RotationRetentionMultiplier,
                    0.01f,
                    0.999f);
                float rotationDrag = GameState.ScaleDampingPer90Frame(rotationDragBase);
                float maxRotationSpeed = inputSettings.ActiveControlScheme == ControlInputMode.XboxController
                    ? MaxRotationSpeed * XboxMaxRotationSpeedMultiplier
                    : MaxRotationSpeed;
                _yawVelocity = MathF.Max(-maxRotationSpeed, MathF.Min(maxRotationSpeed, _yawVelocity)) * rotationDrag;
                _pitchVelocity = MathF.Max(-maxRotationSpeed, MathF.Min(maxRotationSpeed, _pitchVelocity)) * rotationDrag;

                _yawAccumulator += _yawVelocity * deltaTime;
                _pitchAccumulator += _pitchVelocity * deltaTime;

                int yawStep = (int)_yawAccumulator;
                int pitchStep = (int)_pitchAccumulator;
                if (yawStep != 0) { rotationZ += yawStep; _yawAccumulator -= yawStep; }
                if (pitchStep != 0) { tilt += pitchStep; _pitchAccumulator -= pitchStep; }
            }

            // Gently return tilt toward level-flight angle when no pitch input is held.
            // LevelFlightTilt gives a slight forward lean so the ship cruises naturally
            // rather than stopping dead in a hover.
            // Disabled: needs playtesting before enabling.
            //const int LevelFlightTilt = 8;
            //const float TiltReturnRate = 0.04f;
            //if (!_upHeld && !_downHeld && tilt != LevelFlightTilt)
            //{
            //    int diff = tilt - LevelFlightTilt;
            //    int step = (int)(diff * TiltReturnRate);
            //    if (step == 0 && diff != 0) step = Math.Sign(diff);
            //    tilt -= step;
            //}

            if (_cannonRecoilOffset > 0f)
            {
                _cannonRecoilOffset -= _cannonRecoilReturnSpeed * deltaTime;
                if (_cannonRecoilOffset < 0f) _cannonRecoilOffset = 0f;
            }

            if (_fireKeyHeld && !isExploding)
                FireWeapon();

            if (ThrustOn)
            {
                ResetSurfaceBounceState();
                ResetUnsafeSurfaceHit();
                landed = false;
                Physics.ResetHover();
                IncreaseThrustAndRelease();
                HandleThrust(deltaTime);
            }

            if (Thrust == 0 && !isExploding && !IsShipGravitySuppressed())
                ApplyGravity(deltaTime);

            HandleCompletedLoopBonus(UpdateLoopTracking(deltaTime));

            if (!isExploding && !shipCrashDetectionSuppressed && IsBelowSurfaceFailsafeFloor())
            {
                if (theObject.ImpactStatus == null)
                    theObject.ImpactStatus = new ImpactStatus();

                theObject.ImpactStatus.ObjectName = "Surface";
                theObject.ImpactStatus.ObjectHealth = 0;
                theObject.ImpactStatus.HasCrashed = false;
                StartShipExplosion(theObject);
                return UpdateExplodingShip(theObject);
            }

            if (ParentObject.Particles?.Particles.Count > 0)
                ParentObject.Particles.MoveParticles();

            if (theObject.ObjectOffsets != null)
            {
                theObject.ObjectOffsets.y = Physics.ClampToScreenDrop(Physics.ClampToHeightRange(ParentObject.ObjectOffsets.y));
                theObject.ObjectOffsets.z = zoom;
            }

            if (_rocketInstance != null)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();
                _rocketInstance.SetWorldPosition(new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z));
            }

            GameState.GamePlayState.UpdateAltitude(
                GameState.SurfaceState.GlobalMapPosition.y,
                Physics.FloorHeight,
                Physics.CeilingHeight);

            HandleLowAltitudeRunBonus(deltaTime);

            if (!isExploding)
            {
                ApplyLocalTiltToMesh(tilt, theObject);
                ApplyCannonRecoil(theObject);
                UpdateMuzzleFlash(theObject, deltaTime);
                UpdateEngineColors(theObject);
            }

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = rotationX;
                theObject.Rotation.z = rotationZ;
            }

            if (theObject.ImpactStatus.HasCrashed == true && isExploding == false)
            {
                float landingSpeed = CurrentSpeed;
                string crashedWith = theObject.ImpactStatus.ObjectName;

                if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] HasCrashed=true, ObjectName='{crashedWith}', Health={theObject.ImpactStatus.ObjectHealth}, Direction={theObject.ImpactStatus.ImpactDirection}");

                int healthBeforeCrash = theObject.ImpactStatus.ObjectHealth ?? 0;

                // Apply damage from any enemy or weapon collision
                if (crashedWith == "PowerUp")
                {
                    // No damage — collect the powerup; skip health/explosion check
                    CollectPowerUp(theObject);
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] PowerUp collected!");
                    theObject.ImpactStatus.HasCrashed = false;
                    return theObject;
                }
                else if (crashedWith == "MotherShipSmall")
                {
                    if ((DateTime.Now - _motherShipCollisionCooldown).TotalSeconds < MotherShipCollisionCooldownSeconds)
                    {
                        theObject.ImpactStatus.HasCrashed = false;
                        return theObject;
                    }
                    _motherShipCollisionCooldown = DateTime.Now;
                    int ramDamage = ShipSetup.DefaultShipHealth / 2;
                    theObject.ImpactStatus.ObjectHealth -= ramDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] MotherShip ram! Damage={ramDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (crashedWith == "BomberBomb")
                {
                    theObject.ImpactStatus.ObjectHealth -= EnemySetup.BomberBombCollisionDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] BomberBomb hit! Damage={EnemySetup.BomberBombCollisionDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (EnemySetup.IsEnemyTypeValid(crashedWith))
                {
                    theObject.ImpactStatus.ObjectHealth -= EnemySetup.KamikazeDroneCollisionDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] Enemy hit! Damage={EnemySetup.KamikazeDroneCollisionDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (WeaponSetup.IsWeaponTypeValid(crashedWith))
                {
                    int weaponDamage = WeaponSetup.GetWeaponDamage(crashedWith);
                    theObject.ImpactStatus.ObjectHealth -= weaponDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] Weapon hit! Damage={weaponDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (crashedWith == "EnemyLazer")
                {
                    int weaponDamage = WeaponSetup.GetWeaponDamage("Lazer");
                    theObject.ImpactStatus.ObjectHealth -= weaponDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] EnemyLazer hit! Damage={weaponDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (crashedWith == "EnemyLazerMedium")
                {
                    int weaponDamage = WeaponSetup.GetWeaponDamage("Lazer") * 2;
                    theObject.ImpactStatus.ObjectHealth -= weaponDamage;
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] EnemyLazerMedium hit! Damage={weaponDamage}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else if (crashedWith == "Surface" ||
                         theObject.ImpactStatus.ImpactDirection == ImpactDirection.Top ||
                         theObject.ImpactStatus.ImpactDirection == ImpactDirection.Center)
                {
                    bool overLandingPlatform = IsShipOverLandingPlatform();

                    if (overLandingPlatform)
                    {
                        ResetUnsafeSurfaceHit();
                        BeginSurfaceBounceRecovery(reenableGravityAtTop: false);
                    }
                    else
                    {
                        if (IsUnsafeSurfaceHitActive(now))
                        {
                            StartShipExplosion(theObject);
                            theObject.ImpactStatus.HasCrashed = false;
                            return UpdateExplodingShip(theObject);
                        }

                        ArmUnsafeSurfaceHit(now);
                        BeginSurfaceBounceRecovery(reenableGravityAtTop: true);

                        int landingDamage = CalculateSurfaceLandingDamage(landingSpeed);
                        theObject.ImpactStatus.ObjectHealth -= landingDamage;
                    }

                    if (theObject.ImpactStatus.ObjectHealth > 0)
                        PlaySurfaceThud(theObject);

                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] Landing. Speed={landingSpeed:F1}, NewHealth={theObject.ImpactStatus.ObjectHealth}");
                }
                else
                {
                    if (Logger.ShouldLog(logging)) Logger.Log($"[ShipCrash] NO DAMAGE APPLIED! crashedWith='{crashedWith}' did not match any category.");
                }

                // Play impact thud for non-fatal combat collisions that actually dealt damage
                bool isSurfaceHit = crashedWith == "Surface" ||
                                    theObject.ImpactStatus.ImpactDirection == ImpactDirection.Top ||
                                    theObject.ImpactStatus.ImpactDirection == ImpactDirection.Center;
                if (!isSurfaceHit && theObject.ImpactStatus.ObjectHealth > 0 && theObject.ImpactStatus.ObjectHealth < healthBeforeCrash)
                    PlayImpactThud(theObject);

                if (theObject.ImpactStatus.ObjectHealth <= 0)
                {
                    StartShipExplosion(theObject);
                    theObject.ImpactStatus.HasCrashed = false;
                    return UpdateExplodingShip(theObject);
                }

                theObject.ImpactStatus.HasCrashed = false;
            }

            if (Logger.ShouldLog(enableMovementDiagnostics))
            {
                movementLogCounter++;
                if (movementLogCounter % 60 == 0)
                {
                    var mapPos = GameState.SurfaceState.GlobalMapPosition;
                    Logger.Log($"[MoveDiag] map=({mapPos.x:0.##},{mapPos.y:0.##},{mapPos.z:0.##}) offsets=({theObject.ObjectOffsets.x:0.##},{theObject.ObjectOffsets.y:0.##},{theObject.ObjectOffsets.z:0.##}) thrustOn={ThrustOn} thrust={Thrust:0.##}");
                }
            }

            UpdateShipWorldPosition(theObject);

            // The weapon needs to move as well 
            if (theObject.WeaponSystems != null)
                theObject.WeaponSystems.MoveWeapon(audioPlayer, soundRegistry);

            return theObject;
        }

        private I3dObject UpdateExplodingShip(I3dObject theObject)
        {
            StopShipMotionForExplosion();
            GameState.GamePlayState.Thrust = Thrust;
            GameState.GamePlayState.UpdateAltitude(
                GameState.SurfaceState.GlobalMapPosition.y,
                Physics.FloorHeight,
                Physics.CeilingHeight);

            RestoreExplosionTransform(theObject);
            RestoreExplosionTransform(ParentObject);

            Physics.UpdateExplosion(theObject, ExplosionDeltaTime);
            RestoreExplosionTransform(theObject);
            RestoreExplosionTransform(ParentObject);

            if (theObject.ImpactStatus?.HasExploded == true)
            {
                theObject.ObjectParts = new List<I3dObjectPart>();
            }

            if (theObject.Particles?.Particles.Count > 0)
                theObject.Particles.MoveParticles();

            UpdateShipWorldPosition(theObject);
            return theObject;
        }

        private bool IsBelowSurfaceFailsafeFloor()
        {
            return GameState.SurfaceState.GlobalMapPosition.y < Physics.FloorHeight;
        }

        private static bool IsShipCrashDetectionSuppressed()
        {
            return GameState.ShipState.ShipCrashDetectionDisabledUntilUtc > DateTime.UtcNow;
        }

        private static bool IsShipGravitySuppressed()
        {
            return GameState.ShipState.ShipGravityDisabledUntilUtc > DateTime.UtcNow;
        }

        private ShipLoopStatus UpdateLoopTracking(float deltaTime)
        {
            return _loopTracker.Update(tilt, isAirborne: !landed, ThrustOn, deltaTime);
        }

        private void HandleCompletedLoopBonus(ShipLoopStatus loopStatus)
        {
            if (!loopStatus.Completed)
                return;

            int requestedScore = loopStatus.HadCollision
                ? GameSetup.CollisionLoopStyleBonusScore
                : GameSetup.CleanLoopStyleBonusScore;
            int awardedScore = GameState.GamePlayState.AwardStyleBonus(requestedScore);
            if (awardedScore <= 0)
                return;

            var gameplay = GameState.GamePlayState;
            if (gameplay.CurrentSceneType != SceneTypes.Tutorial)
            {
                GameState.EventBus?.Publish(new GameEvent
                {
                    Type = GameEventType.StyleBonusAwarded,
                    ObjectName = "Ship",
                    SceneType = gameplay.CurrentSceneType,
                    SceneIndex = gameplay.SceneIndex,
                    Score = gameplay.Score,
                    AwardedScore = awardedScore,
                    StyleBonusType = loopStatus.HadCollision
                        ? StyleBonusTypes.CollisionLoop
                        : StyleBonusTypes.CleanLoop,
                    TotalKills = gameplay.TotalKills,
                    TotalShotsFired = gameplay.TotalShotsFired,
                    TotalDeaths = gameplay.TotalDeaths,
                    Accuracy = gameplay.Accuracy,
                    PowerUpsCollected = gameplay.PowerUpsCollected,
                    SpeedPowerUpLevel = gameplay.SpeedPowerUpLevel,
                    HadCollision = loopStatus.HadCollision
                });
            }

            var cue = loopStatus.HadCollision
                ? ShipAiVoiceCue.CollisionLoop
                : ShipAiVoiceCue.CleanLoop;

            if (GameState.GamePlayState.PlanetStyleBonusRemaining <= 0)
                cue = ShipAiVoiceCue.PlanetBonusComplete;

            _shipAiVoiceService.TrySpeak(cue, _audio, _soundRegistry);
        }

        private void HandleLowAltitudeRunBonus(float deltaTime)
        {
            if (!IsLowAltitudeRunActive())
            {
                _lowAltitudeRunSeconds = 0f;
                return;
            }

            _lowAltitudeRunSeconds += MathF.Max(0f, deltaTime);
            if (_lowAltitudeRunSeconds < GameSetup.LowAltitudeRunRequiredSeconds)
                return;

            _lowAltitudeRunSeconds = 0f;

            var gameplay = GameState.GamePlayState;
            int awardedScore = gameplay.AwardStyleBonus(GameSetup.LowAltitudeRunStyleBonusScore);
            if (awardedScore <= 0)
                return;

            GameState.EventBus?.Publish(new GameEvent
            {
                Type = GameEventType.StyleBonusAwarded,
                ObjectName = "Ship",
                SceneType = gameplay.CurrentSceneType,
                SceneIndex = gameplay.SceneIndex,
                Score = gameplay.Score,
                AwardedScore = awardedScore,
                StyleBonusType = StyleBonusTypes.LowAltitudeRun,
                TotalKills = gameplay.TotalKills,
                TotalShotsFired = gameplay.TotalShotsFired,
                TotalDeaths = gameplay.TotalDeaths,
                Accuracy = gameplay.Accuracy,
                PowerUpsCollected = gameplay.PowerUpsCollected,
                SpeedPowerUpLevel = gameplay.SpeedPowerUpLevel,
                HadCollision = false
            });

            var cue = gameplay.PlanetStyleBonusRemaining <= 0
                ? ShipAiVoiceCue.PlanetBonusComplete
                : ShipAiVoiceCue.LowAltitudeRisk;

            _shipAiVoiceService.TrySpeak(cue, _audio, _soundRegistry);
        }

        private bool IsLowAltitudeRunActive()
        {
            var gameplay = GameState.GamePlayState;
            if (gameplay.CurrentSceneType == SceneTypes.Tutorial || isExploding || landed)
                return false;

            float altitude = GameState.SurfaceState.GlobalMapPosition.y;
            if (altitude < GameSetup.LowAltitudeRunMinHeight ||
                altitude > GameSetup.LowAltitudeRunMaxHeight)
                return false;

            float horizontalSpeed = MathF.Sqrt(
                Physics.InertiaX * Physics.InertiaX +
                Physics.InertiaZ * Physics.InertiaZ);

            return horizontalSpeed >= GameSetup.LowAltitudeRunMinHorizontalSpeed;
        }

        private static bool IsLoopCollision(IImpactStatus? impactStatus)
        {
            return impactStatus?.HasCrashed == true &&
                   !string.Equals(impactStatus.ObjectName, "PowerUp", StringComparison.OrdinalIgnoreCase);
        }

        private void StartShipExplosion(I3dObject theObject)
        {
            if (isExploding)
                return;

            if (theObject.ImpactStatus == null)
                theObject.ImpactStatus = new ImpactStatus();

            theObject.ImpactStatus.ObjectHealth = 0;
            CaptureExplosionTransform(theObject);

            // Stop the rocket loop before the explosion starts.
            if (_rocketInstance != null)
            {
                _rocketInstance.Stop(playEndSegment: true);
                _rocketInstance = null;
            }

            // Play the ship explosion if audio is configured.
            if (_audio != null && _explosionSound != null)
            {
                var audioPosition = ((_3dObject)theObject).GetAudioPosition();
                _audio.PlayOneShot(
                    _explosionSound,
                    new AudioPlayOptions
                    {
                        VolumeOverride = 1.0f,
                        WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                    });
            }

            // Release some particles at the explosion, set fixed thrust level.
            const int explosionParticleThrust = (int)MaxThrust;
            ResetThrustParticleStyle();
            ParentObject?.Particles?.ReleaseParticles(
                GuideCoordinates,
                StartCoordinates,
                GetExplosionParticleWorldPosition(theObject),
                this,
                explosionParticleThrust,
                true);

            StopShipMotionForExplosion();

            isExploding = true;
            ExplosionDeltaTime = DateTime.Now;

            var explodedVersion = Physics.ExplodeObject(theObject, 200f);
            ParentObject = explodedVersion;
            RestoreExplosionTransform(theObject);
            RestoreExplosionTransform(ParentObject);
        }

        public float CurrentSpeed => Physics.CalculateCurrentSpeed(landed);

        private void BeginSurfaceBounceRecovery(bool reenableGravityAtTop)
        {
            landed = true;
            _surfaceBounceWaitingForGravity = reenableGravityAtTop;
            Physics.InertiaY = 0f;
            Physics.InertiaX *= 0.3f;
            Physics.InertiaZ *= 0.3f;
        }

        private void EnableGravityAfterSurfaceBounce()
        {
            landed = false;
            ResetSurfaceBounceState();
            Physics.InertiaY = MathF.Min(Physics.InertiaY, 0f);
            Physics.HoverElapsed = Physics.HoverFloatDuration + Physics.HoverRampDuration;
        }

        private bool HasReachedSurfaceBounceTop()
        {
            return MathF.Abs(ParentObject.ObjectOffsets.y - ShipRestingScreenY) <= SurfaceBounceTopTolerance &&
                   MathF.Abs(GameState.SurfaceState.GlobalMapPosition.y) <= SurfaceBounceTopTolerance;
        }

        private void ArmUnsafeSurfaceHit(DateTime now)
        {
            _unsafeSurfaceHitArmed = true;
            _unsafeSurfaceHitAt = now;
        }

        private bool IsUnsafeSurfaceHitActive(DateTime now)
        {
            ExpireUnsafeSurfaceHit(now);
            return _unsafeSurfaceHitArmed;
        }

        private void ExpireUnsafeSurfaceHit(DateTime now)
        {
            if (!_unsafeSurfaceHitArmed)
                return;

            if ((now - _unsafeSurfaceHitAt).TotalSeconds >= UnsafeSurfaceHitResetSeconds)
                ResetUnsafeSurfaceHit();
        }

        private void ResetUnsafeSurfaceHit()
        {
            _unsafeSurfaceHitArmed = false;
            _unsafeSurfaceHitAt = DateTime.MinValue;
        }

        private static bool IsShipOverLandingPlatform()
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return false;

            int tileSize = SurfaceSetup.tileSize;
            int viewportCenterOffset = (SurfaceSetup.viewPortSize * tileSize) / 2;
            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            int shipTileX = MapCoordinateHelpers.WorldToTileIndex(
                mapPosition.x + viewportCenterOffset,
                tileSize,
                map.GetLength(1));
            int shipTileZ = MapCoordinateHelpers.WorldToTileIndex(
                mapPosition.z + viewportCenterOffset,
                tileSize,
                map.GetLength(0));

            return LandingPlatformHelpers.IsLandingPlatformTile(map, shipTileX, shipTileZ);
        }

        private void CaptureExplosionTransform(I3dObject theObject)
        {
            var snapshotSource = ParentObject ?? theObject;
            _hasExplosionTransformSnapshot = true;
            _explosionWorldPosition = ToVector3(snapshotSource.WorldPosition);
            _explosionObjectOffsets = ToVector3(snapshotSource.ObjectOffsets);
            _explosionRotation = ToVector3(snapshotSource.Rotation);
            _explosionCalculatedCrashOffset = ToVector3(snapshotSource.CalculatedCrashOffset);
        }

        private void RestoreExplosionTransform(I3dObject? theObject)
        {
            if (!_hasExplosionTransformSnapshot || theObject == null)
                return;

            theObject.WorldPosition = ToVector3(_explosionWorldPosition);
            theObject.ObjectOffsets = ToVector3(_explosionObjectOffsets);
            theObject.Rotation = ToVector3(_explosionRotation);
            theObject.CalculatedCrashOffset = ToVector3(_explosionCalculatedCrashOffset);
        }

        private Vector3 GetExplosionParticleWorldPosition(I3dObject theObject)
        {
            var worldPosition = _explosionWorldPosition ?? ToVector3(theObject.WorldPosition);
            if (worldPosition == null)
                return new Vector3();

            return new Vector3 { x = worldPosition.x, y = worldPosition.y, z = worldPosition.z };
        }

        private static Vector3? ToVector3(IVector3? v)
        {
            if (v is null)
                return null;

            return new Vector3 { x = v.x, y = v.y, z = v.z };
        }

        private void StopShipMotionForExplosion()
        {
            ResetSurfaceBounceState();
            ResetUnsafeSurfaceHit();
            _loopTracker.CancelActiveLoop();
            ThrustOn = false;
            _fireKeyHeld = false;
            Thrust = 0f;
            Physics.InertiaX = 0f;
            Physics.InertiaY = 0f;
            Physics.InertiaZ = 0f;
            Physics.FallVelocity = 0f;
            Physics.ThrustEffect = 0f;
            Physics.VerticalLiftFactor = 0f;
            ResetThrustParticleStyle();
            ResetThrustBurstBoost();
            HideMuzzleFlash(ParentObject);
        }

        private void ResetSurfaceBounceState()
        {
            _surfaceBounceWaitingForGravity = false;
        }

        private static int CalculateSurfaceLandingDamage(float landingSpeed)
        {
            float speed = MathF.Max(0f, landingSpeed);

            if (speed <= SurfaceLandingDamageSpeedThreshold)
                return Math.Max(
                    MinSurfaceLandingDamage,
                    (int)MathF.Ceiling(MinSurfaceLandingDamage + speed * LowSpeedSurfaceLandingDamageMultiplier));

            return Math.Max(MinSurfaceLandingDamage, (int)(speed * HighSpeedSurfaceLandingDamageMultiplier));
        }

        public void ApplyLocalTiltToMesh(int tilt, I3dObject inhabitant)
        {
            if (ParentObject == null) return;

            float radians = tilt * DEG2RAD;
            float cos = MathF.Cos(radians);
            float sin = MathF.Sin(radians);

            foreach (var part in inhabitant.ObjectParts)
            {
                for (int i = 0; i < part.Triangles.Count; i++)
                {
                    var tri = part.Triangles[i];
                    tri.vert1 = RotateAroundX((Vector3)tri.vert1, cos, sin);
                    tri.vert2 = RotateAroundX((Vector3)tri.vert2, cos, sin);
                    tri.vert3 = RotateAroundX((Vector3)tri.vert3, cos, sin);
                    part.Triangles[i] = tri;
                }
            }

            for (int i = 0; i < inhabitant.CrashBoxes.Count; i++)
            {
                var crashbox = inhabitant.CrashBoxes[i];
                for (int j = 0; j < crashbox.Count; j++)
                {
                    crashbox[j] = RotateAroundX((Vector3)crashbox[j], cos, sin);
                }
            }
        }

        private Vector3 RotateAroundX(Vector3 point, float cos, float sin)
        {
            float y = point.y * cos - point.z * sin;
            float z = point.y * sin + point.z * cos;
            return new Vector3(point.x, y, z);
        }

        private void ApplyCannonRecoil(I3dObject theObject)
        {
            if (_cannonRecoilOffset <= 0f) return;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName != "TopCannon") continue;

                for (int i = 0; i < part.Triangles.Count; i++)
                {
                    var tri = part.Triangles[i];
                    var v1 = (Vector3)tri.vert1;
                    var v2 = (Vector3)tri.vert2;
                    var v3 = (Vector3)tri.vert3;
                    tri.vert1 = new Vector3 { x = v1.x, y = v1.y + _cannonRecoilOffset, z = v1.z };
                    tri.vert2 = new Vector3 { x = v2.x, y = v2.y + _cannonRecoilOffset, z = v2.z };
                    tri.vert3 = new Vector3 { x = v3.x, y = v3.y + _cannonRecoilOffset, z = v3.z };
                    part.Triangles[i] = tri;
                }
                break;
            }
        }

        private void TriggerMuzzleFlash(WeaponType selectedWeapon)
        {
            if (ParentObject == null)
                return;

            if (selectedWeapon != WeaponType.Bullet && selectedWeapon != WeaponType.Lazer)
                return;

            if (!ShouldUseMuzzleFlash())
            {
                HideMuzzleFlash(ParentObject);
                return;
            }

            _muzzleFlashSecondsLeft = selectedWeapon == WeaponType.Lazer
                ? MuzzleFlashDurationSeconds * 1.25f
                : MuzzleFlashDurationSeconds;

            _muzzleFlashColor = selectedWeapon == WeaponType.Lazer
                ? LazerMuzzleFlashColor
                : BulletMuzzleFlashColor;

            SetPartColor(ParentObject, MuzzleFlashPartName, _muzzleFlashColor);
            SetPartVisibility(ParentObject, MuzzleFlashPartName, true);
        }

        private void UpdateMuzzleFlash(I3dObject theObject, float deltaTime)
        {
            if (!ShouldUseMuzzleFlash())
            {
                HideMuzzleFlash(theObject);
                return;
            }

            if (_muzzleFlashSecondsLeft <= 0f)
            {
                SetPartVisibility(theObject, MuzzleFlashPartName, false);
                return;
            }

            _muzzleFlashSecondsLeft -= MathF.Max(0f, deltaTime);
            if (_muzzleFlashSecondsLeft <= 0f)
            {
                HideMuzzleFlash(theObject);
                return;
            }

            SetPartColor(theObject, MuzzleFlashPartName, _muzzleFlashColor);
            SetPartVisibility(theObject, MuzzleFlashPartName, true);
        }

        private void HideMuzzleFlash(I3dObject? theObject)
        {
            _muzzleFlashSecondsLeft = 0f;
            SetPartVisibility(theObject, MuzzleFlashPartName, false);
        }

        private static bool ShouldUseMuzzleFlash()
        {
            return GameState.SettingsState?.GraphicsQuality != GraphicsQualityPreset.Low;
        }

        public void HandleThrust(float deltaTime)
        {
            float verticalInertia = Physics.CalculateThrustForces(Thrust, tilt, rotationZ, deltaTime);
            float frameScale = GameState.FrameScale90;
            float travelSpeedMultiplier = GameState.GamePlayState.TravelSpeedMultiplier *
                                          BiomePhysicsSetup.CurrentProfile.TravelSpeedMultiplier *
                                          ConsumeThrustBurstTravelBoost(deltaTime);

            float maxX = (ParentObject.ParentSurface.GlobalMapSize() * ParentObject.ParentSurface.TileSize()) -
                         (ParentObject.ParentSurface.ViewPortSize() * ParentObject.ParentSurface.TileSize());
            float maxZ = (ParentObject.ParentSurface.GlobalMapSize() * ParentObject.ParentSurface.TileSize()) -
                         (ParentObject.ParentSurface.ViewPortSize() * ParentObject.ParentSurface.TileSize());

            GameState.SurfaceState.GlobalMapPosition.x = Physics.WrapPosition(GameState.SurfaceState.GlobalMapPosition.x, Physics.InertiaX * frameScale * travelSpeedMultiplier, 75, maxX);
            GameState.SurfaceState.GlobalMapPosition.z = Physics.WrapPosition(GameState.SurfaceState.GlobalMapPosition.z, Physics.InertiaZ * frameScale * travelSpeedMultiplier, 0, maxZ);

            // Apply vertical inertia to screen position (positive InertiaY = up = ObjectOffsets.y decreases)
            ParentObject.ObjectOffsets.y = Physics.ClampToScreenDrop(Physics.ClampToHeightRange(ParentObject.ObjectOffsets.y - verticalInertia * frameScale));
            // Apply vertical inertia to altitude (positive InertiaY = up = altitude increases)
            // Ceiling-only clamp: FloorHeight must not cap gmpY or the gravity-settle
            // equilibrium oscillates against the clamp, causing surface vibration.
            GameState.SurfaceState.GlobalMapPosition.y = MathF.Min(GameState.SurfaceState.GlobalMapPosition.y + verticalInertia * frameScale, Physics.CeilingHeight);
        }

        public void ApplyGravity(float deltaTime)
        {
            if (landed && !ThrustOn)
            {
                // Smoothly settle screen position and altitude to resting values after landing.
                // Uses proportional easing capped at MaxSettleSpeed to prevent
                // visually jarring jumps when returning from high altitude.
                const float MaxSettleSpeed = 300f;
                float screenDiff = ShipRestingScreenY - ParentObject.ObjectOffsets.y;
                float altDiff = -GameState.SurfaceState.GlobalMapPosition.y;
                float settleRate = 12f;
                float t = MathF.Min(settleRate * deltaTime, 1f);
                float maxStep = MaxSettleSpeed * deltaTime;

                if (MathF.Abs(screenDiff) > 0.5f)
                {
                    float screenStep = screenDiff * t;
                    if (MathF.Abs(screenStep) > maxStep)
                        screenStep = maxStep * MathF.Sign(screenDiff);
                    ParentObject.ObjectOffsets.y += screenStep;
                }
                else
                    ParentObject.ObjectOffsets.y = ShipRestingScreenY;

                if (MathF.Abs(altDiff) > 0.5f)
                {
                    float altStep = altDiff * t;
                    if (MathF.Abs(altStep) > maxStep)
                        altStep = maxStep * MathF.Sign(altDiff);
                    GameState.SurfaceState.GlobalMapPosition.y += altStep;
                }
                else
                    GameState.SurfaceState.GlobalMapPosition.y = 0f;

                if (_surfaceBounceWaitingForGravity && HasReachedSurfaceBounceTop())
                {
                    EnableGravityAfterSurfaceBounce();
                }
                else
                {
                    return;
                }
            }

            float verticalInertia = Physics.ApplyFallGravity(rotationX, deltaTime);
            float frameScale = GameState.FrameScale90;
            ParentObject.ObjectOffsets.y = Physics.ClampToScreenDrop(Physics.ClampToHeightRange(ParentObject.ObjectOffsets.y - verticalInertia * frameScale));
            // Ceiling-only clamp: FloorHeight must not cap gmpY or the gravity-settle
            // equilibrium oscillates against the clamp, causing surface vibration.
            GameState.SurfaceState.GlobalMapPosition.y = MathF.Min(GameState.SurfaceState.GlobalMapPosition.y + verticalInertia * frameScale, Physics.CeilingHeight);

            // Gently pull screen position and altitude back toward resting values
            const float MaxAirSettleSpeed = 300f;
            float airSettle = MathF.Min(Physics.AirborneSettleRate * deltaTime, 1f);
            float airScreenDiff = ShipRestingScreenY - ParentObject.ObjectOffsets.y;
            float airAltDiff = -GameState.SurfaceState.GlobalMapPosition.y;
            float airMaxStep = MaxAirSettleSpeed * deltaTime;

            if (MathF.Abs(airScreenDiff) > 0.5f)
            {
                float airScreenStep = airScreenDiff * airSettle;
                if (MathF.Abs(airScreenStep) > airMaxStep)
                    airScreenStep = airMaxStep * MathF.Sign(airScreenDiff);
                ParentObject.ObjectOffsets.y += airScreenStep;
            }

            if (MathF.Abs(airAltDiff) > 0.5f)
            {
                float airAltStep = airAltDiff * airSettle;
                if (MathF.Abs(airAltStep) > airMaxStep)
                    airAltStep = airMaxStep * MathF.Sign(airAltDiff);
                GameState.SurfaceState.GlobalMapPosition.y += airAltStep;
            }
        }

        /// <summary>
        /// Decomposes current thrust into vertical (altitude) and forward components
        /// based on the ship's tilt angle. Returns normalised fractions in [0, 1].
        /// </summary>
        private (float vertical, float forward) GetThrustComponents()
        {
            if (!ThrustOn || Thrust <= 0f)
                return (0f, 0f);

            float thrustNormalized = Thrust / MaxThrust;
            float tiltRad = tilt * DEG2RAD;
            float vertical = thrustNormalized * MathF.Abs(MathF.Cos(tiltRad));
            float forward  = thrustNormalized * MathF.Abs(MathF.Sin(tiltRad));
            return (vertical, forward);
        }

        /// <summary>
        /// Sets the JetMotor (lower engine) and RearEngine colors based on
        /// the current vertical and forward thrust fractions.
        /// </summary>
        private void UpdateEngineColors(I3dObject theObject)
        {
            var (verticalThrust, forwardThrust) = GetThrustComponents();
            string lowerColor = InterpolateEngineColor(verticalThrust);
            string rearColor  = InterpolateEngineColor(forwardThrust);
            SetPartColor(theObject, "JetMotor", lowerColor);
            SetPartColor(theObject, "RearEngine", rearColor);
        }

        /// <summary>
        /// Linearly interpolates between the idle (dark red) and active (yellow)
        /// engine colors. <paramref name="t"/> is clamped to [0, 1].
        /// </summary>
        private static string InterpolateEngineColor(float t)
        {
            t = MathF.Max(0f, MathF.Min(1f, t));
            float flicker = (_flickerRng.NextSingle() - 0.5f) * 2f * EngineFlickerAmount;
            t = MathF.Max(0f, MathF.Min(1f, t + flicker));
            int r = (int)(EngineIdleRgb.r + (EngineActiveRgb.r - EngineIdleRgb.r) * t);
            int g = (int)(EngineIdleRgb.g + (EngineActiveRgb.g - EngineIdleRgb.g) * t);
            int b = (int)(EngineIdleRgb.b + (EngineActiveRgb.b - EngineIdleRgb.b) * t);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static void SetPartColor(I3dObject theObject, string partName, string color)
        {
            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName != partName)
                    continue;

                for (int i = 0; i < part.Triangles.Count; i++)
                {
                    var tri = part.Triangles[i];
                    tri.Color = color;
                    part.Triangles[i] = tri;
                }
                break;
            }
        }

        private static void SetPartVisibility(I3dObject? theObject, string partName, bool isVisible)
        {
            if (theObject == null)
                return;

            foreach (var part in theObject.ObjectParts)
            {
                if (part.PartName != partName)
                    continue;

                part.IsVisible = isVisible;
                break;
            }
        }

        public void Dispose()
        {
            if (_rocketInstance != null)
            {
                //Try to force-stop the rocket sound immediately
                _rocketInstance.Stop(playEndSegment: false);
                _rocketInstance = null;
            }

            var hook = InputManager.SharedHook;

            hook.KeyDown -= GlobalHookKeyDown;
            hook.KeyUp -= GlobalHookKeyUp;
            hook.MouseMove -= GlobalHookMouseMovement;
            hook.MouseDownExt -= GlobalHookMouseDownExt;
            hook.MouseUpExt -= GlobalHookMouseUpExt;
            hook.MouseWheelExt -= GlobalHookMouseWheel;
            hook.MouseHWheelExt -= GlobalHookMouseWheel;

            //When disposing, reset the global map position to default
            GameState.SurfaceState.GlobalMapPosition = new Vector3 { x = SurfaceSetup.DefaultMapPosition.x, y = SurfaceSetup.DefaultMapPosition.y, z = SurfaceSetup.DefaultMapPosition.z };
        }

        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord)
        {
            if (StartCoord != null) WeaponStartCoordinates = StartCoord;
            if (GuideCoord != null) WeaponGuideCoordinates = GuideCoord;
        }

        public void ReleaseParticles(I3dObject theObject)
        {
            throw new NotImplementedException();
        }

        private void PlayImpactThud(I3dObject theObject)
        {
            if (_audio == null || _impactThudSound == null) return;

            var audioPosition = ((_3dObject)theObject).GetAudioPosition();
            _audio.PlayOneShot(
                _impactThudSound,
                new AudioPlayOptions
                {
                    VolumeOverride = 1.0f,
                    WorldPosition = new System.Numerics.Vector3(audioPosition.x, audioPosition.y, audioPosition.z)
                });
        }

        private void PlaySurfaceThud(I3dObject theObject)
        {
            if (_audio == null || _surfaceThudSound == null) return;

            _audio.PlayOneShot(
                _surfaceThudSound,
                new AudioPlayOptions { VolumeOverride = 1.0f });
        }

        private void CollectPowerUp(I3dObject ship)
        {
            var aiObjects = GameState.SurfaceState.AiObjects;
            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (obj.ObjectName == "PowerUp" && obj.ImpactStatus?.HasCrashed == true)
                {
                    if (!_collectedPowerUpIds.Add(obj.ObjectId))
                        continue;

                    obj.CrashBoxes = new List<List<IVector3>>();

                    bool isTutorialScene = GameState.GamePlayState.CurrentSceneType == SceneTypes.Tutorial;
                    var gameplay = GameState.GamePlayState;
                    bool isSpeedPowerUp = obj.PowerUpType != PowerUpType.Standard;
                    bool progressionChanged;
                    if (isSpeedPowerUp)
                    {
                        progressionChanged = gameplay.ApplySpeedPowerUp(obj.PowerUpType);
                    }
                    else
                    {
                        gameplay.PowerUpsCollected++;
                        progressionChanged = true;
                    }

                    if (!isTutorialScene)
                    {
                        if (progressionChanged)
                            gameplay.Score += GameSetup.PowerUpCollectScore;

                        // Snapshot current remaining objective enemies before saving checkpoint
                        // so reset/load does not restore an already-cleared wave state by mistake.
                        int seedersLeft = 0;
                        int dronesLeft = 0;
                        int motherShipsLeft = 0;
                        for (int j = 0; j < aiObjects.Count; j++)
                        {
                            var enemy = aiObjects[j];
                            if (enemy.ImpactStatus?.HasExploded == true)
                                continue;

                            if (enemy.ObjectName == "Seeder")
                                seedersLeft++;
                            else if (enemy.ObjectName == "KamikazeDrone" && enemy.IsActive)
                                dronesLeft++;
                            else if ((enemy.ObjectName == "MotherShipSmall" || enemy.ObjectName == "MotherShipMedium" || enemy.ObjectName == "MotherShipLarge") && enemy.IsActive)
                                motherShipsLeft++;
                        }

                        gameplay.SeedersRemaining = seedersLeft;
                        gameplay.DronesRemaining = dronesLeft;
                        gameplay.MotherShipsRemaining = motherShipsLeft;
                        gameplay.SaveCheckpoint();

                        try
                        {
                            GameStatePersistence.SaveGameState();
                            _shipAiVoiceService.RequestGameplaySaveConfirmation();
                        }
                        catch { }
                        try { HighscoreService.SubmitFromGamePlay(gameplay); } catch { }

                        GameState.EventBus?.Publish(new GameEvent
                        {
                            Type = GameEventType.PowerUpCollected,
                            Source = obj,
                            ObjectName = obj.ObjectName,
                            PowerUpType = obj.PowerUpType,
                            SceneType = gameplay.CurrentSceneType,
                            SceneIndex = gameplay.SceneIndex,
                            Score = gameplay.Score,
                            TotalKills = gameplay.TotalKills,
                            TotalShotsFired = gameplay.TotalShotsFired,
                            TotalDeaths = gameplay.TotalDeaths,
                            Accuracy = gameplay.Accuracy,
                            PowerUpsCollected = gameplay.PowerUpsCollected,
                            SpeedPowerUpLevel = gameplay.SpeedPowerUpLevel
                        });
                    }

                    if (_audio != null && _powerupSound != null)
                        _audio.Play(_powerupSound, AudioPlayMode.OneShot, new AudioPlayOptions());

                    break;
                }
            }
        }
    }
}
