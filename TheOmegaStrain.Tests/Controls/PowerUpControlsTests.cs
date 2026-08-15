using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class PowerUpControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3(),
            AiObjects = new List<_3dObject>()
        };
    }

    [TestMethod]
    public void MoveObject_ExplodingPowerUpKeepsHitFrameTransformAfterExternalMutation()
    {
        var controls = new PowerUpControls();
        var powerup = CreatePowerUp();
        GameState.SurfaceState.AiObjects.Add(powerup);

        powerup.ImpactStatus!.HasCrashed = true;
        controls.MoveObject(powerup, audioPlayer: null, soundRegistry: null);

        var anchoredWorld = Copy(powerup.WorldPosition!);
        var anchoredOffsets = Copy(powerup.ObjectOffsets!);

        powerup.WorldPosition!.x += 500f;
        powerup.WorldPosition.y += 25f;
        powerup.WorldPosition.z -= 300f;
        powerup.ObjectOffsets!.x -= 100f;
        powerup.ObjectOffsets.y += 250f;
        powerup.ObjectOffsets.z += 150f;

        controls.MoveObject(powerup, audioPlayer: null, soundRegistry: null);

        Assert.AreEqual(anchoredWorld.x, powerup.WorldPosition!.x, 0.001f);
        Assert.AreEqual(anchoredWorld.y, powerup.WorldPosition.y, 0.001f);
        Assert.AreEqual(anchoredWorld.z, powerup.WorldPosition.z, 0.001f);
        Assert.AreEqual(anchoredOffsets.x, powerup.ObjectOffsets!.x, 0.001f);
        Assert.AreEqual(anchoredOffsets.y, powerup.ObjectOffsets.y, 0.001f);
        Assert.AreEqual(anchoredOffsets.z, powerup.ObjectOffsets.z, 0.001f);
    }

    private static _3dObject CreatePowerUp()
    {
        return new _3dObject
        {
            ObjectId = 30,
            ObjectName = "PowerUp",
            WorldPosition = new Vector3 { x = 1100f, y = 2f, z = 2100f },
            ObjectOffsets = new Vector3 { x = 20f, y = -150f, z = 450f },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>>
            {
                new()
                {
                    new Vector3 { x = -10f, y = -10f, z = -10f },
                    new Vector3 { x = 10f, y = 10f, z = 10f }
                }
            },
            ImpactStatus = new ImpactStatus { HasCrashed = false, ObjectName = "Ship", ObjectHealth = 1 },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "PowerUpBody",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "4488FF",
                            noHidden = true,
                            vert1 = new Vector3 { x = -10f, y = 0f, z = 0f },
                            vert2 = new Vector3 { x = 10f, y = 0f, z = 0f },
                            vert3 = new Vector3 { x = 0f, y = 12f, z = 0f }
                        }
                    }
                }
            }
        };
    }

    private static Vector3 Copy(IVector3 source)
    {
        return new Vector3
        {
            x = source.x,
            y = source.y,
            z = source.z
        };
    }
}
