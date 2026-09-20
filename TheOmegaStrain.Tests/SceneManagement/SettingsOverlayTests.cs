using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class SettingsOverlayTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainSettingsOverlayTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();

        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ShipState = new ShipState();
        GameState.WeatherVisualState = new WeatherVisualState();
        GameState.WorldFade = new WorldFadeState();
        GameState.TutorialState = new TutorialRuntimeState();
        GameState.SettingsState = new GameSettingsState();
        GameState.InputDeviceState = new InputDeviceState();
        GameState.ObjectIdCounter = 0;
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        try
        {
            if (Directory.Exists(_testLocalFolder))
                Directory.Delete(_testLocalFolder, recursive: true);
        }
        catch
        {
        }
    }

    [TestMethod]
    public void IntroSoundSettings_OpensAdjustsSavesAndReturnsToIntro()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;
            overlay.CurrentPage = 1;
            overlay.ApplyPageContent();

            HandleKeyPress(handler, world, GameInputKey.S);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Audio, overlay.SettingsPanel);
            Assert.IsTrue(overlay.IsModal);
            StringAssert.Contains(overlay.Title, "SOUND");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Left);

            Assert.AreEqual(95, GameState.SettingsState.MasterVolumePercent);
            Assert.IsTrue(File.Exists(PersistenceSetup.LocalSettingsFilePath));

            HandleKeyPress(handler, world, GameInputKey.Escape);

            Assert.AreEqual(ScreenOverlayType.Intro, overlay.Type);
            Assert.IsTrue(overlay.ShowOverlay);
            Assert.AreEqual(1, overlay.CurrentPage);
        });
    }

    [TestMethod]
    public void IntroGraphicsSettings_OpensAndAppliesPresetDefaults()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.G);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Graphics, overlay.SettingsPanel);
            Assert.AreEqual(GraphicsQualityPreset.Balanced, GameState.SettingsState.GraphicsQuality);
            Assert.AreEqual(CameraAnglePreset.Normal, GameState.SettingsState.CameraAngle);
            StringAssert.Contains(overlay.Body, "CAMERA ANGLE   NORMAL");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.AreEqual(GraphicsQualityPreset.High, GameState.SettingsState.GraphicsQuality);
            Assert.AreEqual(180, GameState.SettingsState.ParticleDensityPercent);
            Assert.IsTrue(GameState.SettingsState.GlowEffectsEnabled);
            Assert.IsTrue(GameState.SettingsState.EnhancedWeatherEnabled);
            Assert.IsTrue(GameState.SettingsState.EnhancedShadowsEnabled);
            Assert.IsTrue(GameState.SettingsState.SceneRasterBackgroundEnabled);
            StringAssert.Contains(overlay.Body, "SKY RASTER     ON");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.AreEqual(CameraAnglePreset.High, GameState.SettingsState.CameraAngle);
            Assert.AreEqual(70f, GameState.SettingsState.CameraPitchDegrees);
            StringAssert.Contains(overlay.Body, "CAMERA ANGLE   HIGH");

            for (int index = 0; index < 5; index++)
                HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.IsFalse(GameState.SettingsState.SceneRasterBackgroundEnabled);
            StringAssert.Contains(overlay.Body, "SKY RASTER     OFF");
        });
    }

    [TestMethod]
    public void CameraAngleSettings_LegacyLowFallsBackToNormalPitch()
    {
        var settings = new GameSettingsState { CameraAngle = CameraAnglePreset.Low };
        Assert.AreEqual(63f, settings.CameraPitchDegrees);

        settings.Normalize();
        Assert.AreEqual(CameraAnglePreset.Normal, settings.CameraAngle);

        settings.CameraAngle = CameraAnglePreset.Normal;
        Assert.AreEqual(63f, settings.CameraPitchDegrees);

        settings.CameraAngle = CameraAnglePreset.High;
        Assert.AreEqual(70f, settings.CameraPitchDegrees);
    }

    [TestMethod]
    public void IntroControlsSettings_OpensAdjustsAndSaves()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.C);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
            StringAssert.Contains(overlay.Title, "CONTROL");
            StringAssert.Contains(overlay.Body, "PLAY USING");
            StringAssert.Contains(overlay.Body, "CONFIGURE");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.AreEqual(ControlInputMode.Mouse, GameState.SettingsState.ActiveControlScheme);
            Assert.AreEqual(ControlInputMode.Mouse, GameState.SettingsState.ControlsEditorScheme);
            StringAssert.Contains(overlay.Body, "MOUSE MAPPINGS");
            Assert.IsTrue(File.Exists(PersistenceSetup.LocalSettingsFilePath));
        });
    }

    [TestMethod]
    public void IntroControlsSettings_CanEditMappingListWithoutChangingActiveControlScheme()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.C);

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
            Assert.AreEqual(ControlInputMode.Mouse, GameState.SettingsState.ControlsEditorScheme);
            StringAssert.Contains(overlay.Body, "MOUSE MAPPINGS");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);

            Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
            Assert.AreNotEqual(MouseControlButton.Right, GameState.SettingsState.MouseThrustButton);
        });
    }

    [TestMethod]
    public void SettingsOverlay_CanSwitchBetweenSettingsPanelsWithoutReturningToIntro()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.C);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            Assert.IsTrue(overlay.SettingsPageNavigationSelected);
            StringAssert.Contains(overlay.Body, "> SETTINGS PAGE");
            StringAssert.Contains(overlay.Footer, "LEFT/RIGHT CHANGE PAGE");
            StringAssert.Contains(overlay.Footer, "DOWN EDIT SETTINGS");
            StringAssert.Contains(overlay.Footer, "ESC BACK");

            HandleKeyPress(handler, world, GameInputKey.G);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Flight, overlay.SettingsPanel);
            StringAssert.Contains(overlay.Title, "FLIGHT");
            StringAssert.Contains(overlay.Body, "FLIGHT FEEL    BALANCED");
            StringAssert.Contains(overlay.Body, "FLIGHT INERTIA NORMAL");
            StringAssert.Contains(overlay.Body, "ROTATION INERTIA NORMAL");
            StringAssert.Contains(overlay.Body, "THRUST ACCEL.  NORMAL");
            StringAssert.Contains(overlay.Body, "GRAVITY PULL   NORMAL");
            StringAssert.Contains(overlay.Body, "RESET          BALANCED DEFAULTS");

            HandleKeyPress(handler, world, GameInputKey.Down);
            HandleKeyPress(handler, world, GameInputKey.Right);
            Assert.AreEqual(FlightHandlingPreset.Inertial, GameState.SettingsState.FlightPreset);
            StringAssert.Contains(overlay.Body, "FLIGHT FEEL    INERTIAL");
            StringAssert.Contains(overlay.Body, "FLIGHT INERTIA HIGH");
            StringAssert.Contains(overlay.Body, "ROTATION INERTIA HIGH");
            StringAssert.Contains(overlay.Body, "THRUST ACCEL.  GENTLE");
            StringAssert.Contains(overlay.Body, "GRAVITY PULL   LIGHT");
            Assert.IsTrue(File.Exists(PersistenceSetup.LocalSettingsFilePath));

            HandleKeyPress(handler, world, GameInputKey.S);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            StringAssert.Contains(overlay.Title, "CONTROL");

            HandleKeyPress(handler, world, GameInputKey.C);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            StringAssert.Contains(overlay.Title, "CONTROL");
        });
    }

    [TestMethod]
    public void FlightSettings_AdjustExistingShipPhysicsProfilesAndResetToCurrentDefaults()
    {
        var settings = new GameSettingsState();

        settings.AdjustFlight(FlightSettingsField.Preset, -1);
        Assert.AreEqual(FlightHandlingPreset.Stable, settings.FlightPreset);
        Assert.AreEqual(FlightCoasting.Short, settings.FlightCoastingSetting);
        Assert.AreEqual(FlightRotationInertia.Low, settings.FlightRotationInertiaSetting);
        Assert.AreEqual(FlightThrustResponse.Quick, settings.FlightThrustResponseSetting);
        Assert.AreEqual(FlightGravityResponse.Strong, settings.FlightGravityResponseSetting);

        settings.AdjustFlight(FlightSettingsField.FlightInertia, 1);
        Assert.AreEqual(FlightHandlingPreset.Custom, settings.FlightPreset);

        settings.AdjustFlight(FlightSettingsField.RotationInertia, 1);
        Assert.AreEqual(FlightRotationInertia.Normal, settings.FlightRotationInertiaSetting);

        settings.AdjustFlight(FlightSettingsField.ResetDefaults, 1);
        Assert.AreEqual(FlightHandlingPreset.Balanced, settings.FlightPreset);
        Assert.AreEqual(0.9975f, settings.ShipCoastingRetention);
        Assert.AreEqual(0.90f, settings.ShipRotationRetention);
        Assert.AreEqual(0.42f, settings.ShipMouseRotationFollow);
        Assert.AreEqual(30f, settings.ShipThrustRampRate);
        Assert.AreEqual(9.6f, settings.ShipThrustSpeedMultiplier);
        Assert.AreEqual(9f, settings.ShipGravityPullMultiplier);
    }

    [TestMethod]
    public void SettingsOverlay_DownEntersValuesAndUpReturnsToPageSelector()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);
            GameState.ScreenOverlayState.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.S);
            var overlay = GameState.ScreenOverlayState;
            Assert.IsTrue(overlay.SettingsPageNavigationSelected);
            StringAssert.Contains(overlay.Body, "> SETTINGS PAGE");
            Assert.IsFalse(overlay.Body.Contains("> MASTER", StringComparison.Ordinal));

            HandleKeyPress(handler, world, GameInputKey.Down);
            Assert.IsFalse(overlay.SettingsPageNavigationSelected);
            StringAssert.Contains(overlay.Body, "> MASTER");
            StringAssert.Contains(overlay.Footer, "LEFT/RIGHT ADJUST");

            HandleKeyPress(handler, world, GameInputKey.Up);
            Assert.IsTrue(overlay.SettingsPageNavigationSelected);
            StringAssert.Contains(overlay.Body, "> SETTINGS PAGE");
        });
    }

    [TestMethod]
    public void IntroKeyboardShortcut_OpensKeyboardControlSettings()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;
            overlay.CurrentPage = 0;
            overlay.ApplyPageContent();

            HandleKeyPress(handler, world, GameInputKey.K);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ControlsEditorScheme);
        });
    }

    [TestMethod]
    public void IntroXboxShortcut_OpensControllerSettingsWhenControllerConnected()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            GameState.InputDeviceState.SetXboxControllerConnected(true);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;
            overlay.CurrentPage = 0;
            overlay.ApplyPageContent();

            HandleKeyPress(handler, world, GameInputKey.X);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            Assert.AreEqual(ControlInputMode.XboxController, GameState.SettingsState.ControlsEditorScheme);
            GameState.InputDeviceState.Reset();
        });
    }

    [TestMethod]
    public void OverlayActivation_WhenSettingsOverlayIsOpen_ClosesAndReturnsToIntro()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ShowOverlay = true;

            HandleKeyPress(handler, world, GameInputKey.C);
            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);

            handler.HandleOverlayActivation(world);

            Assert.AreEqual(ScreenOverlayType.Intro, overlay.Type);
            Assert.IsTrue(overlay.ShowOverlay);
        });
    }

    [TestMethod]
    public void OverlayActivation_WhenInputDismissalDisabled_KeepsOverlayOpen()
    {
        RunOnStaThread(() =>
        {
            var handler = new SceneHandler();
            var world = CreateRealWorld(handler);
            handler.SetupActiveScene(world);

            var overlay = GameState.ScreenOverlayState;
            overlay.ResetToDefaults();
            overlay.Type = ScreenOverlayType.Game;
            overlay.Header = "PLANET SECURED";
            overlay.Title = "MISSION REWARD";
            overlay.ShowOverlay = true;
            overlay.CanDismissWithInput = false;

            handler.HandleOverlayActivation(world);

            Assert.AreEqual(ScreenOverlayType.Game, overlay.Type);
            Assert.AreEqual("MISSION REWARD", overlay.Title);
            Assert.IsTrue(overlay.ShowOverlay);
        });
    }

    private static GameWorld CreateRealWorld(SceneHandler handler)
    {
        var world = new GameWorld
        {
            SceneHandler = handler
        };
        world.WorldInhabitants.Clear();
        return world;
    }

    private static void HandleKeyPress(SceneHandler handler, GameWorld world, GameInputKey key)
    {
        handler.HandleKeyPress(key, world);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (failure != null)
            throw failure;
    }
}
