using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using _3dRotations.World.Objects;
using System.Reflection;
using System.Windows.Forms;
using static CommonUtilities.WeaponHelpers.WeaponHelpers;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class ShipWeaponAudioTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState
        {
            CurrentSceneType = SceneTypes.Game
        };
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.TutorialState = new TutorialRuntimeState();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<_3dObject>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
        GameState.ShipState = new ShipState();
        GameState.SettingsState = new GameSettingsState();
        GameState.DeltaTime = GameState.GameplayBaselineDeltaTime;
    }

    [TestMethod]
    public void RightShift_WhenBulletCooldownActive_DoesNotStartBulletAudioOrProjectile()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.BulletCooldownLeft = 0.1f;

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(0, fixture.Audio.PlayCount,
            "Bullet audio should not start when cooldown prevents an actual shot.");
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count,
            "Cooldown should prevent projectile activation.");
        Assert.IsFalse(GetPart(fixture.Ship, "MuzzleFlash").IsVisible,
            "Muzzle flash should only appear when a projectile is actually activated.");
    }

    [TestMethod]
    public void RightShift_WhenWeaponGuideMissing_DoesNotStartBulletAudioOrConsumeCooldown()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: false);

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(0, fixture.Audio.PlayCount,
            "Bullet audio should wait until the render loop has supplied weapon guides.");
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0f, GameState.GamePlayState.BulletCooldownLeft);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void RightShift_WhenBulletFires_StartsBulletAudioAndProjectileTogether()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(1, fixture.Audio.PlayCount);
        Assert.AreEqual("bullet_main", fixture.Audio.LastDefinitionId);
        // Regression: bullet must play as a single-shot "pow" per trigger press.
        // It was previously a SegmentedLoop, which could get stuck looping endlessly.
        Assert.AreEqual(AudioPlayMode.OneShot, fixture.Audio.LastMode);
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);
        Assert.IsTrue(GetPart(fixture.Ship, "MuzzleFlash").IsVisible);
        AssertPartColor(fixture.Ship, "MuzzleFlash", "fff8c8");
    }

    [TestMethod]
    public void RightShift_WhenLowGraphics_BulletStillFiresButMuzzleFlashStaysHidden()
    {
        GameState.SettingsState.GraphicsQuality = GraphicsQualityPreset.Low;
        using var fixture = CreateReadyShip(withWeaponGuides: true);

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(1, fixture.Audio.PlayCount);
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);
        Assert.IsFalse(GetPart(fixture.Ship, "MuzzleFlash").IsVisible);
    }

    [TestMethod]
    public void RightShift_WhenLaserGuideMissing_DoesNotStartLaserAudioOrConsumeCooldown()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: false);
        GameState.GamePlayState.SelectedWeapon = WeaponType.Lazer;
        GameState.GamePlayState.ActivePowerup = "LAZER";

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(0, fixture.Audio.PlayCount,
            "Laser audio should also wait until an actual projectile can be dispatched.");
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0f, GameState.GamePlayState.LaserCooldownLeft);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void RightShift_WhenLaserFires_StartsLaserAudioAndProjectileTogether()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.SelectedWeapon = WeaponType.Lazer;
        GameState.GamePlayState.ActivePowerup = "LAZER";

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(1, fixture.Audio.PlayCount);
        Assert.AreEqual("lazer_main", fixture.Audio.LastDefinitionId);
        Assert.AreEqual(AudioPlayMode.OneShot, fixture.Audio.LastMode);
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);
        Assert.IsTrue(GetPart(fixture.Ship, "MuzzleFlash").IsVisible);
        AssertPartColor(fixture.Ship, "MuzzleFlash", "bfffff");
    }

    [TestMethod]
    public void KeyDown_WhenCurrentSceneIsIntro_DoesNotFireWeapon()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.CurrentSceneType = SceneTypes.Intro;

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(0, fixture.Audio.PlayCount);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void KeyDown_WhenGameIntroOverlayIsVisible_DoesNotChangeWeapon()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.PowerUpsCollected = 2;
        GameState.GamePlayState.SelectedWeapon = WeaponType.Bullet;
        GameState.GamePlayState.ActivePowerup = "BULLET";
        // The production intro overlay is modal (SetIntroPreset sets IsModal=true),
        // so the input gate must continue to silence input while it is visible.
        GameState.ScreenOverlayState.Type = ScreenOverlayType.Intro;
        GameState.ScreenOverlayState.IsModal = true;
        GameState.ScreenOverlayState.ShowOverlay = true;

        InvokeKeyDown(fixture.Controls, Keys.D3);

        Assert.AreEqual(WeaponType.Bullet, GameState.GamePlayState.SelectedWeapon);
        Assert.AreEqual("BULLET", GameState.GamePlayState.ActivePowerup);
        Assert.AreEqual(0, fixture.Audio.PlayCount);
    }

    [TestMethod]
    public void KeyDown_WhenModalOverlayIsVisible_DoesNotAffectGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
        GameState.ScreenOverlayState.IsModal = true;
        GameState.ScreenOverlayState.ShowOverlay = true;

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);
        InvokeKeyDown(fixture.Controls, Keys.Space);

        Assert.AreEqual(0, fixture.Audio.PlayCount);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
    }

    [TestMethod]
    public void KeyDown_WhenGameplayIsPaused_DoesNotAffectGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.Phase = GamePhase.Paused;

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);
        InvokeKeyDown(fixture.Controls, Keys.Space);

        Assert.AreEqual(0, fixture.Audio.PlayCount);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
    }

    [TestMethod]
    public void MouseDown_WhenGameplayIsPaused_DoesNotAffectGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.Phase = GamePhase.Paused;

        InvokeMouseDown(fixture.Controls, MouseButtons.Left);
        InvokeMouseDown(fixture.Controls, MouseButtons.Right);

        Assert.AreEqual(0, fixture.Audio.PlayCount);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
    }

    [TestMethod]
    public void MouseDown_WhenKeyboardControlIsActive_DoesNotAffectGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);

        InvokeMouseDown(fixture.Controls, MouseButtons.Left);
        InvokeMouseDown(fixture.Controls, MouseButtons.Right);

        Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
        Assert.AreEqual(0, fixture.Audio.PlayCount);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.IsFalse(fixture.Controls.ThrustOn);
    }

    [TestMethod]
    public void MouseDown_WhenMouseControlIsActive_UsesMouseMappings()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;

        InvokeMouseDown(fixture.Controls, MouseButtons.Left);

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);

        InvokeMouseDown(fixture.Controls, MouseButtons.Right);

        Assert.IsTrue(fixture.Controls.ThrustOn);
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void MouseMove_WhenMouseControlIsActive_AllowsForwardPitch()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        InvokeMouseMovement(fixture.Controls, 100, 100);
        InvokeMouseMovement(fixture.Controls, 100, 40);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(fixture.Controls.tilt < 0,
            "Mouse movement upward must rotate the ship forward with the original Virus-style mapping.");
    }

    [TestMethod]
    public void MouseMove_WhenMouseControlIsActive_AllowsBackwardPitch()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        InvokeMouseMovement(fixture.Controls, 100, 100);
        InvokeMouseMovement(fixture.Controls, 100, 160);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(fixture.Controls.tilt > 0,
            "Mouse movement downward must still rotate the ship backward with the original Virus-style mapping.");
    }

    [TestMethod]
    public void RawMouseDelta_WhenMouseControlIsActive_AllowsForwardPitch()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.HandleRawMouseDelta(0, -60);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(fixture.Controls.tilt < 0,
            "Raw mouse delta upward must rotate the ship forward without relying on screen position.");
    }

    [TestMethod]
    public void RawMouseDelta_WhenMouseControlIsActive_AllowsBackwardPitch()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.HandleRawMouseDelta(0, 60);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(fixture.Controls.tilt > 0,
            "Raw mouse delta downward must rotate the ship backward without relying on screen position.");
    }

    [TestMethod]
    public void RawMouseDelta_WhenMouseControlIsActive_UsesLowerPitchSensitivityThanAxisRotation()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.HandleRawMouseDelta(60, 60);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(Math.Abs(fixture.Controls.rotationZ) > Math.Abs(fixture.Controls.tilt),
            "Mouse pitch should be less sensitive than rotation around the ship axis, matching the Virus-style feel.");
    }

    [TestMethod]
    public void RawMouseDelta_WhenMouseControlIsActive_SettlesAtMouseTargetWithoutRotationalInertia()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.HandleRawMouseDelta(0, 60);

        for (int i = 0; i < 20; i++)
            fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        int settledTilt = fixture.Controls.tilt;

        for (int i = 0; i < 20; i++)
            fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(settledTilt, fixture.Controls.tilt,
            "Mouse control should settle at the virtual mouse target instead of drifting on rotation inertia.");
    }

    [TestMethod]
    public void RawMouseDelta_WhenMouseControlIsActive_ClampsSingleLargeDelta()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.DeltaTime = 0.1f;

        fixture.Controls.HandleRawMouseDelta(5000, 5000);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsTrue(Math.Abs(fixture.Controls.rotationZ) <= 25,
            "A single raw mouse spike must not cause a long axis-rotation jump.");
        Assert.IsTrue(Math.Abs(fixture.Controls.tilt) <= 15,
            "A single raw mouse spike must not cause a long pitch jump.");
    }

    [TestMethod]
    public void KeyDown_WhenMouseControlIsActive_StillAllowsKeyboardWeaponSelection()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.ActiveControlScheme = ControlInputMode.Mouse;
        GameState.GamePlayState.PowerUpsCollected = 1;

        InvokeKeyDown(fixture.Controls, Keys.D2);

        Assert.AreEqual("DECOY", GameState.GamePlayState.ActivePowerup);
        Assert.IsFalse(fixture.Controls.ThrustOn);
    }

    [TestMethod]
    public void FireWeapon_WhenDecoyIsSelected_AllowsOnlyOneDeployPerSecond()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true, parentSurface: new Surface());
        GameState.GamePlayState.ActivePowerup = "DECOY";

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);
        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(1, GameState.SurfaceState.AiObjects.Count(obj => obj.ObjectName == "DroneDecoy"),
            "Decoy deployment must be rate-limited so repeated input cannot spawn multiple decoys in one second.");
        Assert.AreEqual(1, GameState.PendingWorldObjects.Count(obj => obj.ObjectName == "DroneDecoy"));

        SetLastDecoyDeployUtc(fixture.Controls, DateTime.UtcNow.AddSeconds(-1.1));

        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.AreEqual(2, GameState.SurfaceState.AiObjects.Count(obj => obj.ObjectName == "DroneDecoy"),
            "A new decoy should be allowed after the one-second cooldown has elapsed.");
    }

    [TestMethod]
    public void KeyDown_WhenPowerupSlot4IsPressed_DoesNotChangeCurrentPowerupYet()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.PowerUpsCollected = 3;
        GameState.GamePlayState.SelectedWeapon = WeaponType.Lazer;
        GameState.GamePlayState.ActivePowerup = "LAZER";

        InvokeKeyDown(fixture.Controls, Keys.D4);

        Assert.AreEqual(WeaponType.Lazer, GameState.GamePlayState.SelectedWeapon);
        Assert.AreEqual("LAZER", GameState.GamePlayState.ActivePowerup);
        Assert.AreEqual(0, fixture.Audio.PlayCount);
    }

    [TestMethod]
    public void KeyDown_WhenKeyboardMappingChanges_UsesConfiguredKey()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.SettingsState.KeyboardThrustKey = "W";

        InvokeKeyDown(fixture.Controls, Keys.Space);
        Assert.IsFalse(fixture.Controls.ThrustOn);

        InvokeKeyDown(fixture.Controls, Keys.W);
        Assert.IsTrue(fixture.Controls.ThrustOn);
    }

    [TestMethod]
    public void ClearGameplayInputForPause_WhenThrustIsHeld_StopsThrustImmediately()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);

        InvokeKeyDown(fixture.Controls, Keys.Space);
        Assert.IsTrue(fixture.Controls.ThrustOn);

        fixture.Controls.ClearGameplayInputForPause();

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
        Assert.AreEqual(0f, fixture.Controls.Physics.ThrustEffect, 0.001f);
        Assert.AreEqual(0f, fixture.Controls.Physics.VerticalLiftFactor, 0.001f);
    }

    [TestMethod]
    public void ResumeFromGameplayPause_PausesGravityForResumeWindow()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        InvokeKeyDown(fixture.Controls, Keys.Space);
        Assert.IsTrue(fixture.Controls.ThrustOn);

        fixture.Controls.ResumeFromGameplayPause(fixture.Ship);

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
        Assert.IsTrue(GameState.ShipState.ShipGravityDisabledUntilUtc > DateTime.UtcNow);
    }

    [TestMethod]
    public void KeyDown_WhenNonModalGameOverlayIsVisible_StillAcceptsGameplayInput()
    {
        // Ordinary non-modal Game overlays should not block controls. Timeline-
        // driven victory reward overlays use a separate pause flag.
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
        GameState.ScreenOverlayState.IsModal = false;
        GameState.ScreenOverlayState.ShowOverlay = true;

        InvokeKeyDown(fixture.Controls, Keys.Space);
        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.IsTrue(fixture.Controls.ThrustOn,
            "Thrust must remain available while a non-modal in-game overlay is visible.");
        Assert.AreEqual(1, fixture.Weapons.ActiveWeapons.Count,
            "Weapons must still fire while a non-modal in-game overlay is visible.");
        Assert.AreEqual(1, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void KeyDown_WhenVictoryRewardPauseIsActive_BlocksGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        GameState.GamePlayState.IsVictoryRewardPauseActive = true;

        InvokeKeyDown(fixture.Controls, Keys.Space);
        InvokeKeyDown(fixture.Controls, Keys.RShiftKey);

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0, fixture.Weapons.ActiveWeapons.Count);
        Assert.AreEqual(0, GameState.GamePlayState.TotalShotsFired);
    }

    [TestMethod]
    public void MoveObject_WhenVictoryRewardPauseIsActive_FreezesShipPhysics()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);
        fixture.Ship.ObjectOffsets.y = 123f;
        GameState.SurfaceState.GlobalMapPosition.y = 40f;

        InvokeKeyDown(fixture.Controls, Keys.Space);
        Assert.IsTrue(fixture.Controls.ThrustOn);

        GameState.GamePlayState.IsVictoryRewardPauseActive = true;
        GameState.ShipState.ShipWorldPosition = new Vector3 { x = 11f, y = 22f, z = 33f };
        fixture.Ship.Rotation = new Vector3 { x = -35f, y = 18f, z = 142f };
        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
        Assert.AreEqual(123f, fixture.Ship.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(40f, GameState.SurfaceState.GlobalMapPosition.y, 0.001f);
        Assert.AreEqual(-35f, fixture.Ship.Rotation!.x, 0.001f,
            "Victory reward pause should leave the current ship rotation frozen instead of normalizing it.");
        Assert.AreEqual(18f, fixture.Ship.Rotation.y, 0.001f);
        Assert.AreEqual(142f, fixture.Ship.Rotation.z, 0.001f);
        Assert.AreEqual(11f, GameState.ShipState.ShipWorldPosition!.x, 0.001f,
            "Victory reward pause should freeze the last known ship world-state instead of recalculating it from a render copy.");
        Assert.AreEqual(22f, GameState.ShipState.ShipWorldPosition.y, 0.001f);
        Assert.AreEqual(33f, GameState.ShipState.ShipWorldPosition.z, 0.001f);
    }

    [TestMethod]
    public void MoveObject_WhenModalOverlayAppears_ClearsHeldGameplayInput()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        InvokeKeyDown(fixture.Controls, Keys.Space);
        Assert.IsTrue(fixture.Controls.ThrustOn);

        // SetIntroPreset/SetOutroPreset/SetNameEntryPreset all set IsModal=true,
        // which is the contract the input gate relies on.
        GameState.ScreenOverlayState.Type = ScreenOverlayType.Intro;
        GameState.ScreenOverlayState.IsModal = true;
        GameState.ScreenOverlayState.ShowOverlay = true;

        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.IsFalse(fixture.Controls.ThrustOn);
        Assert.AreEqual(0f, fixture.Controls.Thrust, 0.001f);
        Assert.AreEqual(0f, GameState.GamePlayState.Thrust, 0.001f);
    }

    [TestMethod]
    public void MoveObject_WhenBomberBombHitsShip_UsesBomberBombDamage()
    {
        using var fixture = CreateReadyShip(withWeaponGuides: true);
        fixture.Ship.ImpactStatus = new ImpactStatus
        {
            HasCrashed = true,
            ObjectName = "BomberBomb",
            ObjectHealth = 100
        };

        fixture.Controls.MoveObject(fixture.Ship, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(100 - EnemySetup.BomberBombCollisionDamage, fixture.Ship.ImpactStatus.ObjectHealth);
        Assert.IsFalse(fixture.Ship.ImpactStatus.HasCrashed);
    }

    private static ShipFixture CreateReadyShip(bool withWeaponGuides, ISurface? parentSurface = null)
    {
        var controls = new ShipControls();
        var ship = new _3dObject
        {
            ObjectId = GameState.ObjectIdCounter++,
            ObjectName = "Ship",
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = 70f, y = 0f, z = 0f },
            ImpactStatus = new ImpactStatus { ObjectName = "Ship", ObjectHealth = 100 },
            CrashBoxes = new List<List<IVector3>>(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "MuzzleFlash",
                    IsVisible = false,
                    Triangles = new List<ITriangleMeshWithColor> { CreateGuideVertex(0f, -50f, 28f) }
                }
            },
            Movement = controls,
            ParentSurface = parentSurface
        };

        var weapons = new Weapons(
            new List<I3dObject> { Bullet.CreateBullet(parentSurface: null!) },
            controls,
            ship);

        ship.WeaponSystems = weapons;
        controls.ParentObject = ship;

        var audio = new CapturingAudioPlayer();
        var registry = new FakeSoundRegistry();
        controls.ConfigureAudio(audio, registry);
        weapons.ConfigureAudio(audio, registry);

        if (withWeaponGuides)
        {
            controls.SetWeaponGuideCoordinates(
                CreateGuideVertex(0f, 0f, 0f),
                CreateGuideVertex(0f, -1000f, 0f));
        }

        return new ShipFixture(ship, controls, weapons, audio);
    }

    private static I3dObjectPart GetPart(I3dObject ship, string partName)
    {
        foreach (var part in ship.ObjectParts)
        {
            if (part.PartName == partName)
                return part;
        }

        Assert.Fail($"Expected part '{partName}' on ship.");
        throw new InvalidOperationException();
    }

    private static void AssertPartColor(I3dObject ship, string partName, string color)
    {
        foreach (var triangle in GetPart(ship, partName).Triangles)
        {
            Assert.AreEqual(color, triangle.Color);
        }
    }

    private static TriangleMeshWithColor CreateGuideVertex(float x, float y, float z)
    {
        return new TriangleMeshWithColor
        {
            vert1 = new Vector3 { x = x, y = y, z = z },
            vert2 = new Vector3 { x = x, y = y, z = z },
            vert3 = new Vector3 { x = x, y = y, z = z },
            normal1 = new Vector3 { x = 0f, y = 0f, z = 1f }
        };
    }

    private static void InvokeKeyDown(ShipControls controls, Keys key)
    {
        var method = typeof(ShipControls).GetMethod(
            "GlobalHookKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        method!.Invoke(controls, new object[] { controls, new KeyEventArgs(key) });
    }

    private static void InvokeMouseDown(ShipControls controls, MouseButtons button)
    {
        var method = typeof(ShipControls).GetMethod(
            "GlobalHookMouseDown",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        method!.Invoke(controls, new object[] { controls, new MouseEventArgs(button, clicks: 1, x: 100, y: 100, delta: 0) });
    }

    private static void InvokeMouseMovement(ShipControls controls, int x, int y)
    {
        var method = typeof(ShipControls).GetMethod(
            "HandleGlobalHookMouseMovement",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method);
        method!.Invoke(controls, new object[] { new MouseEventArgs(MouseButtons.None, clicks: 0, x, y, delta: 0), false });
    }

    private static void SetLastDecoyDeployUtc(ShipControls controls, DateTime lastDeployUtc)
    {
        var field = typeof(ShipControls).GetField(
            "_lastDecoyDeployUtc",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(field);
        field!.SetValue(controls, lastDeployUtc);
    }

    private sealed class ShipFixture : IDisposable
    {
        public ShipFixture(_3dObject ship, ShipControls controls, Weapons weapons, CapturingAudioPlayer audio)
        {
            Ship = ship;
            Controls = controls;
            Weapons = weapons;
            Audio = audio;
        }

        public _3dObject Ship { get; }
        public ShipControls Controls { get; }
        public Weapons Weapons { get; }
        public CapturingAudioPlayer Audio { get; }

        public void Dispose() => Controls.Dispose();
    }

    private sealed class FakeSoundRegistry : ISoundRegistry
    {
        public SoundDefinition Get(string id)
        {
            return new SoundDefinition
            {
                Id = id,
                Usage = id,
                File = $"{id}.wav",
                Settings = new SoundSettings { Volume = 1f }
            };
        }

        public bool TryGet(string id, out SoundDefinition definition)
        {
            definition = Get(id);
            return true;
        }
    }

    private sealed class CapturingAudioPlayer : IAudioPlayer
    {
        public int PlayCount { get; private set; }
        public string? LastDefinitionId { get; private set; }
        public AudioPlayMode? LastMode { get; private set; }
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            PlayCount++;
            LastDefinitionId = definition.Id;
            LastMode = mode;

            return new CapturingAudioInstance
            {
                SoundId = definition.Id,
                IsPlaying = true,
                IsLooping = mode == AudioPlayMode.SegmentedLoop
            };
        }

        public void PlayOneShot(SoundDefinition definition, AudioPlayOptions? options = null) =>
            Play(definition, AudioPlayMode.OneShot, options);

        public void Stop(IAudioInstance instance, bool playEndSegment) => instance.Stop(playEndSegment);
        public void StopAll() { }
        public void StopNonMusic() { }
        public void PlayMusic(SoundDefinition definition, float? volumeOverride = null) { }
        public void SetMusicVolume(float volume) => MusicVolume = volume;
        public void StopMusic() { }
        public void Update(double deltaTimeSeconds) { }
    }

    private sealed class CapturingAudioInstance : IAudioInstance
    {
        public Guid Id { get; } = Guid.NewGuid();
        public string SoundId { get; set; } = string.Empty;
        public bool IsPlaying { get; set; }
        public bool IsLooping { get; set; }

        public void SetVolume(float volume) { }
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) { }
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
