using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls;
using _3dRotations.World.Objects;
using static CommonUtilities.WeaponHelpers.WeaponHelpers;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

// Side-by-side comparison of the Ship lazer vs the MotherShipMedium lazer.
//
// Purpose: pin down EXACTLY where the two firing setups diverge. Both call
// `Weapons.FireWeapon` with `WeaponType.Lazer`, but only the Ship's lazer works
// in-game. This test builds the two Weapons instances the way Scene5 does, fires
// both with identical inputs, and asserts:
//   1. Both produce exactly one ActiveWeapon of type Lazer.
//   2. Both use the same per-second Velocity (so travel speed is equal).
//   3. Both use the same MaxRange and LifetimeSeconds (so they live equally long).
//   4. Their post-fire ObjectOffsets land in comparable coordinate spaces.
//   5. Their Trajectory is the same unit vector.
//
// If any assertion fails, the failure message points at the specific setup knob
// (velocity override, name, world-position wiring, etc.) that must be reconciled
// before the two lazers will behave the same.
[TestClass]
public class ShipVsMotherShipMediumLazerTests
{
    private const float Epsilon = 1e-3f;

    [TestInitialize]
    public void ResetAiObjects()
    {
        // FireWeapon → ApplyAimAssist iterates AiObjects. Start empty so both
        // instances get a clean, equal aim-assist path.
        GameState.SurfaceState.AiObjects.Clear();
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

    private static _3dObject BuildLazerTemplate(string name = "Lazer", float scaleMultiplier = 1f, float crashBoxScaleMultiplier = 1f)
    {
        // Use the real factory so DeepCopySingleObject has every field it expects
        // (ObjectOffsets, Rotation, WorldPosition, etc.). Surface can be null for this test.
        var beam = Lazer.CreateLazer(
            parentSurface: null!,
            scaleMultiplier: scaleMultiplier,
            crashBoxScaleMultiplier: crashBoxScaleMultiplier);
        beam.ObjectName = name;
        return beam;
    }

    private static Vector3 GetCrashBoxSize(_3dObject obj)
    {
        Assert.IsNotNull(obj.CrashBoxes, $"{obj.ObjectName}: expected crashboxes.");
        Assert.AreEqual(1, obj.CrashBoxes.Count, $"{obj.ObjectName}: expected one lazer crashbox.");
        return GetCrashBoxSize(obj.CrashBoxes[0]);
    }

    private static Vector3 GetCrashBoxSize(List<IVector3> box)
    {
        var minX = float.MaxValue;
        var minY = float.MaxValue;
        var minZ = float.MaxValue;
        var maxX = float.MinValue;
        var maxY = float.MinValue;
        var maxZ = float.MinValue;

        foreach (var point in box)
        {
            if (point.x < minX) minX = point.x;
            if (point.y < minY) minY = point.y;
            if (point.z < minZ) minZ = point.z;
            if (point.x > maxX) maxX = point.x;
            if (point.y > maxY) maxY = point.y;
            if (point.z > maxZ) maxZ = point.z;
        }

        return new Vector3
        {
            x = maxX - minX,
            y = maxY - minY,
            z = maxZ - minZ
        };
    }

    private static _3dObject BuildShooter(string objectName, Vector3 objectOffsets, Vector3 worldPosition)
    {
        return new _3dObject
        {
            ObjectId = GameState.ObjectIdCounter++,
            ObjectName = objectName,
            ObjectOffsets = objectOffsets,
            WorldPosition = worldPosition,
            Rotation = new Vector3 { x = WorldViewSetup.CameraPitchDegrees, y = 0, z = 0 }, // camera-tilt baseline both use
            ImpactStatus = new ImpactStatus { ObjectName = objectName },
            CrashBoxes = new List<List<IVector3>>(),
        };
    }

    private static Weapons BuildShipLikeWeapons(_3dObject ship)
    {
        var template = BuildLazerTemplate("Lazer");
        return new Weapons(new List<I3dObject> { template }, new NoopMovement(), ship)
        {
            ShowAimAssist = true,       // as Scene5 wires it for the player
            FireAsEnemyWeapon = false,
        };
    }

    private static Weapons BuildMotherShipMediumLikeWeapons(_3dObject mother)
    {
        // Match Scene5 EXACTLY (after we reverted the velocity/range/lifetime overrides).
        var template = BuildLazerTemplate(
            "Lazer",
            scaleMultiplier: 2.0f,
            crashBoxScaleMultiplier: WeaponSetup.EnemyLazerMediumCrashBoxScale);
        return new Weapons(new List<I3dObject> { template }, new NoopMovement(), mother)
        {
            ShowAimAssist = false,
            FireAsEnemyWeapon = true,
            EnemyLazerName = "EnemyLazerMedium",
        };
    }

    private static ActiveWeapon FireOnce(Weapons weapons, _3dObject shooter)
    {
        // Same trajectory/start/world triple for both shooters so differences are
        // purely a function of the Weapons-instance configuration.
        var trajectory   = new Vector3 { x = 0f,   y = -1000f, z = 0f };
        var startPos     = new Vector3 { x = 0f,   y = 0f,    z = 0f };
        var worldPos     = shooter.WorldPosition;

        weapons.FireWeapon(trajectory, startPos, worldPos, WeaponType.Lazer, shooter, tilt: 0);

        Assert.AreEqual(1, weapons.ActiveWeapons.Count, $"{shooter.ObjectName}: expected exactly one ActiveWeapon after a single FireWeapon call.");
        return (ActiveWeapon)weapons.ActiveWeapons[0];
    }

    [TestMethod]
    public void MotherShipMedium_lazer_template_has_twenty_percent_smaller_crashbox()
    {
        var normalBeam = Lazer.CreateLazer(parentSurface: null!, scaleMultiplier: 2.0f);
        var mediumBeam = Lazer.CreateLazer(
            parentSurface: null!,
            scaleMultiplier: 2.0f,
            crashBoxScaleMultiplier: WeaponSetup.EnemyLazerMediumCrashBoxScale);

        var normalSize = GetCrashBoxSize(normalBeam);
        var mediumSize = GetCrashBoxSize(mediumBeam);

        Assert.AreEqual(normalSize.x * WeaponSetup.EnemyLazerMediumCrashBoxScale, mediumSize.x, Epsilon);
        Assert.AreEqual(normalSize.y * WeaponSetup.EnemyLazerMediumCrashBoxScale, mediumSize.y, Epsilon);
        Assert.AreEqual(normalSize.z * WeaponSetup.EnemyLazerMediumCrashBoxScale, mediumSize.z, Epsilon);
    }

    [TestMethod]
    public void Ship_lazer_and_MotherShipMedium_lazer_use_identical_default_velocity()
    {
        var ship   = BuildShooter("Ship",             new Vector3 { x = 0, y = 0,    z = 0   }, new Vector3 { x = 0,     y = 0, z = 0     });
        var mother = BuildShooter("MotherShipMedium", new Vector3 { x = 0, y = -1500, z = 400 }, new Vector3 { x = 95700, y = 0, z = 88000 });

        var shipWeapon   = FireOnce(BuildShipLikeWeapons(ship),                 ship);
        var motherWeapon = FireOnce(BuildMotherShipMediumLikeWeapons(mother),   mother);

        Assert.AreEqual(shipWeapon.Velocity, motherWeapon.Velocity, Epsilon,
            $"Velocity differs — Ship={shipWeapon.Velocity}, MSM={motherWeapon.Velocity}. " +
            "If this fails, the MSM Weapons instance has a LazerVelocityOverride set that the Ship doesn't.");
    }

    [TestMethod]
    public void Ship_lazer_and_MotherShipMedium_lazer_use_identical_default_range_and_lifetime()
    {
        var ship   = BuildShooter("Ship",             new Vector3 { x = 0, y = 0,    z = 0   }, new Vector3 { x = 0,     y = 0, z = 0     });
        var mother = BuildShooter("MotherShipMedium", new Vector3 { x = 0, y = -1500, z = 400 }, new Vector3 { x = 95700, y = 0, z = 88000 });

        var shipWeapon   = FireOnce(BuildShipLikeWeapons(ship),                 ship);
        var motherWeapon = FireOnce(BuildMotherShipMediumLikeWeapons(mother),   mother);

        Assert.AreEqual(shipWeapon.MaxRange, motherWeapon.MaxRange, Epsilon,
            $"MaxRange differs — Ship={shipWeapon.MaxRange}, MSM={motherWeapon.MaxRange}.");
        Assert.AreEqual(shipWeapon.LifetimeSeconds, motherWeapon.LifetimeSeconds, Epsilon,
            $"LifetimeSeconds differs — Ship={shipWeapon.LifetimeSeconds}, MSM={motherWeapon.LifetimeSeconds}.");
    }

    [TestMethod]
    public void Both_lazers_produce_the_same_unit_trajectory_for_the_same_input_direction()
    {
        var ship   = BuildShooter("Ship",             new Vector3 { x = 0, y = 0,    z = 0   }, new Vector3 { x = 0,     y = 0, z = 0     });
        var mother = BuildShooter("MotherShipMedium", new Vector3 { x = 0, y = -1500, z = 400 }, new Vector3 { x = 95700, y = 0, z = 88000 });

        var shipWeapon   = FireOnce(BuildShipLikeWeapons(ship),                 ship);
        var motherWeapon = FireOnce(BuildMotherShipMediumLikeWeapons(mother),   mother);

        Assert.AreEqual(shipWeapon.Trajectory.x, motherWeapon.Trajectory.x, Epsilon, "Trajectory.x differs.");
        Assert.AreEqual(shipWeapon.Trajectory.y, motherWeapon.Trajectory.y, Epsilon, "Trajectory.y differs.");
        Assert.AreEqual(shipWeapon.Trajectory.z, motherWeapon.Trajectory.z, Epsilon, "Trajectory.z differs.");

        // Trajectory MUST be unit length — MoveWeapon multiplies by Velocity*dt.
        var magShip   = MathF.Sqrt(shipWeapon.Trajectory.x   * shipWeapon.Trajectory.x   + shipWeapon.Trajectory.y   * shipWeapon.Trajectory.y   + shipWeapon.Trajectory.z   * shipWeapon.Trajectory.z);
        var magMother = MathF.Sqrt(motherWeapon.Trajectory.x * motherWeapon.Trajectory.x + motherWeapon.Trajectory.y * motherWeapon.Trajectory.y + motherWeapon.Trajectory.z * motherWeapon.Trajectory.z);
        Assert.AreEqual(1f, magShip,   1e-3f, $"Ship trajectory not unit-length (|v|={magShip}).");
        Assert.AreEqual(1f, magMother, 1e-3f, $"MSM trajectory not unit-length (|v|={magMother}).");
    }

    [TestMethod]
    public void MotherShipMedium_lazer_spawn_equals_Ship_spawn_plus_parent_offsets()
    {
        // Both setups go through the same `lazerStart` formula in FireWeapon:
        //   lazerStart = startPosition + (trajectory - startPosition) * 0.25
        //              + LazerExitOffset + shooter.ObjectOffsets
        // With identical inputs, the ONLY difference between Ship and MSM lazers
        // is the parent-shooter's ObjectOffsets. Pinning this down tells us exactly
        // how much world/screen-space shift we must compensate for if the MSM lazer
        // is rendering off-screen.
        var ship   = BuildShooter("Ship",             new Vector3 { x = 0, y = 0,    z = 0   }, new Vector3 { x = 0,     y = 0, z = 0     });
        var mother = BuildShooter("MotherShipMedium", new Vector3 { x = 0, y = -1500, z = 400 }, new Vector3 { x = 95700, y = 0, z = 88000 });

        var shipWeapon   = FireOnce(BuildShipLikeWeapons(ship),                 ship);
        var motherWeapon = FireOnce(BuildMotherShipMediumLikeWeapons(mother),   mother);

        var shipOffsets   = shipWeapon.WeaponObject.ObjectOffsets;
        var motherOffsets = motherWeapon.WeaponObject.ObjectOffsets;

        Assert.AreEqual(shipOffsets.x + mother.ObjectOffsets.x, motherOffsets.x, Epsilon,
            $"MSM lazer spawn.x ({motherOffsets.x:F2}) must equal Ship lazer spawn.x ({shipOffsets.x:F2}) + mother.ObjectOffsets.x ({mother.ObjectOffsets.x:F2}).");
        Assert.AreEqual(shipOffsets.y + mother.ObjectOffsets.y, motherOffsets.y, Epsilon,
            $"MSM lazer spawn.y ({motherOffsets.y:F2}) must equal Ship lazer spawn.y ({shipOffsets.y:F2}) + mother.ObjectOffsets.y ({mother.ObjectOffsets.y:F2}). " +
            "This vertical shift (−1500) is why the MSM beam renders off the top of the screen.");
        Assert.AreEqual(shipOffsets.z + mother.ObjectOffsets.z, motherOffsets.z, Epsilon,
            $"MSM lazer spawn.z ({motherOffsets.z:F2}) must equal Ship lazer spawn.z ({shipOffsets.z:F2}) + mother.ObjectOffsets.z ({mother.ObjectOffsets.z:F2}).");
    }

    [TestMethod]
    public void MotherShipMedium_lazer_is_renamed_when_FireAsEnemyWeapon_is_true()
    {
        // Documents the ONLY classification difference between the two lazers:
        // the enemy version is renamed, which re-routes it through the collision
        // system's name-based flags (IsWeapon=false, IsLazer=true via explicit
        // string list in CrashDetection.Cache.cs).
        var ship   = BuildShooter("Ship",             new Vector3 { x = 0, y = 0,    z = 0   }, new Vector3 { x = 0,     y = 0, z = 0     });
        var mother = BuildShooter("MotherShipMedium", new Vector3 { x = 0, y = -1500, z = 400 }, new Vector3 { x = 95700, y = 0, z = 88000 });

        var shipWeapon   = FireOnce(BuildShipLikeWeapons(ship),                 ship);
        var motherWeapon = FireOnce(BuildMotherShipMediumLikeWeapons(mother),   mother);

        Assert.AreEqual("Lazer",             shipWeapon.WeaponObject.ObjectName);
        Assert.AreEqual("EnemyLazerMedium",  motherWeapon.WeaponObject.ObjectName);
    }
}
