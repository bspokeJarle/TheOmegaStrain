using System.Reflection;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Collision;
using TheOmegaStrain.Runtime.Rendering;

namespace TheOmegaStrain.Tests.Controls;

public partial class AttackShipWeaponTests
{
    [TestMethod]
    public void StandaloneRocketController_DoesNotApplyWorkshopTestRotationOrMovement()
    {
        var obj = TheOmegaStrain.Game.World.Objects.Rocket.CreateRocket(null!);
        obj.Rotation = new Vector3(63, 15, 35);
        obj.WorldPosition = new Vector3(50000, 0, 50000);
        obj.ObjectOffsets = new Vector3(20, -100, 400);
        var controls = new RocketControls();

        for (int frame = 0; frame < 90; frame++)
            controls.MoveObject(obj, null, null);

        AssertVector(new Vector3(63, 15, 35), obj.Rotation);
        AssertVector(new Vector3(50000, 0, 50000), obj.WorldPosition);
        AssertVector(new Vector3(20, -100, 400), obj.ObjectOffsets);
    }

    [DataTestMethod]
    [DataRow(0f)]
    [DataRow(2f)]
    [DataRow(10f)]
    public void ReloadDelay_IsConfigurableAndStartsAtRemovalNotLaunch(float delay)
    {
        var owner = CreateAttackShip();
        ((AttackShipControls)owner.Movement!).RocketReloadDelaySeconds = delay;
        Fire(owner, Now);
        var previous = owner.WeaponSystems!.ActiveWeapons.Single();
        Fire(owner, Now.AddSeconds(30));
        Assert.AreSame(previous, owner.WeaponSystems.ActiveWeapons.Single());

        owner.WeaponSystems.ActiveWeapons.Clear();
        var removedAt = Now.AddSeconds(40);
        Fire(owner, removedAt);
        if (delay > 0)
        {
            Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count, "Time spent flying is not reload time.");
            Fire(owner, removedAt.AddSeconds(delay - 0.001));
            Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
            Fire(owner, removedAt.AddSeconds(delay));
        }
        Assert.AreEqual(1, owner.WeaponSystems.ActiveWeapons.Count);
        Assert.AreNotSame(previous, owner.WeaponSystems.ActiveWeapons.Single());
    }

    [TestMethod]
    public void ReloadContinuesOffscreenAndIsNotSharedBetweenAttackShips()
    {
        var first = CreateAttackShip();
        var second = CreateAttackShip();
        ((AttackShipControls)first.Movement!).RocketReloadDelaySeconds = 2f;
        ((AttackShipControls)second.Movement!).RocketReloadDelaySeconds = 10f;
        Fire(first, Now);
        Fire(second, Now);
        first.WeaponSystems!.ActiveWeapons.Clear();
        second.WeaponSystems!.ActiveWeapons.Clear();
        first.IsOnScreen = false;
        Fire(first, Now.AddSeconds(30));
        Fire(second, Now.AddSeconds(30));
        Fire(first, Now.AddSeconds(33));
        Assert.AreEqual(0, first.WeaponSystems.ActiveWeapons.Count);
        Assert.IsFalse(first.IsOnScreen);
        first.IsOnScreen = true; // Simulate the next render/AI update.
        Fire(first, Now.AddSeconds(33));
        Fire(second, Now.AddSeconds(33));
        Assert.AreEqual(1, first.WeaponSystems.ActiveWeapons.Count);
        Assert.AreEqual(0, second.WeaponSystems.ActiveWeapons.Count);
        Fire(second, Now.AddSeconds(40));
        Assert.AreEqual(1, second.WeaponSystems.ActiveWeapons.Count);
    }

    [TestMethod]
    public void OutsideBoundsRocket_ExplodesOnlyAfterFuelDepletionAndAllowsNextLaunch()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.WeaponObject.ObjectOffsets.x = ScreenSetup.screenSizeX * 3f;
        rocket.FiredTime = DateTime.UtcNow.AddSeconds(-20);
        rocket.LifetimeSeconds = 15;
        rocket.RocketState!.PoweredSeconds = 1f;
        AdvanceRocket(owner, rocket, 0.1);
        Assert.AreEqual(1.1f, rocket.RocketState.PoweredSeconds, 0.001f);
        Assert.IsFalse(rocket.RocketState.IsFalling);
        Assert.IsFalse(rocket.RocketState.IsExploding);

        rocket.RocketState.PoweredSeconds = 2.9f;
        StepRocket(owner, rocket, 3f - rocket.RocketState.PoweredSeconds);
        owner.WeaponSystems.MoveWeapon(null, null);
        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.IsTrue(rocket.RocketState.IsExploding);
        Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
        Assert.AreSame(rocket.WeaponObject, owner.WeaponSystems.Get3DObjects().Single());

        Fire(owner, Now.AddSeconds(20)); // Observe cleanup, then start the reload delay.
        Fire(owner, Now.AddSeconds(29.999));
        Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
        Fire(owner, Now.AddSeconds(30));
        Assert.AreEqual(1, owner.WeaponSystems.ActiveWeapons.Count);
        Assert.AreNotSame(rocket, owner.WeaponSystems.ActiveWeapons.Single());
    }

    [DataTestMethod]
    [DataRow(1500f, 30)]
    [DataRow(1500f, 60)]
    [DataRow(1500f, 90)]
    [DataRow(-2600f, 90)]
    public void VisibleRocketBeyondLegacyDepthBounds_RemainsRenderedDuringPhysicsFall(float depth, int fps)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        var obj = (OmegaObject3D)rocket.WeaponObject;
        obj.WorldPosition = new Vector3(50000, 0, 50000);
        obj.ObjectOffsets = new Vector3(0, -150, depth);
        obj.Particles = new RocketParticleSpy();
        rocket.RocketGuidanceLocked = true;
        rocket.Trajectory = new Vector3(0, 0, 1);
        rocket.RocketState!.PoweredSeconds = 2.99f;
        var projector = OmegaPerspectiveProjectorFactory.Create();
        Assert.IsTrue(projector.ProjectToTriangles(new() { obj }, 0).Count > 0,
            "This rocket is visibly rendered despite being beyond the old weapon Z limit.");

        StepRocket(owner, rocket, 3f - rocket.RocketState.PoweredSeconds);
        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.IsFalse(rocket.RocketState.IsExploding, "Fuel depletion must not detonate a visible rocket.");
        float startY = obj.ObjectOffsets.y;
        for (int frame = 0; frame < fps; frame++)
        {
            StepRocket(owner, rocket, 1f / fps);
            Assert.IsFalse(rocket.RocketState.IsExploding);
            Assert.IsTrue(projector.ProjectToTriangles(new() { obj }, frame + 1).Count > 0,
                "The actual projector must retain the rocket mesh throughout its fall.");
        }
        Assert.AreEqual(startY + 150f, obj.ObjectOffsets.y, 1.8f);
        Assert.AreEqual(300f, rocket.RocketState.Physics.Velocity.y, 0.01f);
        Assert.AreEqual(0, ((RocketParticleSpy)obj.Particles).Emissions);
        Assert.AreSame(obj, owner.WeaponSystems.Get3DObjects().Single());
    }

    [TestMethod]
    public void AttackShip_CanFireThreeSuccessiveMissesAfterAutomaticExplosionAndReload()
    {
        var owner = CreateAttackShip();
        var now = Now;
        var launchedIds = new HashSet<int>();
        Fire(owner, now);
        for (int launch = 0; launch < 3; launch++)
        {
            // The next rendered owner shares the weapon/controller state, as in LiveGameLoop.
            var frameOwner = (OmegaObject3D)OmegaObjectHelpers.DeepCopySingleObject(owner);
            var rocket = (ActiveWeapon)frameOwner.WeaponSystems!.ActiveWeapons.Single();
            Assert.IsTrue(launchedIds.Add(rocket.WeaponObject.ObjectId));
            rocket.RocketGuidanceLocked = true;
            rocket.Trajectory = new Vector3(1, 0, 0); // Simulate Ship dodging a committed shot.
            for (int frame = 0; frame < 90 * 10 && !rocket.RocketState!.IsExploding; frame++)
                StepRocket(frameOwner, rocket, 1f / 90f);

            Assert.AreEqual(3f, rocket.RocketState!.PoweredSeconds);
            Assert.IsTrue(rocket.RocketState.IsExploding, "A missed rocket must finish without a forged collision.");
            frameOwner.WeaponSystems.MoveWeapon(null, null);
            Assert.AreEqual(0, frameOwner.WeaponSystems.ActiveWeapons.Count);
            now = now.AddSeconds(10);
            Fire(frameOwner, now);
            Fire(frameOwner, now.AddSeconds(9.999));
            Assert.AreEqual(0, frameOwner.WeaponSystems.ActiveWeapons.Count);
            now = now.AddSeconds(10);
            Fire(frameOwner, now);
            Assert.AreEqual(1, frameOwner.WeaponSystems.ActiveWeapons.Count);
        }
    }

    [TestMethod]
    public void SpentRocket_ExplodesWhenItLaterCrossesBoundsDuringPhysicsFall()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.WeaponObject.WorldPosition = new Vector3(50000, 0, 50000);
        rocket.WeaponObject.ObjectOffsets = new Vector3(ScreenSetup.screenSizeX * 1.5f - 100f, 0, 0);
        rocket.RocketGuidanceLocked = true;
        rocket.Trajectory = new Vector3(1, 0, 0);
        rocket.RocketState!.PoweredSeconds = 3f;

        StepRocket(owner, rocket, 0.1f);
        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.IsFalse(rocket.RocketState.IsExploding);
        Assert.IsTrue(rocket.RocketState.Physics.Velocity.y > 0f);
        StepRocket(owner, rocket, 0.1f);
        owner.WeaponSystems.MoveWeapon(null, null);
        Assert.IsTrue(rocket.RocketState.IsExploding);
        Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
    }

    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void RocketBounds_UseCurrentWorldAndOffsetsRatherThanLaunchLocalOffsets(bool outside)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        // Large world and local offsets cancel out on screen. Moving the map can change that.
        rocket.WeaponObject.WorldPosition = new Vector3(60000, 50, 45000);
        rocket.WeaponObject.ObjectOffsets = new Vector3(-10000, -50, -5000);
        if (outside) GameState.SurfaceState.GlobalMapPosition.x += ScreenSetup.screenSizeX * 2f;
        rocket.RocketState!.PoweredSeconds = 3f;
        rocket.RocketGuidanceLocked = true;
        rocket.Trajectory = new Vector3();
        StepRocket(owner, rocket, 0.01f);
        Assert.AreEqual(outside, rocket.RocketState.IsExploding);
    }

    [TestMethod]
    public void MotorStopsAtExactlyThreeSecondsWithoutDetonating()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.RocketState!.PoweredSeconds = 2.99f;
        rocket.WeaponObject.Particles = new RocketParticleSpy();
        StepRocket(owner, rocket, 3f - rocket.RocketState.PoweredSeconds);
        Assert.AreEqual(3f, rocket.RocketState.PoweredSeconds);
        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.IsFalse(rocket.RocketState.IsExploding);
        Assert.AreEqual(0, ((RocketParticleSpy)rocket.WeaponObject.Particles).Emissions);
    }

    [TestMethod]
    public void FuelDepletion_PreservesMomentumAndDelegatesFallToPhysics()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.RocketGuidanceLocked = true;
        rocket.Trajectory = new Vector3(0.6f, -0.8f, 0f);
        rocket.MaxRange = 10000f;
        rocket.RocketState!.PoweredSeconds = 2.95f;
        var before = rocket.WeaponObject.ObjectOffsets;
        float poweredSeconds = 3f - rocket.RocketState.PoweredSeconds;
        var expectedVelocity = VectorMath.Multiply(rocket.Trajectory, 750f);
        IVector3 expectedPosition = VectorMath.Add(before, VectorMath.Multiply(expectedVelocity, poweredSeconds));
        var expectedPhysics = new Gameplay.Physics.Physics
        {
            Velocity = new Vector3(expectedVelocity.x, expectedVelocity.y, expectedVelocity.z),
            GravityStrength = WeaponSetup.RocketGravityStrength,
            Friction = 0f
        };
        float remaining = 0.1f - poweredSeconds;
        while (remaining > 0f)
        {
            float step = MathF.Min(remaining, GameState.GameplayBaselineDeltaTime);
            expectedPosition = expectedPhysics.ApplyForces(expectedPosition, step);
            remaining = MathF.Max(0f, remaining - step);
        }

        StepRocket(owner, rocket, 0.1f);

        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.IsTrue(rocket.RocketGuidanceLocked);
        Assert.AreEqual(3f, rocket.RocketState.PoweredSeconds);
        Assert.AreEqual(0f, rocket.RocketState.Physics.Thrust);
        AssertVector(expectedPosition, rocket.WeaponObject.ObjectOffsets);
        AssertVector(expectedPhysics.Velocity, rocket.RocketState.Physics.Velocity);

        // Once falling, changing Ship cannot reset velocity or reactivate guidance.
        float oldVerticalVelocity = rocket.RocketState.Physics.Velocity.y;
        GameState.ShipState.ShipObjectOffsets = new Vector3(1000, -1000, 1000);
        StepRocket(owner, rocket, 0.1f);
        Assert.AreEqual(oldVerticalVelocity + WeaponSetup.RocketGravityStrength * 0.1f,
            rocket.RocketState.Physics.Velocity.y, 0.001f);
    }

    [TestMethod]
    public void ReachingOldRange_DoesNotStopMotorOrRemoveRocket()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.MaxRange = 2500f;
        rocket.DistanceTraveled = rocket.MaxRange;
        StepRocket(owner, rocket, 0.02f);
        Assert.IsFalse(rocket.RocketState!.IsFalling);
        Assert.IsTrue(rocket.RocketState.PoweredSeconds < 3f);
        Assert.IsFalse(rocket.RocketState.IsExploding);
        Assert.AreEqual(1, owner.WeaponSystems.ActiveWeapons.Count);
        Fire(owner, Now.AddSeconds(20));
        Assert.AreSame(rocket, owner.WeaponSystems.ActiveWeapons.Single());
        Assert.AreSame(rocket.WeaponObject, owner.WeaponSystems.Get3DObjects().Single());
    }

    [TestMethod]
    public void MotorEmissionsStopExactlyWithFuelButExistingExhaustKeepsMoving()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.MaxRange = 10000f;
        rocket.RocketGuidanceLocked = true;
        var particles = new RocketParticleSpy();
        rocket.WeaponObject.Particles = particles;
        rocket.RocketState!.PoweredSeconds = 2.9f;
        StepRocket(owner, rocket, 0.05f);
        Assert.AreEqual(1, particles.Emissions);
        Assert.IsFalse(rocket.RocketState.IsFalling);
        var guide = rocket.WeaponObject.ObjectParts.Single(p => p.PartName == "RocketParticlesStartGuide").Triangles[0];
        Assert.AreSame(guide, particles.LastStart, "Weapon guides are already rotated and must be reused directly.");

        rocket.RocketState.PoweredSeconds = 3f;
        var oldOffset = rocket.WeaponObject.ObjectOffsets;
        var oldParticlePosition = new Vector3(1, 2, 3);
        particles.Particles.Add(new Particle { Position = oldParticlePosition });
        StepRocket(owner, rocket, 0.1f);
        Assert.IsTrue(rocket.RocketState.IsFalling);
        Assert.AreEqual(1, particles.Emissions);
        Assert.IsTrue(particles.MoveCalls > 0);
        AssertVector(VectorMath.Add(oldOffset, oldParticlePosition),
            VectorMath.Add(rocket.WeaponObject.ObjectOffsets, particles.Particles[0].Position));
    }

    [DataTestMethod]
    [DataRow(30)]
    [DataRow(60)]
    [DataRow(90)]
    public void PoweredAndPhysicsFlight_AreStableAcrossFrameRates(int fps)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        rocket.RocketGuidanceLocked = true;
        rocket.Trajectory = new Vector3(1, 0, 0);
        rocket.MaxRange = 10000f;
        rocket.WeaponObject.ObjectOffsets = new Vector3();
        for (int frame = 0; frame < fps; frame++) StepRocket(owner, rocket, 1f / fps);
        Assert.AreEqual(750f, rocket.WeaponObject.ObjectOffsets.x, 0.01f);
        Assert.AreEqual(0f, rocket.WeaponObject.ObjectOffsets.y, 0.001f);

        rocket.RocketState!.PoweredSeconds = 3f;
        for (int frame = 0; frame < fps; frame++) StepRocket(owner, rocket, 1f / fps);
        Assert.AreEqual(1500f, rocket.WeaponObject.ObjectOffsets.x, 0.05f);
        Assert.AreEqual(300f, rocket.RocketState.Physics.Velocity.y, 0.01f);
        // ApplyForces is semi-implicit Euler, bounded to <= 1/90 s substeps.
        Assert.AreEqual(150f, rocket.WeaponObject.ObjectOffsets.y, 1.8f);
    }

    [TestMethod]
    public void RocketPhysicsAndParticles_ArePerLaunchAndNotCopiedFromTemplate()
    {
        var first = CreateAttackShip();
        var second = CreateAttackShip();
        Fire(first, Now);
        Fire(second, Now);
        var a = (ActiveWeapon)first.WeaponSystems!.ActiveWeapons.Single();
        var b = (ActiveWeapon)second.WeaponSystems!.ActiveWeapons.Single();
        Assert.AreNotSame(a.RocketState, b.RocketState);
        Assert.AreNotSame(a.RocketState!.Physics, b.RocketState!.Physics);
        Assert.AreNotSame(a.WeaponObject.Movement, b.WeaponObject.Movement);
        Assert.AreNotSame(a.WeaponObject.Particles, b.WeaponObject.Particles);
        Assert.AreSame(a.RocketState.Physics, a.WeaponObject.Movement!.Physics);
        a.RocketState.PoweredSeconds = 3f;
        StepRocket(first, a, 0.1f);
        Assert.IsFalse(b.RocketState.IsFalling);
        Assert.AreEqual(0f, b.RocketState.PoweredSeconds);
        var copy = OmegaObjectHelpers.DeepCopySingleObject(first);
        Assert.AreSame(first.WeaponSystems, copy.WeaponSystems);
        Assert.AreSame(a.RocketState, ((ActiveWeapon)copy.WeaponSystems!.ActiveWeapons.Single()).RocketState);
    }

    [DataTestMethod]
    [DataRow("Ship")]
    [DataRow("Surface")]
    public void RocketExplodesBeforeVisualCleanup_AndFreesActiveWeaponSlot(string reason)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        var obj = rocket.WeaponObject;
        obj.ImpactStatus.HasCrashed = true;
        obj.ImpactStatus.ObjectName = reason;

        owner.WeaponSystems.MoveWeapon(null, null);

        Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
        Assert.AreSame(obj, owner.WeaponSystems.Get3DObjects().Single());
        Assert.IsTrue(rocket.RocketState!.IsExploding);
        Assert.IsFalse(obj.ImpactStatus.HasExploded);
        Assert.AreEqual(0, obj.CrashBoxes.Count);
        Assert.IsTrue(obj.Particles!.Particles.Count > 0);
        Assert.IsTrue(obj.ObjectParts.All(part => part.PartName == "ExplodingPart"));
        var position = new Vector3(obj.ObjectOffsets.x, obj.ObjectOffsets.y, obj.ObjectOffsets.z);

        // Drive the existing real explosion to completion without a wall-clock sleep.
        rocket.RocketState.ExplosionStarted = DateTime.Now.AddSeconds(-10);
        foreach (var particle in obj.Particles.Particles) particle.BirthTime = DateTime.UtcNow.AddMinutes(-1);
        for (int frame = 0; frame < 30; frame++) owner.WeaponSystems.MoveWeapon(null, null);
        AssertVector(position, obj.ObjectOffsets);
        Assert.IsTrue(obj.ImpactStatus.HasExploded);
        Assert.AreEqual(0, owner.WeaponSystems.Get3DObjects().Count());
    }

    [DataTestMethod]
    [DataRow(63f)]
    [DataRow(70f)]
    public void FallingRocket_HitsSurfaceTileFarFromViewportCentre(float pitch)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        var surface = new OmegaObject3D
        {
            ObjectId = GameState.ObjectIdCounter++, ObjectName = "Surface",
            WorldPosition = new Vector3(), ObjectOffsets = new Vector3(),
            Rotation = new Vector3(pitch, 0, 0), CrashBoxesFollowRotation = true,
            ImpactStatus = new ImpactStatus(), IsOnScreen = true,
            ObjectParts = new List<I3dObjectPart>
            {
                new OmegaObjectPart3D
                {
                    PartName = "VisibleSurfaceTile",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColorAndTexture>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "ffffff",
                            noHidden = true,
                            vert1 = new Vector3(280, -30, 0),
                            vert2 = new Vector3(360, -30, 0),
                            vert3 = new Vector3(320, 30, 0)
                        }
                    }
                }
            },
            CrashBoxes = new()
            {
                OmegaObject3DHelpers.GenerateCrashBoxCorners(new Vector3(280, -30, -30), new Vector3(360, 30, 30)),
                OmegaObject3DHelpers.GenerateCrashBoxCorners(new Vector3(-360, -30, -30), new Vector3(-280, 30, 30))
            }
        };
        new ObjectFrameTransformer().RotateObjectGeometry(surface);
        rocket.WeaponObject.WorldPosition = new Vector3(50000, 0, 50000);
        rocket.WeaponObject.ObjectOffsets = new Vector3(320, -150, 0);
        rocket.Trajectory = new Vector3();
        rocket.Velocity = 0;
        rocket.RocketState!.PoweredSeconds = 3;
        rocket.WeaponObject.IsOnScreen = true;
        var previousStaticCheck = typeof(CrashDetection).GetField("_lastStaticCheck", BindingFlags.NonPublic | BindingFlags.Static)!;
        var savedStaticCheck = previousStaticCheck.GetValue(null);
        try
        {
            // Deliberately suppress the ordinary 100 ms static scan. Rockets need every frame.
            previousStaticCheck.SetValue(null, DateTime.Now.AddMinutes(1));
            for (int frame = 0; frame < 90 && !rocket.WeaponObject.ImpactStatus.HasCrashed; frame++)
            {
                StepRocket(owner, rocket, 1f / 90);
                Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition((OmegaObject3D)rocket.WeaponObject, 750, 512, out _, out _, out _));
                CrashDetection.HandleCrashboxes(new() { (OmegaObject3D)rocket.WeaponObject, surface }, false);
            }
            Assert.IsTrue(rocket.WeaponObject.ImpactStatus.HasCrashed);
            Assert.AreEqual("Surface", rocket.WeaponObject.ImpactStatus.ObjectName);
            owner.WeaponSystems.MoveWeapon(null, null);
            Assert.IsTrue(rocket.RocketState.IsExploding);
            Assert.AreEqual(0, owner.WeaponSystems.ActiveWeapons.Count);
        }
        finally { previousStaticCheck.SetValue(null, savedStaticCheck); }
    }

    [TestMethod]
    public void WeaponRendererIncludesRocketExhaust()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        ((ParticlesAI)rocket.WeaponObject.Particles!).VariedStartMaxTicks = 0;
        StepRocket(owner, rocket, 0.03f);
        var weapons = new List<OmegaObject3D>();
        var particles = new List<OmegaObject3D>();
        new WeaponsManager().HandleWeapons(owner, weapons, particles);
        Assert.AreSame(rocket.WeaponObject, weapons.Single());
        Assert.IsTrue(particles.Count > 0);
    }

    [TestMethod]
    public void AttackShip_UsesPositionedRocketLoopUntilItExplodes()
    {
        var owner = CreateAttackShip();
        owner.WeaponSystems = null;
        var audio = new RocketAudioSpy();
        var sounds = new RocketSoundRegistry();

        owner.Movement!.MoveObject(owner, audio, sounds);

        var loop = audio.Plays.Single();
        Assert.AreEqual("rocket_main", loop.SoundId);
        Assert.AreEqual(AudioPlayMode.SegmentedLoop, loop.Mode);
        Assert.IsTrue(loop.Instance.IsPlaying);
        Assert.IsTrue(loop.Instance.PositionUpdates > 0);

        owner.ImpactStatus = new ImpactStatus
        {
            HasCrashed = true,
            ObjectName = "Ship",
            ObjectHealth = EnemySetup.AttackShipHealth
        };
        owner.Movement.MoveObject(owner, audio, sounds);

        Assert.IsFalse(loop.Instance.IsPlaying);
    }

    [TestMethod]
    public void Rocket_UsesPositionedRocketLoopOnlyWhileMotorHasFuel()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
        var audio = new RocketAudioSpy();
        var sounds = new RocketSoundRegistry();

        owner.WeaponSystems.MoveWeapon(audio, sounds);

        var loop = audio.Plays.Single();
        Assert.AreEqual("rocket_main", loop.SoundId);
        Assert.AreEqual(AudioPlayMode.SegmentedLoop, loop.Mode);
        Assert.IsTrue(loop.Instance.IsPlaying);
        Assert.IsTrue(loop.Instance.PositionUpdates > 0);

        StepRocket(owner, rocket, WeaponSetup.RocketFuelSeconds);

        Assert.IsTrue(rocket.RocketState!.IsFalling);
        Assert.IsFalse(loop.Instance.IsPlaying);
    }

    private static void StepRocket(OmegaObject3D owner, ActiveWeapon rocket, float seconds) =>
        typeof(Weapons).GetMethod("MoveRocket", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(owner.WeaponSystems, new object[] { rocket, seconds });

    private sealed class RocketParticleSpy : IParticles
    {
        public IObjectMovement ParentShip { get; set; } = null!;
        public List<IParticle> Particles { get; set; } = new();
        public float LifeMultiplier { get; set; } = 1f;
        public int MaxParticlesOverride { get; set; }
        public int Emissions { get; private set; }
        public int MoveCalls { get; private set; }
        public ITriangleMeshWithColorAndTexture? LastStart { get; private set; }
        public void ReleaseParticles(ITriangleMeshWithColorAndTexture trajectory,
            ITriangleMeshWithColorAndTexture startPosition, IVector3 worldPosition,
            IObjectMovement parentShip, int thrust, bool? explosion, float upwardVelocityBoost = 0f)
        { Emissions++; LastStart = startPosition; }
        public void MoveParticles() => MoveCalls++;
    }

    private sealed class RocketSoundRegistry : ISoundRegistry
    {
        public SoundDefinition Get(string id) => Create(id);

        public bool TryGet(string id, out SoundDefinition definition)
        {
            definition = Create(id);
            return true;
        }

        private static SoundDefinition Create(string id) => new()
        {
            Id = id,
            Usage = id,
            File = id + ".wav",
            Settings = new SoundSettings { Volume = 0.6f, Is3D = true }
        };
    }

    private sealed class RocketAudioSpy : IAudioPlayer
    {
        public List<RocketPlayCall> Plays { get; } = new();
        public float MusicVolume { get; private set; } = 0.15f;

        public IAudioInstance Play(SoundDefinition definition, AudioPlayMode mode, AudioPlayOptions? options = null)
        {
            var instance = new RocketAudioInstance(definition.Id);
            Plays.Add(new RocketPlayCall(definition.Id, mode, instance));
            return instance;
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

    private sealed record RocketPlayCall(
        string SoundId,
        AudioPlayMode Mode,
        RocketAudioInstance Instance);

    private sealed class RocketAudioInstance : IAudioInstance
    {
        public RocketAudioInstance(string soundId) => SoundId = soundId;

        public Guid Id { get; } = Guid.NewGuid();
        public string SoundId { get; }
        public bool IsPlaying { get; private set; } = true;
        public bool IsLooping => true;
        public int PositionUpdates { get; private set; }
        public void SetVolume(float volume) { }
        public void SetSpeed(float speed) { }
        public void SetWorldPosition(System.Numerics.Vector3 position) => PositionUpdates++;
        public void Stop(bool playEndSegment) => IsPlaying = false;
    }
}
