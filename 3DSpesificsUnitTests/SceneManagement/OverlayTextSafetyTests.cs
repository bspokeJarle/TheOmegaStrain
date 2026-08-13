using _3dRotations.Scenes.Scene1;
using _3dRotations.Scenes.Scene2;
using _3dRotations.Scenes.Scene3;
using _3dRotations.Scenes.Scene4;
using _3dRotations.Scenes.Scene5;
using _3dRotations.Scenes.Scene6;
using _3dRotations.Scenes.Scene7;
using _3dRotations.Scenes.Scene8;
using _3dRotations.Scenes.Intro;
using _3dRotations.Scenes.Outro;
using _3dRotations.Scenes.SceneSimulation;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.Persistence;
using Domain;
using System.Reflection;

namespace _3DSpesificsUnitTests.SceneManagement;

[TestClass]
public class OverlayTextSafetyTests
{
    private string _originalLocalFolder = "";
    private string _testLocalFolder = "";
    private int _originalMaxHighscoreEntries;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WorldFade = new WorldFadeState();
        GameState.ObjectIdCounter = 0;
        GameState.DeltaTime = 0f;

        _originalLocalFolder = PersistenceSetup.LocalFolder;
        _originalMaxHighscoreEntries = PersistenceSetup.MaxHighscoreEntries;
        _testLocalFolder = Path.Combine(Path.GetTempPath(), "OmegaStrainOverlayTextTests", Guid.NewGuid().ToString("N"));
        PersistenceSetup.LocalFolder = _testLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = 100;
        PersistenceSetup.Initialize();
        HighscoreService.SaveLocalHighscores(new HighscoreList());
    }

    [TestCleanup]
    public void Cleanup()
    {
        PersistenceSetup.LocalFolder = _originalLocalFolder;
        PersistenceSetup.MaxHighscoreEntries = _originalMaxHighscoreEntries;

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
    public void SceneOverlays_UseAsciiTextAndActualNewLines()
    {
        IScene[] scenes =
        {
            new Intro(),
            new Scene1(),
            new Scene2(),
            new Scene3(),
            new Scene4(),
            new Scene5(),
            new Scene6(),
            new Scene7(),
            new Scene8(),
            new SceneSimulation()
        };

        foreach (var scene in scenes)
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            scene.SetupSceneOverlay();
            AssertOverlayTextIsSafe(scene.GetType().Name, GameState.ScreenOverlayState);
        }
    }

    [TestMethod]
    public void OutroCongratulationsOverlay_UsesAsciiTextAndActualNewLines()
    {
        var method = typeof(OutroDirector).GetMethod(
            "ShowCongratulationsOverlay",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Expected OutroDirector.ShowCongratulationsOverlay to exist.");

        method.Invoke(null, new object[] { GameState.ScreenOverlayState });

        AssertOverlayTextIsSafe(nameof(OutroDirector), GameState.ScreenOverlayState);
    }

    [TestMethod]
    public void NameEntryOverlay_UsesAsciiCursorAndActualNewLineForValidation()
    {
        var overlay = GameState.ScreenOverlayState;
        overlay.SetNameEntryPreset("ace");
        overlay.NameEntryValidationMessage = "ALREADY USED";
        overlay.Update(1f / 60f);

        Assert.AreEqual("ACE", overlay.NameEntryBuffer);
        AssertOverlayTextIsSafe("NameEntry", overlay);
        Assert.IsTrue(overlay.Body.Contains('\n'), "Name entry validation should use an actual line break.");
    }

    private static void AssertOverlayTextIsSafe(string owner, ScreenOverlayState overlay)
    {
        AssertTextIsSafe(owner, "Header", overlay.Header);
        AssertTextIsSafe(owner, "Title", overlay.Title);
        AssertTextIsSafe(owner, "Body", overlay.Body);
        AssertTextIsSafe(owner, "Footer", overlay.Footer);

        for (int pageIndex = 0; pageIndex < overlay.Pages.Count; pageIndex++)
        {
            var page = overlay.Pages[pageIndex];
            for (int textIndex = 0; textIndex < page.Length; textIndex++)
                AssertTextIsSafe(owner, $"Page {pageIndex} text {textIndex}", page[textIndex]);
        }
    }

    private static void AssertTextIsSafe(string owner, string field, string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        Assert.IsFalse(text.Contains("\\n"), $"{owner} {field} contains a literal backslash-n.");
        Assert.IsFalse(text.Contains("\\r"), $"{owner} {field} contains a literal backslash-r.");

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            bool allowedControl = c == '\n' || c == '\r' || c == '\t';
            bool allowedAscii = c >= 32 && c <= 126;
            Assert.IsTrue(allowedControl || allowedAscii,
                $"{owner} {field} contains unsupported character U+{(int)c:X4} at index {i}.");
        }
    }
}
