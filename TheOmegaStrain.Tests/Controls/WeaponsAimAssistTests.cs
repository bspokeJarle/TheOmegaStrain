using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.Helpers;
using static TheOmegaStrain.Domain.WeaponHelpers;

namespace TheOmegaStrain.Tests.Controls;

[TestClass]
public class WeaponsAimAssistTests
{
    private const float Epsilon = 0.001f;

    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.ShipState = new ShipState();
        GameState.SurfaceState = new SurfaceState
        {
            AiObjects = new List<OmegaObject3D>(),
            GlobalMapPosition = new Vector3 { x = 0f, y = 0f, z = 0f }
        };
    }

    [TestMethod]
    public void LazerAimAssist_IgnoresOffscreenEnemiesAndHidesGuide()
    {
        var ship = CreateShip();
        var weapons = CreateWeapons(ship);

        GameState.SurfaceState.AiObjects.Add(CreateEnemy(
            "KamikazeDrone",
            x: ScreenSetup.screenSizeX * 2f,
            y: -300f,
            z: 0f));

        var lazer = FireStraightLazer(weapons, ship);
        weapons.MoveWeapon(null, null);

        Assert.AreEqual(0f, lazer.Trajectory.x, Epsilon, "Offscreen enemies must not pull the lazer sideways.");
        Assert.AreEqual(-1f, lazer.Trajectory.y, Epsilon, "Offscreen enemies must not alter the forward lazer direction.");
        Assert.IsFalse(GameState.GamePlayState.AimAssistTargetActive, "Aim guide should stay hidden when no enemy is visible.");
    }

    [TestMethod]
    public void LazerAimAssist_PullsTowardVisibleEnemyAndShowsGuide()
    {
        var ship = CreateShip();
        var weapons = CreateWeapons(ship);
        var enemy = CreateEnemy("KamikazeDrone", x: 240f, y: -420f, z: 0f);
        GameState.SurfaceState.AiObjects.Add(enemy);

        var lazer = FireStraightLazer(weapons, ship);
        weapons.MoveWeapon(null, null);

        Assert.IsTrue(lazer.Trajectory.x > 0.35f, $"Visible enemy should pull lazer right; actual x={lazer.Trajectory.x:0.###}.");
        Assert.IsTrue(lazer.Trajectory.x < 0.45f, $"Aim assist should help without snapping fully to the enemy; actual x={lazer.Trajectory.x:0.###}.");
        Assert.IsTrue(lazer.Trajectory.y < -0.75f, $"Lazer should still mostly move forward; actual y={lazer.Trajectory.y:0.###}.");

        Assert.IsTrue(GameState.GamePlayState.AimAssistTargetActive, "Aim guide should be visible for an on-screen target.");
        Assert.AreEqual(ScreenSetup.screenSizeX / 2f + enemy.WorldPosition!.x, GameState.GamePlayState.AimAssistTargetScreenX, 1f);
        Assert.AreEqual(ScreenSetup.screenSizeY / 2f + enemy.WorldPosition.y, GameState.GamePlayState.AimAssistTargetScreenY, 1f);
    }

    [TestMethod]
    public void AimGuide_ChoosesVisibleEnemyWhenAnotherEnemyIsOffscreen()
    {
        var ship = CreateShip();
        var weapons = CreateWeapons(ship);
        var offscreenEnemy = CreateEnemy("KamikazeDrone", x: -ScreenSetup.screenSizeX * 2f, y: -200f, z: 0f);
        var visibleEnemy = CreateEnemy("MotherShipSmall", x: 180f, y: -360f, z: 0f);

        GameState.SurfaceState.AiObjects.Add(offscreenEnemy);
        GameState.SurfaceState.AiObjects.Add(visibleEnemy);

        FireStraightLazer(weapons, ship);
        weapons.MoveWeapon(null, null);

        Assert.IsTrue(GameState.GamePlayState.AimAssistTargetActive);
        Assert.AreEqual(ScreenSetup.screenSizeX / 2f + visibleEnemy.WorldPosition!.x, GameState.GamePlayState.AimAssistTargetScreenX, 1f);
        Assert.AreEqual(ScreenSetup.screenSizeY / 2f + visibleEnemy.WorldPosition.y, GameState.GamePlayState.AimAssistTargetScreenY, 1f);
    }

    private static Weapons CreateWeapons(OmegaObject3D ship)
    {
        return new Weapons(new List<I3dObject> { Lazer.CreateLazer(parentSurface: null!) }, new NoopMovement(), ship)
        {
            ShowAimAssist = true
        };
    }

    private static ActiveWeapon FireStraightLazer(Weapons weapons, OmegaObject3D ship)
    {
        weapons.FireWeapon(
            new Vector3 { x = 0f, y = -1000f, z = 0f },
            new Vector3 { x = 0f, y = 0f, z = 0f },
            GameState.SurfaceState.GlobalMapPosition,
            WeaponType.Lazer,
            ship,
            tilt: 0);

        Assert.AreEqual(1, weapons.ActiveWeapons.Count);
        return (ActiveWeapon)weapons.ActiveWeapons[0];
    }

    private static OmegaObject3D CreateShip()
    {
        return new OmegaObject3D
        {
            ObjectId = 9001,
            ObjectName = "Ship",
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            WorldPosition = new Vector3 { x = 0f, y = 0f, z = 0f },
            Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 0f },
            ImpactStatus = new ImpactStatus { ObjectName = "Ship", ObjectHealth = 100 },
            CrashBoxes = CreateCrashBoxes(),
            ObjectParts = CreateObjectParts()
        };
    }

    private static OmegaObject3D CreateEnemy(string objectName, float x, float y, float z)
    {
        return new OmegaObject3D
        {
            ObjectId = GameState.ObjectIdCounter++,
            ObjectName = objectName,
            ObjectOffsets = new Vector3 { x = 0f, y = 0f, z = 0f },
            WorldPosition = new Vector3 { x = x, y = y, z = z },
            Rotation = new Vector3 { x = 0f, y = 0f, z = 0f },
            ImpactStatus = new ImpactStatus { ObjectName = objectName, ObjectHealth = 100 },
            CrashBoxes = CreateCrashBoxes(),
            ObjectParts = CreateObjectParts()
        };
    }

    private static List<List<IVector3>> CreateCrashBoxes()
    {
        var min = new Vector3 { x = -20f, y = -20f, z = -20f };
        var max = new Vector3 { x = 20f, y = 20f, z = 20f };
        return new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(min, max)
        };
    }

    private static List<I3dObjectPart> CreateObjectParts()
    {
        return new List<I3dObjectPart>
        {
            new OmegaObjectPart3D
            {
                PartName = "Body",
                IsVisible = true,
                Triangles = new List<ITriangleMeshWithColor>
                {
                    new TriangleMeshWithColor
                    {
                        Color = "ffffff",
                        vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 20f, z = 0f },
                        normal1 = new Vector3 { x = 0f, y = 0f, z = 1f }
                    }
                }
            }
        };
    }

    private sealed class NoopMovement : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) => theObject;
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void Dispose() { }
    }
}
