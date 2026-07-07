using _3DWorld.Scene;
using _3dTesting._3dWorld;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Persistence;
using Domain;
using System.Windows.Input;
using System.Windows.Interop;

namespace _3DSpesificsUnitTests.SceneManagement;

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

            HandleKeyPress(handler, world, Key.S);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Audio, overlay.SettingsPanel);
            Assert.IsTrue(overlay.IsModal);
            StringAssert.Contains(overlay.Title, "SOUND");

            HandleKeyPress(handler, world, Key.Left);

            Assert.AreEqual(95, GameState.SettingsState.MasterVolumePercent);
            Assert.IsTrue(File.Exists(PersistenceSetup.LocalSettingsFilePath));

            HandleKeyPress(handler, world, Key.Escape);

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

            HandleKeyPress(handler, world, Key.G);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Graphics, overlay.SettingsPanel);
            Assert.AreEqual(GraphicsQualityPreset.Balanced, GameState.SettingsState.GraphicsQuality);

            HandleKeyPress(handler, world, Key.Right);

            Assert.AreEqual(GraphicsQualityPreset.High, GameState.SettingsState.GraphicsQuality);
            Assert.AreEqual(180, GameState.SettingsState.ParticleDensityPercent);
            Assert.IsTrue(GameState.SettingsState.GlowEffectsEnabled);
            Assert.IsTrue(GameState.SettingsState.EnhancedWeatherEnabled);
            Assert.IsTrue(GameState.SettingsState.EnhancedShadowsEnabled);
        });
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

            HandleKeyPress(handler, world, Key.C);

            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);
            Assert.AreEqual(ScreenOverlaySettingsPanel.Controls, overlay.SettingsPanel);
            Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
            StringAssert.Contains(overlay.Title, "CONTROL");

            HandleKeyPress(handler, world, Key.Right);

            Assert.AreEqual(ControlInputMode.Mouse, GameState.SettingsState.ActiveControlScheme);
            StringAssert.Contains(overlay.Body, "MOUSE");
            Assert.IsTrue(File.Exists(PersistenceSetup.LocalSettingsFilePath));
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

            HandleKeyPress(handler, world, Key.C);
            Assert.AreEqual(ScreenOverlayType.Settings, overlay.Type);

            handler.HandleOverlayActivation(world);

            Assert.AreEqual(ScreenOverlayType.Intro, overlay.Type);
            Assert.IsTrue(overlay.ShowOverlay);
        });
    }

    private static _3dWorld CreateRealWorld(SceneHandler handler)
    {
        var world = new _3dWorld
        {
            SceneHandler = handler
        };
        world.WorldInhabitants.Clear();
        return world;
    }

    private static void HandleKeyPress(SceneHandler handler, _3dWorld world, Key key)
    {
        using var source = new HwndSource(new HwndSourceParameters("OmegaStrainSettingsKeyTest")
        {
            Width = 1,
            Height = 1
        });

        handler.HandleKeyPress(CreateKeyArgs(key, source), world);
    }

    private static KeyEventArgs CreateKeyArgs(Key key, HwndSource source)
    {
        return new KeyEventArgs(Keyboard.PrimaryDevice, source, 0, key)
        {
            RoutedEvent = Keyboard.KeyDownEvent
        };
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
