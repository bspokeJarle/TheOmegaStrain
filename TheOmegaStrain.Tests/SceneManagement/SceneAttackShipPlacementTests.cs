using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.GamePlayHelpers;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Scenes.Scene1;
using TheOmegaStrain.Game.Scenes.Scene6;
using TheOmegaStrain.Game.Scenes.Scene7;
using TheOmegaStrain.Game.Scenes.Scene8;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Gameplay.Controls;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
[DoNotParallelize]
public class SceneAttackShipPlacementTests
{
    private GamePlayState _gameplay = null!;
    private SurfaceState _surface = null!;
    private ShipState _ship = null!;
    private ScreenOverlayState _overlay = null!;
    private WeatherVisualState _weather = null!;
    private int _objectId;

    [TestInitialize]
    public void Setup()
    {
        _gameplay = GameState.GamePlayState;
        _surface = GameState.SurfaceState;
        _ship = GameState.ShipState;
        _overlay = GameState.ScreenOverlayState;
        _weather = GameState.WeatherVisualState;
        _objectId = GameState.ObjectIdCounter;
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.ShipState = new ShipState();
        GameState.ScreenOverlayState = new ScreenOverlayState();
        GameState.WeatherVisualState = new WeatherVisualState();
    }

    [TestCleanup]
    public void Cleanup()
    {
        GameState.GamePlayState = _gameplay;
        GameState.SurfaceState = _surface;
        GameState.ShipState = _ship;
        GameState.ScreenOverlayState = _overlay;
        GameState.WeatherVisualState = _weather;
        GameState.ObjectIdCounter = _objectId;
    }

    [DataTestMethod]
    [DataRow(1, 0, 0)]
    [DataRow(6, 1, 55000)]
    [DataRow(7, 2, 55000)]
    [DataRow(8, 3, 58000)]
    public void SetupScene_PlacesRequestedArmedAttackShips(int sceneNumber, int count, int spread)
    {
        IScene scene = sceneNumber switch
        {
            1 => new Scene1(), 6 => new Scene6(), 7 => new Scene7(), 8 => new Scene8(),
            _ => throw new ArgumentOutOfRangeException(nameof(sceneNumber))
        };
        var world = new TestWorld();
        scene.SetupScene(world);
        var ships = world.WorldInhabitants.Where(o => o.ObjectName == "AttackShip").ToList();
        Assert.AreEqual(count, ships.Count);
        Assert.AreEqual(count, GameState.SurfaceState.AiObjects.Count(o => o.ObjectName == "AttackShip"));
        Assert.IsFalse(world.WorldInhabitants.Any(o => o.ObjectName is "Rocket" or "EnemyRocket"),
            "Rockets belong to each weapon system, not standalone scene inhabitants.");
        Assert.AreEqual(count, ships.Select(o => o.Movement).Distinct().Count());
        Assert.AreEqual(count, ships.Select(o => o.WeaponSystems!.ActiveWeapons).Distinct().Count());

        float ws = SurfaceSetup.WorldScale;
        // Supply the live Ship state normally published by its movement controller.
        GameState.ShipState.ShipObjectOffsets = new Vector3(0f, 0f, 400f);
        GameState.ShipState.ShipWorldPosition = SurfacePositionSyncHelpers.GetShipWorldPosition(0f, 400f);
        foreach (var ship in ships)
        {
            Assert.IsTrue(ship.IsActive);
            Assert.IsInstanceOfType<AttackShipControls>(ship.Movement);
            Assert.AreEqual(EnemySetup.AttackShipHealth, ship.ImpactStatus!.ObjectHealth);
            Assert.IsFalse(ship.CrashBoxDebugMode);
            Assert.IsFalse(ship.HasPowerUp);
            Assert.IsTrue(ship.WorldPosition.x >= (95700 - spread) * ws && ship.WorldPosition.x < (95700 + spread) * ws);
            Assert.IsTrue(ship.WorldPosition.z >= (92000 - spread) * ws && ship.WorldPosition.z < (92000 + spread) * ws);
            AssertOutsidePlatform(ship);
            Assert.AreSame(ship, GameState.SurfaceState.AiObjects.Single(o => o.ObjectId == ship.ObjectId));
            var weapons = (Weapons)ship.WeaponSystems!;
            Assert.IsTrue(weapons.FireAsEnemyWeapon);
            Assert.IsFalse(weapons.ShowAimAssist);
            Assert.AreSame(ship, weapons.ParentShipObject);
            Assert.AreSame(ship.Movement, weapons.ParentShip);
            Assert.AreEqual(0, weapons.ActiveWeapons.Count);
            ship.IsOnScreen = false;
            weapons.FireWeapon(new Vector3(288, 0, 0), new Vector3(48, 0, 0),
                ship.WorldPosition, WeaponType.Rocket, ship, 0);
            Assert.AreEqual(0, weapons.ActiveWeapons.Count, "Off-screen spawn must not fire.");
            // Simulate the render system marking this owner visible before firing.
            ship.IsOnScreen = true;
            weapons.FireWeapon(new Vector3(288, 0, 0), new Vector3(48, 0, 0),
                ship.WorldPosition, WeaponType.Rocket, ship, 0);
            Assert.AreEqual("EnemyRocket", weapons.ActiveWeapons.Single().WeaponObject.ObjectName);
        }
        if (ships.Count > 1)
        {
            ships[0].WeaponSystems!.ActiveWeapons.Clear();
            Assert.IsTrue(ships.Skip(1).All(o => o.WeaponSystems!.ActiveWeapons.Count == 1));
        }
        scene.SetupSceneOverlay();
        if (count > 0)
            StringAssert.Contains(GameState.ScreenOverlayState.Body, $"{count} AttackShips");
    }

    [TestMethod]
    public void Placement_RerollsPlatformAndSupportsRepeatableRandomPositions()
    {
        GameState.SurfaceState.Global2DMap = new SurfaceData[2560, 2560];
        var surface = new Surface();
        var first = new TestWorld();
        var random = new PlatformFirstRandom();
        AttackShipPlacementHelpers.AddAttackShipGroup(first, surface, 3, 55000, random);
        Assert.IsTrue(random.Calls > 6, "The first candidate is on the platform and must be rejected.");
        foreach (var ship in first.WorldInhabitants) AssertOutsidePlatform(ship);

        var repeat = new TestWorld();
        AttackShipPlacementHelpers.AddAttackShipGroup(repeat, surface, 3, 55000, new PlatformFirstRandom());
        CollectionAssert.AreEqual(
            first.WorldInhabitants.Select(o => (o.WorldPosition.x, o.WorldPosition.z)).ToArray(),
            repeat.WorldInhabitants.Select(o => (o.WorldPosition.x, o.WorldPosition.z)).ToArray());
        var other = new TestWorld();
        AttackShipPlacementHelpers.AddAttackShipGroup(other, surface, 3, 55000, new Random(7));
        Assert.AreNotEqual(first.WorldInhabitants[0].WorldPosition.x, other.WorldInhabitants[0].WorldPosition.x);
    }

    private static void AssertOutsidePlatform(I3dObject ship)
    {
        var map = GameState.SurfaceState.Global2DMap;
        Assert.IsFalse(LandingPlatformHelpers.IsLandingPlatformTile(map,
            MapCoordinateHelpers.WorldXToTileIndex(ship.WorldPosition.x, map),
            MapCoordinateHelpers.WorldZToTileIndex(ship.WorldPosition.z, map), bufferTiles: 2));
    }

    private sealed class PlatformFirstRandom : Random
    {
        public int Calls { get; private set; }
        public PlatformFirstRandom() : base(42) { }
        public override int Next(int minValue, int maxValue) => ++Calls switch
        {
            1 => 300, 2 => 4000, // (96000, 96000): centre of the test map.
            _ => base.Next(minValue, maxValue)
        };
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }
}
