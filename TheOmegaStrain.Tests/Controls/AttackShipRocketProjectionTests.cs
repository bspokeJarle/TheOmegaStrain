using System.Reflection;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.Projection;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Runtime.Rendering;
using TheOmegaStrain.Wpf.Rendering;

namespace TheOmegaStrain.Tests.Controls;

public partial class AttackShipWeaponTests
{
    // Rocket 186013 from the Release trace: the map moves away from the launch
    // anchor while accumulated offsets keep the rocket visible, including its fall.
    [DataTestMethod]
    [DataRow(58663.53f, 209.46f, 57116.73f, 1751.60f, 289.06f, -154.40f, -25.88f)]
    [DataRow(58959.20f, 203.54f, 57510.02f, 2198.45f, 277.72f, -448.31f, -9.10f)]
    [DataRow(59453.23f, 194.99f, 57789.52f, 2801.98f, 442.30f, -980.92f, 312.89f)]
    [DataRow(59786.44f, 171.03f, 57789.83f, 3181.82f, 723.08f, -1316.13f, 515.55f)]
    public void LoggedRocket_RemainsRenderedAsMapMovesAwayFromLaunchAnchor(
        float mapX, float mapY, float mapZ, float offsetX, float offsetY, float offsetZ, float velocityY)
    {
        int oldWidth = ScreenSetup.screenSizeX;
        int oldHeight = ScreenSetup.screenSizeY;
        try
        {
            ScreenSetup.Initialize(1548, 972);
            var owner = CreateAttackShip();
            Fire(owner, Now);
            var rocket = (ActiveWeapon)owner.WeaponSystems!.ActiveWeapons.Single();
            var obj = (OmegaObject3D)rocket.WeaponObject;
            obj.WorldPosition = new Vector3(56395.31f, -90.14f, 56347.07f);
            obj.ObjectOffsets = new Vector3(offsetX, offsetY, offsetZ);
            GameState.SurfaceState.GlobalMapPosition = new Vector3(mapX, mapY, mapZ);
            typeof(Weapons).GetMethod("SetRocketDirection", BindingFlags.Instance | BindingFlags.NonPublic)!
                .Invoke(owner.WeaponSystems, new object[]
                {
                    rocket, VectorMath.Normalize(new Vector3(562.29f, velocityY, -496.23f))
                });

            Assert.IsFalse(obj.CheckInhabitantVisibility(), "The launch anchor has left the world visibility radius.");
            Assert.IsTrue(ObjectPlacementHelpers.TryGetRenderPosition(obj, 774, 486, out var x, out var y, out _));
            Assert.IsTrue(x > 0 && x < 1548 && y > 0 && y < 972, "The actual rocket is still inside the screen.");
            var renderObjects = new List<OmegaObject3D>();
            new WeaponsManager().HandleWeapons(owner, renderObjects);
            var triangles = OmegaPerspectiveProjectorFactory.Create().ProjectToTriangles(renderObjects, 0);
            Assert.IsTrue(triangles.Count > 0, "Do not cull a visible rocket using its old launch anchor.");
            Assert.IsTrue(WorldRenderer.CullTrianglesOutsideRenderDepth(triangles) > 0,
                "The falling rocket must also survive the renderer's depth filter.");
            AssertVector(new Vector3(56395.31f, -90.14f, 56347.07f), obj.WorldPosition);
            AssertVector(new Vector3(offsetX, offsetY, offsetZ), obj.ObjectOffsets);

            obj.ObjectOffsets.x += 10000;
            Assert.AreEqual(0, OmegaPerspectiveProjectorFactory.Create().ProjectToTriangles(renderObjects, 0).Count,
                "The normal screen filter must still reject an actually off-screen rocket.");
        }
        finally { ScreenSetup.Initialize(oldWidth, oldHeight); }
    }

    [DataTestMethod]
    [DataRow("Rocket", true)]
    [DataRow("EnemyRocket", true)]
    [DataRow("AttackShip", false)]
    public void ProjectileProjection_LeavesOrdinaryInhabitantFilteringUnchanged(string name, bool visible)
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var obj = (OmegaObject3D)owner.WeaponSystems!.ActiveWeapons.Single().WeaponObject;
        obj.ObjectName = name;
        obj.WorldPosition = new Vector3(45000, 0, 50000);
        obj.ObjectOffsets = new Vector3(5000, 0, 400);
        Assert.IsFalse(obj.CheckInhabitantVisibility());
        var triangles = OmegaPerspectiveProjectorFactory.Create().ProjectToTriangles(new() { obj }, 0);
        Assert.AreEqual(visible, triangles.Count > 0);
    }

    [TestMethod]
    public void ProjectileProjection_RespectsFrameVisibilityHoldForOrdinaryObject()
    {
        var owner = CreateAttackShip();
        Fire(owner, Now);
        var obj = (OmegaObject3D)owner.WeaponSystems!.ActiveWeapons.Single().WeaponObject;
        obj.ObjectName = "AttackShip";
        obj.WorldPosition = new Vector3(45000, 0, 50000);
        obj.ObjectOffsets = new Vector3(5000, 0, 400);
        obj.IsOnScreen = true;

        Assert.IsFalse(obj.CheckInhabitantVisibility());
        Assert.IsTrue(OmegaPerspectiveProjectorFactory.Create()
            .ProjectToTriangles(new() { obj }, 0)
            .Count > 0);
    }

    [DataTestMethod]
    [DataRow("Rocket", true)]
    [DataRow("EnemyRocket", true)]
    [DataRow("AttackShip", false)]
    public void RocketParticles_UseCurrentPositionInsteadOfEmittersLaunchAnchor(string emitterName, bool visible)
    {
        var emitter = CreateAttackShip();
        emitter.ObjectName = emitterName;
        emitter.WorldPosition = new Vector3(45000, 0, 50000);
        emitter.ObjectOffsets = new Vector3(5000, 0, 400);
        emitter.Particles = new RocketParticleSpy();
        emitter.Particles.Particles.Add(new Particle
        {
            Visible = true,
            WorldPosition = emitter.WorldPosition,
            Position = new Vector3(),
            Rotation = new Vector3(),
            ParticleTriangle = new TriangleMeshWithColor
            {
                vert1 = new Vector3(-5, -5, 0), vert2 = new Vector3(5, -5, 0), vert3 = new Vector3(0, 5, 0),
                Color = "ffffff", noHidden = true
            }
        });
        var particles = new List<OmegaObject3D>();
        new ParticleManager().HandleParticles(emitter, particles);
        var particle = particles.Single();
        Assert.AreEqual(emitterName, particle.ImpactStatus!.ObjectName);
        Assert.IsFalse(particle.CheckInhabitantVisibility());
        var triangles = OmegaPerspectiveProjectorFactory.Create().ProjectToTriangles(particles, 0);
        Assert.AreEqual(visible, triangles.Count > 0);
    }
}
