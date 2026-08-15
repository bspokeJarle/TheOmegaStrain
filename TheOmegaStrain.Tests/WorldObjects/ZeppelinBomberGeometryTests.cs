using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using Domain;
using GameAiAndControls.Controls.ZeppelinBomberControls;
using System.Reflection;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.WorldObjects;

[TestClass]
public class ZeppelinBomberGeometryTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3 { x = 0f, y = 40f, z = 0f },
            AiObjects = new List<_3dObject>()
        };
        GameState.ShipState = new ShipState();
    }

    [TestMethod]
    public void BomberPropeller_AttachesToRearMount()
    {
        var bomber = ZeppelinBomber.CreateZeppelinBomber(null!);
        var rearMount = GetPart(bomber, "BomberRearMount");
        var propeller = GetPart(bomber, "BomberPropeller");

        float rearMountBackX = MinX(rearMount);
        float rearMountFrontX = MaxX(rearMount);
        float propellerFrontX = MaxX(propeller);
        float propellerBackX = MinX(propeller);

        Assert.IsTrue(
            propellerFrontX >= rearMountBackX - 0.001f,
            "Propeller hub should touch or overlap the rear mount instead of floating behind it.");
        Assert.IsTrue(
            propellerFrontX <= rearMountFrontX + 0.001f,
            "Propeller hub should attach at the rear mount, not move through the whole bomber body.");
        Assert.IsTrue(
            propellerBackX < rearMountBackX,
            "Propeller should still extend behind the rear mount.");
    }

    [TestMethod]
    public void BomberPropellerAnimation_KeepsHubAttachedToCenterLine()
    {
        var bomber = ZeppelinBomber.CreateZeppelinBomber(null!);
        var controls = new ZeppelinBomberControls { ParentObject = bomber };
        var propeller = GetPart(bomber, "BomberPropeller");
        var originalHubCenter = FrontFaceCenter(propeller);

        for (int i = 0; i < 80; i++)
        {
            InvokeAnimatePropeller(controls, 0.1f);
        }

        var animatedHubCenter = FrontFaceCenter(propeller);

        Assert.AreEqual(originalHubCenter.x, animatedHubCenter.x, 0.001f,
            "Propeller animation should not move the hub away from the rear mount on X.");
        Assert.AreEqual(originalHubCenter.y, animatedHubCenter.y, 0.001f,
            "Propeller animation should keep the hub centered on local Y.");
        Assert.AreEqual(originalHubCenter.z, animatedHubCenter.z, 0.001f,
            "Propeller animation should keep the hub centered on local Z.");
    }

    [TestMethod]
    public void BomberMovement_SyncsSixtyUnitsAboveSurface()
    {
        var bomber = ZeppelinBomber.CreateZeppelinBomber(null!);
        bomber.WorldPosition = new Vector3 { x = 1000f, y = 0f, z = 2000f };
        bomber.ObjectOffsets = new Vector3 { x = 10f, y = 120f, z = 400f };
        bomber.Rotation = new Vector3();
        bomber.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.ZeppelinBomberHealth };

        var controls = new ZeppelinBomberControls { ParentObject = bomber };

        controls.MoveObject(bomber, null, null);

        Assert.AreEqual(10f, bomber.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(160f, bomber.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(400f, bomber.ObjectOffsets.z, 0.001f);
    }

    private static I3dObjectPart GetPart(I3dObject obj, string partName)
    {
        var part = obj.ObjectParts.SingleOrDefault(part => part.PartName == partName);
        Assert.IsNotNull(part, $"Expected bomber part '{partName}' to exist.");
        return part;
    }

    private static float MinX(I3dObjectPart part)
    {
        return part.Triangles
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
            .Min(vertex => vertex.x);
    }

    private static float MaxX(I3dObjectPart part)
    {
        return part.Triangles
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
            .Max(vertex => vertex.x);
    }

    private static Vector3 FrontFaceCenter(I3dObjectPart part)
    {
        float maxX = MaxX(part);
        const float tolerance = 0.001f;
        var frontVertices = part.Triangles
            .SelectMany(triangle => new[] { triangle.vert1, triangle.vert2, triangle.vert3 })
            .Where(vertex => MathF.Abs(vertex.x - maxX) <= tolerance)
            .ToList();

        Assert.IsTrue(frontVertices.Count > 0, "Expected the propeller front hub face to have vertices.");

        return new Vector3
        {
            x = maxX,
            y = (frontVertices.Min(vertex => vertex.y) + frontVertices.Max(vertex => vertex.y)) * 0.5f,
            z = (frontVertices.Min(vertex => vertex.z) + frontVertices.Max(vertex => vertex.z)) * 0.5f
        };
    }

    private static void InvokeAnimatePropeller(ZeppelinBomberControls controls, float deltaSeconds)
    {
        var method = typeof(ZeppelinBomberControls).GetMethod(
            "AnimatePropeller",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(method, "Expected ZeppelinBomberControls to keep propeller animation in the control class.");
        method.Invoke(controls, new object[] { deltaSeconds });
    }
}
