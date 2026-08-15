using TheOmegaStrain.Game.SceneManagement;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class SceneHandlerSceneIndexOverrideTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.ObjectIdCounter = 0;
    }

    [TestMethod]
    public void Constructor_UsesGamePlaySceneIndexAsStartupOverride()
    {
        GameState.GamePlayState.SceneIndex = 3;

        var handler = new SceneHandler();

        Assert.AreEqual("Scene3", handler.GetActiveScene().GetType().Name);
        Assert.AreEqual(SceneBiomeTypes.Rainforrest, handler.GetActiveScene().SceneBiome);
    }

    [TestMethod]
    public void Constructor_IgnoresInvalidSceneIndexOverride()
    {
        GameState.GamePlayState.SceneIndex = 99;

        var handler = new SceneHandler();

        Assert.AreEqual("Intro", handler.GetActiveScene().GetType().Name);
    }
}
