using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Gameplay.Physics;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Tests.Physics;

[TestClass]
public class ExplosionPhysicsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SettingsState = new GameSettingsState();
        GameState.WeatherVisualState = new WeatherVisualState();
    }

    [TestMethod]
    public void ExplodeObject_MarksOriginalPartsAsExplodingParts()
    {
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        var returned = physics.ExplodeObject(obj, explosionForce: 200f);

        Assert.AreSame(obj, returned, "Most controls ignore the return value, so ExplodeObject must mark the live object.");
        Assert.AreEqual(2, obj.ObjectParts.Count);
        Assert.IsTrue(obj.ObjectParts.All(part => part.PartName == "ExplodingPart"),
            "Exploding object parts should bypass render batching while preserving their original part and triangle indexes.");
    }

    [TestMethod]
    public void UpdateExplosion_KeepsOriginalPartIndexes()
    {
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        physics.ExplodeObject(obj, explosionForce: 200f);
        physics.UpdateExplosion(obj, DateTime.Now.AddMilliseconds(-100));

        Assert.AreEqual(2, obj.ObjectParts.Count);
        Assert.IsTrue(obj.ObjectParts.All(part => part.PartName == "ExplodingPart"));
        Assert.AreEqual(1, obj.ObjectParts[0].Triangles.Count);
        Assert.AreEqual(1, obj.ObjectParts[1].Triangles.Count);
    }

    [TestMethod]
    public void UpdateExplosion_MarksFreshDeepCopyAsExplodingParts()
    {
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var original = CreateTwoPartObject();
        var firstFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);

        physics.ExplodeObject(firstFrameCopy, explosionForce: 200f);

        var nextFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);
        physics.UpdateExplosion(nextFrameCopy, DateTime.Now.AddMilliseconds(-100));

        Assert.IsTrue(nextFrameCopy.ObjectParts.All(part => part.PartName == "ExplodingPart"),
            "Explosion state must be re-applied to each frame's fresh deep copy so render batching stays disabled.");
    }

    [TestMethod]
    public void ExplosionState_IsIndependentOfFrameCopyGeometry()
    {
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var original = CreateTwoPartObject();
        var firstFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);

        physics.ExplodeObject(firstFrameCopy, explosionForce: 0f);
        MutateAllGeometry(firstFrameCopy, 99999f);

        var nextFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);
        physics.UpdateExplosion(nextFrameCopy, DateTime.Now);

        Assert.IsTrue(
            nextFrameCopy.ObjectParts
                .SelectMany(part => part.Triangles)
                .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
                .All(vertex => Math.Abs(vertex.x) < 1000f && Math.Abs(vertex.y) < 1000f && Math.Abs(vertex.z) < 1000f),
            "Explosion state survives across frame copies, so stored triangles must be deep copies, not frame-object references.");
    }

    [TestMethod]
    public void ExplosionState_IsNotMutatedByFrameRotation()
    {
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var original = CreateTwoPartObject();
        var firstFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);

        physics.ExplodeObject(firstFrameCopy, explosionForce: 0f);
        physics.UpdateExplosion(firstFrameCopy, DateTime.Now.AddSeconds(1));

        new OmegaMeshRotation().RotateMesh(firstFrameCopy.ObjectParts[0].Triangles, 90f, 'Z');

        var nextFrameCopy = (_3dObject)OmegaObjectHelpers.DeepCopySingleObject(original);
        physics.UpdateExplosion(nextFrameCopy, DateTime.Now.AddSeconds(1));

        var v = nextFrameCopy.ObjectParts[0].Triangles[0].vert1;
        Assert.AreEqual(-10f, v.x, 0.001f,
            "Render-loop rotation must not mutate the physics-owned explosion triangle between frames.");
        Assert.AreEqual(0f, v.y, 0.001f,
            "Render-loop rotation must stay isolated to the current frame copy.");
    }

    [TestMethod]
    public void ExplodeObject_RaisesImpactFlashWhenGlowIsEnabled()
    {
        GameState.SettingsState.GlowEffectsEnabled = true;
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        physics.ExplodeObject(obj, explosionForce: 260f);

        Assert.IsTrue(GameState.WeatherVisualState.ImpactFlashIntensity > 0f);
    }

    [TestMethod]
    public void ExplodeObject_DoesNotRaiseImpactFlashWhenGlowIsDisabled()
    {
        GameState.SettingsState.GlowEffectsEnabled = false;
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        physics.ExplodeObject(obj, explosionForce: 260f);

        Assert.AreEqual(0f, GameState.WeatherVisualState.ImpactFlashIntensity);
    }

    [TestMethod]
    public void UpdateExplosion_AddsDebrisShimmerOutsideLowGraphics()
    {
        GameState.SettingsState.GraphicsQuality = GraphicsQualityPreset.Balanced;
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        physics.ExplodeObject(obj, explosionForce: 0f);
        physics.UpdateExplosion(obj, DateTime.Now.AddMilliseconds(-100));

        var color = obj.ObjectParts[0].Triangles[0].Color;
        Assert.IsFalse(string.Equals("ff0000", color, StringComparison.OrdinalIgnoreCase),
            "Balanced graphics should add a subtle warm shimmer to debris while it is still exploding.");
    }

    [TestMethod]
    public void UpdateExplosion_DisablesDebrisShimmerOnLowGraphics()
    {
        GameState.SettingsState.GraphicsQuality = GraphicsQualityPreset.Low;
        var physics = new TheOmegaStrain.Gameplay.Physics.Physics();
        var obj = CreateTwoPartObject();

        physics.ExplodeObject(obj, explosionForce: 0f);
        physics.UpdateExplosion(obj, DateTime.Now.AddMilliseconds(-100));

        var color = obj.ObjectParts[0].Triangles[0].Color;
        Assert.IsTrue(string.Equals("ff0000", color, StringComparison.OrdinalIgnoreCase),
            "Low graphics should keep the previous explosion color path without debris shimmer.");
    }

    private static _3dObject CreateTwoPartObject()
    {
        return new _3dObject
        {
            ObjectId = 1,
            ObjectName = "Exploder",
            ObjectOffsets = new Vector3(),
            Rotation = new Vector3(),
            WorldPosition = new Vector3(),
            CrashBoxes = new List<List<IVector3>>(),
            ImpactStatus = new ImpactStatus(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Hull",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        CreateTriangle(-10f)
                    }
                },
                new _3dObjectPart
                {
                    PartName = "Wing",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        CreateTriangle(10f)
                    }
                }
            }
        };
    }

    private static TriangleMeshWithColor CreateTriangle(float x)
    {
        return new TriangleMeshWithColor
        {
            Color = "ff0000",
            noHidden = true,
            angle = 1f,
            normal1 = new Vector3 { x = 0f, y = 0f, z = 1f },
            normal2 = new Vector3 { x = 0f, y = 0f, z = 1f },
            normal3 = new Vector3 { x = 0f, y = 0f, z = 1f },
            vert1 = new Vector3 { x = x, y = 0f, z = 0f },
            vert2 = new Vector3 { x = x + 8f, y = 0f, z = 0f },
            vert3 = new Vector3 { x = x, y = 8f, z = 0f }
        };
    }

    private static void MutateAllGeometry(I3dObject obj, float value)
    {
        foreach (var vertex in obj.ObjectParts
                     .SelectMany(part => part.Triangles)
                     .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 }))
        {
            vertex.x = value;
            vertex.y = value;
            vertex.z = value;
        }
    }
}
