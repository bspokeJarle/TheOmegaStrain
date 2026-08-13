using Domain;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Architecture;

[TestClass]
public class EngineObjectCompatibilityTests
{
    [TestMethod]
    public void DomainVector3_KeepsExistingOperatorsAndType()
    {
        var result = new Vector3(1, 2, 3) + new Vector3(4, 5, 6);

        Assert.IsInstanceOfType(result, typeof(Vector3));
        Assert.AreEqual(5, result.x);
        Assert.AreEqual(7, result.y);
        Assert.AreEqual(9, result.z);
    }

    [TestMethod]
    public void DomainTriangleMesh_LazyVectorsRemainDomainVector3()
    {
        var mesh = new TriangleMesh();

        Assert.IsNull(mesh.Vert1Raw);
        Assert.IsInstanceOfType(mesh.vert1, typeof(Vector3));
        Assert.IsInstanceOfType(mesh.Vert1Raw, typeof(Vector3));
    }

    [TestMethod]
    public void DomainObject_ImplementsGameplayAndEngineContracts()
    {
        I3dObject gameplayObject = new _3dObject
        {
            ObjectId = 1,
            ObjectName = "TestObject"
        };

        Assert.IsInstanceOfType(gameplayObject, typeof(IRenderable3dObject));

        var renderable = (IRenderable3dObject)gameplayObject;
        renderable.ObjectParts.Add(new _3dObjectPart { PartName = "Hull" });

        gameplayObject.HasPowerUp = true;
        gameplayObject.PowerUpType = PowerUpType.TravelSpeedLevel1;

        Assert.AreEqual(1, renderable.ObjectParts.Count);
        Assert.AreEqual("Hull", renderable.ObjectParts[0].PartName);
        Assert.IsTrue(gameplayObject.HasPowerUp);
        Assert.AreEqual(PowerUpType.TravelSpeedLevel1, gameplayObject.PowerUpType);
    }

    [TestMethod]
    public void DomainWorld_ExposesRenderableWorldView()
    {
        I3dWorld world = new TestWorld
        {
            WorldInhabitants = new List<I3dObject>
            {
                new _3dObject { ObjectId = 1, ObjectName = "Ship" },
                new _3dObject { ObjectId = 2, ObjectName = "Surface" }
            }
        };

        var renderableWorld = (IRenderable3dWorldView)world;

        Assert.AreEqual(2, renderableWorld.RenderableObjects.Count());
        Assert.IsTrue(renderableWorld.RenderableObjects.All(o => o is IRenderable3dObject));
    }

    [TestMethod]
    public void RenderableWorldView_TracksWorldInhabitantsWithoutCopying()
    {
        I3dWorld world = new TestWorld();
        var renderableWorld = (IRenderable3dWorldView)world;

        world.WorldInhabitants.Add(new _3dObject { ObjectId = 1, ObjectName = "Ship" });

        Assert.AreEqual("Ship", renderableWorld.RenderableObjects.Single().ObjectName);

        world.WorldInhabitants.Clear();

        Assert.AreEqual(0, renderableWorld.RenderableObjects.Count());
    }

    [TestMethod]
    public void DomainImpactStatus_ImplementsGameplayAndEngineImpactContracts()
    {
        IImpactStatus gameplayImpact = new ImpactStatus
        {
            HasCrashed = true,
            HasExploded = true,
            ObjectName = "Surface",
            CrashBoxName = "MainSurface",
            ImpactDirection = ImpactDirection.Bottom,
            ObjectHealth = 42
        };

        Assert.IsInstanceOfType(gameplayImpact, typeof(IImpactState));

        var engineImpact = (IImpactState)gameplayImpact;

        Assert.IsTrue(engineImpact.HasCrashed);
        Assert.IsTrue(engineImpact.HasExploded);
        Assert.AreEqual("Surface", engineImpact.ObjectName);
        Assert.AreEqual("MainSurface", engineImpact.CrashBoxName);
        Assert.AreEqual(ImpactDirection.Bottom, engineImpact.ImpactDirection);
        Assert.AreEqual(42, gameplayImpact.ObjectHealth);
    }

    [TestMethod]
    public void DomainGameLoop_ExtendsEngineFrameLoopContract()
    {
        Assert.IsTrue(
            typeof(IGameLoop<object>).GetInterfaces().Contains(typeof(IWorldFrameLoop<I3dWorld, object>)));
    }

    private sealed class TestWorld : I3dWorld
    {
        public List<I3dObject> WorldInhabitants { get; set; } = new();
        public ISceneHandler SceneHandler { get; set; } = null!;
        public IGameEventBus? EventBus { get; set; }
        public bool IsPaused { get; set; }
    }
}
