namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineRadialObjectScannerTests
{
    [TestMethod]
    public void Scan_FindsTargetsInsideRadiusAndSkipsSelf()
    {
        var objects = new List<RadialScanObject>
        {
            new("Source", new EngineVector3(0f, 0f, 0f), isSource: true),
            new("Near", new EngineVector3(3f, 0f, 4f), isTarget: true),
            new("Far", new EngineVector3(20f, 0f, 0f), isTarget: true),
            new("Ignored", new EngineVector3(1f, 0f, 1f))
        };
        var hits = new List<string>();

        int hitCount = RadialObjectScanner.Scan(
            objects,
            radius: 5f,
            source => source.IsSource,
            obj => obj.Position,
            (source, target) => target.IsTarget,
            obj => obj.Position,
            (in RadialHitContext<RadialScanObject, EngineVector3> context) =>
                hits.Add($"{context.Source.Name}->{context.Target.Name}:{context.Distance:0}"));

        Assert.AreEqual(1, hitCount);
        CollectionAssert.AreEqual(new[] { "Source->Near:5" }, hits);
    }

    [TestMethod]
    public void Scan_IgnoresSourcesOrTargetsWithoutPositions()
    {
        var objects = new List<RadialScanObject>
        {
            new("SourceWithoutPosition", null, isSource: true),
            new("Source", new EngineVector3(0f, 0f, 0f), isSource: true),
            new("TargetWithoutPosition", null, isTarget: true),
            new("Target", new EngineVector3(2f, 0f, 0f), isTarget: true)
        };
        var hits = new List<string>();

        int hitCount = RadialObjectScanner.Scan(
            objects,
            radius: 3f,
            source => source.IsSource,
            obj => obj.Position,
            (source, target) => target.IsTarget,
            obj => obj.Position,
            (in RadialHitContext<RadialScanObject, EngineVector3> context) =>
                hits.Add($"{context.Source.Name}->{context.Target.Name}"));

        Assert.AreEqual(1, hitCount);
        CollectionAssert.AreEqual(new[] { "Source->Target" }, hits);
    }

    private sealed class RadialScanObject
    {
        public RadialScanObject(
            string name,
            EngineVector3? position,
            bool isSource = false,
            bool isTarget = false)
        {
            Name = name;
            Position = position;
            IsSource = isSource;
            IsTarget = isTarget;
        }

        public string Name { get; }
        public EngineVector3? Position { get; }
        public bool IsSource { get; }
        public bool IsTarget { get; }
    }
}
