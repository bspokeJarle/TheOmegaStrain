namespace _3DSpesificsUnitTests.Engine;

[TestClass]
public class EngineObjectLifecycleCleanerTests
{
    [TestMethod]
    public void Cleanup_ReleasesResourcesBeforeClearingRenderableState()
    {
        var obj = CreateRenderableObject();
        bool releaseSawObjectState = false;

        EngineObjectLifecycleCleaner.Cleanup(
            new[] { obj },
            released =>
            {
                releaseSawObjectState =
                    released.CrashBoxes.Count == 1
                    && released.ObjectParts.Count == 1
                    && released.WorldPosition != null
                    && released.ObjectOffsets != null
                    && released.CalculatedCrashOffset != null;
            });

        Assert.IsTrue(releaseSawObjectState);
        Assert.AreEqual(0, obj.CrashBoxes.Count);
        Assert.AreEqual(0, obj.ObjectParts.Count);
        Assert.IsNull(obj.WorldPosition);
        Assert.IsNull(obj.ObjectOffsets);
        Assert.IsNull(obj.CalculatedCrashOffset);
    }

    [TestMethod]
    public void ClearRenderableState_DoesNotResetGameplayFacingFields()
    {
        var obj = CreateRenderableObject();
        obj.CrashBoxNames = new List<string?> { "Body" };
        obj.Rotation = new EngineVector3(1, 2, 3);
        obj.IsOnScreen = true;
        obj.HasShadow = true;
        obj.ShadowOffset = new EngineVector3(4, 5, 6);
        obj.IsActive = false;

        EngineObjectLifecycleCleaner.ClearRenderableState(obj);

        Assert.AreEqual("Body", obj.CrashBoxNames[0]);
        Assert.AreEqual(1f, obj.Rotation.x);
        Assert.IsTrue(obj.IsOnScreen);
        Assert.IsTrue(obj.HasShadow);
        Assert.AreEqual(4f, obj.ShadowOffset!.x);
        Assert.IsFalse(obj.IsActive);
    }

    [TestMethod]
    public void NotImplementedTypeDisposalGuard_SuppressesRepeatedNotImplementedDisposeByType()
    {
        int disposeAttempts = 0;
        var guard = new NotImplementedTypeDisposalGuard<UnsupportedDisposableResource>(
            _ =>
            {
                disposeAttempts++;
                throw new NotImplementedException();
            });

        bool firstResult = guard.TryDispose(new UnsupportedDisposableResource());
        bool secondResult = guard.TryDispose(new UnsupportedDisposableResource());

        Assert.IsFalse(firstResult);
        Assert.IsFalse(secondResult);
        Assert.AreEqual(1, disposeAttempts);
    }

    [TestMethod]
    public void NotImplementedTypeDisposalGuard_DisposesSupportedResources()
    {
        int disposeAttempts = 0;
        var guard = new NotImplementedTypeDisposalGuard<SupportedDisposableResource>(_ => disposeAttempts++);

        bool firstResult = guard.TryDispose(new SupportedDisposableResource());
        bool secondResult = guard.TryDispose(new SupportedDisposableResource());

        Assert.IsTrue(firstResult);
        Assert.IsTrue(secondResult);
        Assert.AreEqual(2, disposeAttempts);
    }

    private static Engine3dObject CreateRenderableObject() =>
        new()
        {
            ObjectId = 42,
            ObjectName = "LifecycleProbe",
            ObjectParts = new List<I3dObjectPart>
            {
                new Engine3dObjectPart { PartName = "Body", IsVisible = true }
            },
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new EngineVector3(1, 2, 3),
                    new EngineVector3(4, 5, 6)
                }
            },
            WorldPosition = new EngineVector3(10, 20, 30),
            ObjectOffsets = new EngineVector3(1, 1, 1),
            CalculatedCrashOffset = new EngineVector3(2, 2, 2)
        };

    private sealed class UnsupportedDisposableResource
    {
    }

    private sealed class SupportedDisposableResource
    {
    }
}
