namespace RetroMesh.Engine.Tests;

[TestClass]
public class EngineImpactStateMarkerTests
{
    [TestMethod]
    public void MarkCollisionPair_SetsBothImpactStatesWithDirectionsAndCrashBoxNames()
    {
        var a = CreateObject(1, "A", "Body");
        var b = CreateObject(2, "B", "Wing");
        var collision = new CollisionBoxPairResult(
            BoxIndexA: 0,
            BoxIndexB: 0,
            BoundsA: default,
            BoundsB: default,
            CenterA: new EngineVector3(),
            CenterB: new EngineVector3(),
            CenterDistance: 10f,
            DirectionA: ImpactDirection.Left,
            DirectionB: ImpactDirection.Right);

        ImpactStateMarker.MarkCollisionPair(
            a,
            b,
            collision,
            static obj => obj.ImpactStatus);

        Assert.IsTrue(a.ImpactStatus.HasCrashed);
        Assert.AreEqual("B", a.ImpactStatus.ObjectName);
        Assert.AreEqual(ImpactDirection.Left, a.ImpactStatus.ImpactDirection);
        Assert.AreEqual("Body", a.ImpactStatus.CrashBoxName);

        Assert.IsTrue(b.ImpactStatus.HasCrashed);
        Assert.AreEqual("A", b.ImpactStatus.ObjectName);
        Assert.AreEqual(ImpactDirection.Right, b.ImpactStatus.ImpactDirection);
        Assert.AreEqual("Wing", b.ImpactStatus.CrashBoxName);
    }

    [TestMethod]
    public void MarkImpact_PreservesExistingProtectedImpactName()
    {
        var impact = new EngineImpactState
        {
            HasCrashed = true,
            ObjectName = "Weapon",
            ImpactDirection = ImpactDirection.Top,
            CrashBoxName = "Existing"
        };

        var result = ImpactStateMarker.MarkImpact(
            impact,
            sourceName: "Surface",
            direction: ImpactDirection.Bottom,
            crashBoxName: "New",
            preserveExistingImpactName: static name => name == "Weapon");

        Assert.IsTrue(result.HasImpactState);
        Assert.IsTrue(result.WasAlreadyCrashed);
        Assert.IsFalse(result.Updated);
        Assert.AreEqual("Weapon", impact.ObjectName);
        Assert.AreEqual(ImpactDirection.Top, impact.ImpactDirection);
        Assert.AreEqual("Existing", impact.CrashBoxName);
    }

    [TestMethod]
    public void MarkImpact_UpdatesExistingUnprotectedImpactName()
    {
        var impact = new EngineImpactState
        {
            HasCrashed = true,
            ObjectName = "Surface",
            ImpactDirection = ImpactDirection.Bottom,
            CrashBoxName = "Ground"
        };

        var result = ImpactStateMarker.MarkImpact(
            impact,
            sourceName: "Weapon",
            direction: ImpactDirection.Top,
            crashBoxName: "Body",
            preserveExistingImpactName: static name => name == "Weapon");

        Assert.IsTrue(result.Updated);
        Assert.AreEqual("Weapon", impact.ObjectName);
        Assert.AreEqual(ImpactDirection.Top, impact.ImpactDirection);
        Assert.AreEqual("Body", impact.CrashBoxName);
    }

    private static EngineObjectWithImpact CreateObject(int id, string name, string crashBoxName)
    {
        return new EngineObjectWithImpact
        {
            ObjectId = id,
            ObjectName = name,
            ImpactStatus = new EngineImpactState(),
            CrashBoxNames = new List<string?> { crashBoxName }
        };
    }

    private sealed class EngineObjectWithImpact : Engine3dObject
    {
        public required EngineImpactState ImpactStatus { get; init; }
    }
}
