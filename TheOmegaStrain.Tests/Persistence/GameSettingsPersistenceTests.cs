using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.Persistence;

namespace TheOmegaStrain.Tests.Persistence;

[TestClass]
public class GameSettingsPersistenceTests
{
    private string _originalLocalFolder = string.Empty;
    private string _testLocalFolder = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainSettingsPersistenceTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.Initialize();
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
    public void SaveAndLoadSettings_RoundTripsAudioAndGraphicsValues()
    {
        var settings = new GameSettingsState
        {
            MasterVolumePercent = 80,
            MusicVolumePercent = 70,
            EffectsVolumePercent = 60,
            VoiceVolumePercent = 50,
            GraphicsQuality = GraphicsQualityPreset.High,
            ParticleDensityPercent = 130,
            GlowEffectsEnabled = true,
            EnhancedWeatherEnabled = true,
            EnhancedShadowsEnabled = false,
            ActiveControlScheme = ControlInputMode.Mouse,
            KeyboardThrustKey = "W",
            KeyboardFireKey = "F",
            MouseThrustButton = MouseControlButton.Right,
            MouseFireButton = MouseControlButton.Middle,
            XboxThrustButton = XboxControlButton.RightShoulder,
            XboxFireButton = XboxControlButton.LeftShoulder,
            XboxBulletButton = XboxControlButton.X,
            XboxDecoyButton = XboxControlButton.Y,
            XboxLazerButton = XboxControlButton.B,
            XboxPowerup4Button = XboxControlButton.A
        };

        GameSettingsPersistence.SaveSettings(settings);
        var loaded = GameSettingsPersistence.LoadSettings();

        Assert.AreEqual(80, loaded.MasterVolumePercent);
        Assert.AreEqual(70, loaded.MusicVolumePercent);
        Assert.AreEqual(60, loaded.EffectsVolumePercent);
        Assert.AreEqual(50, loaded.VoiceVolumePercent);
        Assert.AreEqual(GraphicsQualityPreset.High, loaded.GraphicsQuality);
        Assert.AreEqual(130, loaded.ParticleDensityPercent);
        Assert.IsTrue(loaded.GlowEffectsEnabled);
        Assert.IsTrue(loaded.EnhancedWeatherEnabled);
        Assert.IsFalse(loaded.EnhancedShadowsEnabled);
        Assert.AreEqual(ControlInputMode.Mouse, loaded.ActiveControlScheme);
        Assert.AreEqual("W", loaded.KeyboardThrustKey);
        Assert.AreEqual("F", loaded.KeyboardFireKey);
        Assert.AreEqual(MouseControlButton.Right, loaded.MouseThrustButton);
        Assert.AreEqual(MouseControlButton.Middle, loaded.MouseFireButton);
        Assert.AreEqual(XboxControlButton.RightShoulder, loaded.XboxThrustButton);
        Assert.AreEqual(XboxControlButton.LeftShoulder, loaded.XboxFireButton);
        Assert.AreEqual(XboxControlButton.X, loaded.XboxBulletButton);
        Assert.AreEqual(XboxControlButton.Y, loaded.XboxDecoyButton);
        Assert.AreEqual(XboxControlButton.B, loaded.XboxLazerButton);
        Assert.AreEqual(XboxControlButton.A, loaded.XboxPowerup4Button);
        Assert.AreEqual(GameSettingsState.CurrentSettingsSchemaVersion, loaded.SettingsSchemaVersion);
    }

    [TestMethod]
    public void LoadIntoGameState_UsesDefaultSettingsWhenFileIsMissing()
    {
        GameState.SettingsState = new GameSettingsState { MasterVolumePercent = 10 };

        GameSettingsPersistence.LoadIntoGameState();

        Assert.AreEqual(100, GameState.SettingsState.MasterVolumePercent);
        Assert.AreEqual(GraphicsQualityPreset.Balanced, GameState.SettingsState.GraphicsQuality);
        Assert.AreEqual(ControlInputMode.Keyboard, GameState.SettingsState.ActiveControlScheme);
        Assert.AreEqual(MouseControlButton.Right, GameState.SettingsState.MouseThrustButton);
        Assert.AreEqual(MouseControlButton.Left, GameState.SettingsState.MouseFireButton);
        Assert.AreEqual(XboxControlButton.RightTrigger, GameState.SettingsState.XboxThrustButton);
        Assert.AreEqual(XboxControlButton.LeftTrigger, GameState.SettingsState.XboxFireButton);
        Assert.AreEqual(XboxControlButton.X, GameState.SettingsState.XboxBulletButton);
        Assert.AreEqual(XboxControlButton.Y, GameState.SettingsState.XboxDecoyButton);
        Assert.AreEqual(XboxControlButton.B, GameState.SettingsState.XboxLazerButton);
        Assert.AreEqual(XboxControlButton.A, GameState.SettingsState.XboxPowerup4Button);
        Assert.AreEqual(GameSettingsState.CurrentSettingsSchemaVersion, GameState.SettingsState.SettingsSchemaVersion);
    }

    [TestMethod]
    public void LoadSettings_WhenLegacyXboxDefaultsAreStored_MigratesToTriggerLayout()
    {
        Directory.CreateDirectory(PersistenceSetup.LocalFolder);
        File.WriteAllText(PersistenceSetup.LocalSettingsFilePath,
            """
            {
              "activeControlScheme": "XboxController",
              "xboxThrustButton": "A",
              "xboxFireButton": "RightTrigger"
            }
            """);

        var loaded = GameSettingsPersistence.LoadSettings();

        Assert.AreEqual(ControlInputMode.XboxController, loaded.ActiveControlScheme);
        Assert.AreEqual(XboxControlButton.RightTrigger, loaded.XboxThrustButton);
        Assert.AreEqual(XboxControlButton.LeftTrigger, loaded.XboxFireButton);
        Assert.AreEqual(XboxControlButton.X, loaded.XboxBulletButton);
        Assert.AreEqual(XboxControlButton.Y, loaded.XboxDecoyButton);
        Assert.AreEqual(XboxControlButton.B, loaded.XboxLazerButton);
        Assert.AreEqual(XboxControlButton.A, loaded.XboxPowerup4Button);
        Assert.AreEqual(GameSettingsState.CurrentSettingsSchemaVersion, loaded.SettingsSchemaVersion);
    }

    [TestMethod]
    public void LoadSettings_WhenSchema2XboxSlotsAreStored_MigratesToSequentialFaceButtons()
    {
        Directory.CreateDirectory(PersistenceSetup.LocalFolder);
        File.WriteAllText(PersistenceSetup.LocalSettingsFilePath,
            """
            {
              "settingsSchemaVersion": 2,
              "activeControlScheme": "XboxController",
              "xboxBulletButton": "X",
              "xboxDecoyButton": "B",
              "xboxLazerButton": "Y"
            }
            """);

        var loaded = GameSettingsPersistence.LoadSettings();

        Assert.AreEqual(ControlInputMode.XboxController, loaded.ActiveControlScheme);
        Assert.AreEqual(XboxControlButton.X, loaded.XboxBulletButton);
        Assert.AreEqual(XboxControlButton.Y, loaded.XboxDecoyButton);
        Assert.AreEqual(XboxControlButton.B, loaded.XboxLazerButton);
        Assert.AreEqual(XboxControlButton.A, loaded.XboxPowerup4Button);
        Assert.AreEqual(GameSettingsState.CurrentSettingsSchemaVersion, loaded.SettingsSchemaVersion);
    }
}
