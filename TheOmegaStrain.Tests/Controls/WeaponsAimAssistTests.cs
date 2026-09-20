using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
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

    [TestCleanup]
    public void Cleanup()
    {
        OmegaWorldViewSetup.ConfigurePitch(63f);
    }

    [DataTestMethod]
    [DataRow(CameraAnglePreset.Low)]
    [DataRow(CameraAnglePreset.Normal)]
    [DataRow(CameraAnglePreset.High)]
    public void LazerAimAssist_ProjectsRotatedCrashCenterAtEveryCameraAngle(CameraAnglePreset angle)
    {
        var settings = new GameSettingsState { CameraAngle = angle };
        OmegaWorldViewSetup.ConfigurePitch(settings.CameraPitchDegrees);

        var ship = CreateShip();
        var weapons = CreateWeapons(ship);
        var enemy = CreateEnemy("KamikazeDrone", x: 120f, y: -250f, z: 0f);
        enemy.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0f, z = 0f };
        enemy.CrashBoxes = new List<List<IVector3>>
        {
            OmegaObject3DHelpers.GenerateCrashBoxCorners(
                new Vector3 { x = -20f, y = -20f, z = 60f },
                new Vector3 { x = 20f, y = 20f, z = 100f })
        };
        enemy.CrashBoxesFollowRotation = true;
        new ObjectFrameTransformer().RotateObjectGeometry(enemy);
        GameState.SurfaceState.AiObjects.Add(enemy);

        var lazer = FireStraightLazer(weapons, ship);
        weapons.MoveWeapon(null, null);

        var rotatedCenter = Center(enemy.CrashBoxes[0]);
        float originX = ScreenSetup.screenSizeX / 2f + enemy.WorldPosition!.x;
        float originY = ScreenSetup.screenSizeY / 2f + enemy.WorldPosition.y;
        Assert.IsTrue(ProjectionMath.TryProjectVertex(
            rotatedCenter,
            originX,
            originY,
            -enemy.WorldPosition.z,
            ScreenSetup.perspectiveAdjustment,
            ScreenSetup.defaultObjectZoom,
            out var expectedScreen));

        Assert.IsTrue(expectedScreen.x >= 0f && expectedScreen.x <= ScreenSetup.screenSizeX &&
                      expectedScreen.y >= 0f && expectedScreen.y <= ScreenSetup.screenSizeY,
            $"Rotated crash center should remain visible for {angle}; actual ({expectedScreen.x:0.##}, {expectedScreen.y:0.##}).");

        Assert.IsTrue(GameState.GamePlayState.AimAssistTargetActive, $"Aim assist should acquire a target at {angle} angle.");
        Assert.AreEqual(expectedScreen.x, GameState.GamePlayState.AimAssistTargetScreenX, 1f);
        Assert.AreEqual(expectedScreen.y, GameState.GamePlayState.AimAssistTargetScreenY, 1f);
        Assert.IsTrue(lazer.Trajectory.x > 0f, $"Aim assist should pull the lazer toward the projected target at {angle} angle.");
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
    public void LazerAimAssist_UsesSameSurfaceSlopePositionAsRenderer()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { z = 1000f };
        var ship = CreateShip();
        var weapons = CreateWeapons(ship);
        var enemy = CreateEnemy("KamikazeDrone", x: 100f, y: -250f, z: 600f);
        enemy.ParentSurface = new Surface();
        GameState.SurfaceState.AiObjects.Add(enemy);

        FireStraightLazer(weapons, ship);
        weapons.MoveWeapon(null, null);

        float correctionY = (float)SurfaceSlopeRenderPositionHelpers.GetCorrectionY(enemy, enemy.WorldPosition);
        Assert.IsTrue(ProjectionMath.TryProjectVertex(
            GetLocalCrashBoxCenter(enemy),
            ScreenSetup.screenSizeX / 2f + enemy.WorldPosition.x,
            ScreenSetup.screenSizeY / 2f + enemy.WorldPosition.y + correctionY,
            GameState.SurfaceState.GlobalMapPosition.z - enemy.WorldPosition.z,
            ScreenSetup.perspectiveAdjustment,
            ScreenSetup.defaultObjectZoom,
            out var expectedScreen));

        Assert.IsTrue(GameState.GamePlayState.AimAssistTargetActive);
        Assert.AreEqual(expectedScreen.x, GameState.GamePlayState.AimAssistTargetScreenX, 1f);
        Assert.AreEqual(expectedScreen.y, GameState.GamePlayState.AimAssistTargetScreenY, 1f);
    }

    [TestMethod]
    public void LazerFromAirborneEnemy_StartsAtCorrectedVisibleMuzzle()
    {
        GameState.SurfaceState.GlobalMapPosition = new Vector3 { z = 1000f };
        var surface = new Surface();
        var attackShip = AttackShip.CreateAttackShip(surface);
        attackShip.WorldPosition = new Vector3 { x = 100f, y = 0f, z = 600f };
        attackShip.ObjectOffsets = new Vector3 { x = 0f, y = 25f, z = 400f };
        attackShip.Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees };
        var weapons = CreateWeapons(attackShip);
        var start = new Vector3 { x = 0f, y = 10f, z = 0f };
        var trajectory = new Vector3 { x = 0f, y = -990f, z = 0f };

        weapons.FireWeapon(
            trajectory,
            start,
            attackShip.WorldPosition,
            WeaponType.Lazer,
            attackShip,
            tilt: 0);

        var lazer = weapons.ActiveWeapons.Single().WeaponObject;
        float correctionY = (float)SurfaceSlopeRenderPositionHelpers.GetCorrectionY(
            attackShip,
            attackShip.WorldPosition);
        float expectedY = start.y + (trajectory.y - start.y) * 0.25f
            + WeaponSetup.LazerExitOffsetY + attackShip.ObjectOffsets.y + correctionY;
        Assert.AreEqual(expectedY, lazer.ObjectOffsets.y, Epsilon);
        Assert.AreEqual(25f, attackShip.ObjectOffsets.y, Epsilon,
            "Weapon launch correction must not mutate the parent object.");
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
                Triangles = new List<ITriangleMeshWithColorAndTexture>
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

    private static Vector3 Center(IReadOnlyList<IVector3> points)
    {
        return new Vector3
        {
            x = points.Average(point => point.x),
            y = points.Average(point => point.y),
            z = points.Average(point => point.z)
        };
    }

    private static Vector3 GetLocalCrashBoxCenter(OmegaObject3D obj)
    {
        var center = ObjectCollisionGeometry.GetLocalCrashCenter(obj);
        return new Vector3(center.x, center.y, center.z);
    }

    private sealed class NoopMovement : IObjectMovement
    {
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public IPhysics Physics { get; set; } = null!;
        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) => theObject;
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void Dispose() { }
    }
}
