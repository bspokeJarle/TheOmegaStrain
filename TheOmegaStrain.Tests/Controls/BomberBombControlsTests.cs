using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using GameAiAndControls.Controls;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class BomberBombControlsTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.GamePlayState = new GamePlayState();
        GameState.SurfaceState = new SurfaceState();
        GameState.SurfaceState.AiObjects = new List<_3dObject>();
    }

    [TestMethod]
    public void MoveObject_WhenBombCrashes_ExplosionKeepsSurfaceImpactMetadata()
    {
        var control = new BomberBombControls();
        var bomb = new _3dObject
        {
            ObjectId = 101,
            ObjectName = "BomberBomb",
            IsActive = true,
            WorldPosition = new Vector3 { x = 1000, y = 0, z = 2000 },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 500 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } },
            ImpactStatus = new ImpactStatus { HasCrashed = true },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "BombPart",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 0, y = 0, z = 0 },
                            vert2 = new Vector3 { x = 1, y = 0, z = 0 },
                            vert3 = new Vector3 { x = 0, y = 1, z = 0 },
                            Color = "FFFFFF"
                        }
                    }
                }
            }
        };

        GameState.SurfaceState.AiObjects.Add(bomb);

        // First move starts explosion path.
        control.MoveObject(bomb, null, null);

        Assert.IsTrue(bomb.ImpactStatus!.HasCrashed, "Bomb should remain marked as crashed.");
        Assert.AreEqual("Surface", bomb.ImpactStatus.ObjectName,
            "Bomb impact target should be persisted as Surface for crater detection.");
    }

    [TestMethod]
    public void MoveObject_WhenBombExplodes_UsesStrongerExplosionForce()
    {
        var physics = new CapturingPhysics();
        var control = new BomberBombControls
        {
            Physics = physics
        };
        var bomb = CreateCrashedBomb();

        GameState.SurfaceState.AiObjects.Add(bomb);

        control.MoveObject(bomb, null, null);

        Assert.AreEqual(225f, physics.LastExplosionForce, 0.001f);
    }

    private static _3dObject CreateCrashedBomb()
    {
        return new _3dObject
        {
            ObjectId = 102,
            ObjectName = "BomberBomb",
            IsActive = true,
            WorldPosition = new Vector3 { x = 1000, y = 0, z = 2000 },
            ObjectOffsets = new Vector3 { x = 0, y = 0, z = 500 },
            Rotation = new Vector3(),
            CrashBoxes = new List<List<IVector3>> { new() { new Vector3(), new Vector3 { x = 1, y = 1, z = 1 } } },
            ImpactStatus = new ImpactStatus { HasCrashed = true },
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "BombPart",
                    IsVisible = true,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            vert1 = new Vector3 { x = 0, y = 0, z = 0 },
                            vert2 = new Vector3 { x = 1, y = 0, z = 0 },
                            vert3 = new Vector3 { x = 0, y = 1, z = 0 },
                            Color = "FFFFFF"
                        }
                    }
                }
            }
        };
    }

    private sealed class CapturingPhysics : IPhysics
    {
        public float LastExplosionForce { get; private set; }
        public float Mass { get; set; }
        public IVector3 Velocity { get; set; } = new Vector3();
        public float Thrust { get; set; }
        public float Friction { get; set; }
        public float MaxSpeed { get; set; }
        public float MaxThrust { get; set; }
        public float GravityStrength { get; set; }
        public IVector3 GravitySource { get; set; } = new Vector3();
        public IVector3 Acceleration { get; set; } = new Vector3();
        public int BounceCooldownFrames { get; set; }
        public float BounceHeightMultiplier { get; set; }
        public string? ExplosionColorOverride { get; set; }
        public float FallVelocity { get; set; }
        public float InertiaX { get; set; }
        public float InertiaY { get; set; }
        public float InertiaZ { get; set; }
        public float ThrustEffect { get; set; }
        public float VerticalLiftFactor { get; set; }
        public float GravityAcceleration { get; set; }
        public float TerminalFallSpeed { get; set; }
        public float GravityPullMultiplier { get; set; }
        public float ThrustSpeedMultiplier { get; set; }
        public float ThrustHeightMultiplier { get; set; }
        public float ThrustRampRate { get; set; }
        public float InertiaDrag { get; set; }
        public float MaxInertia { get; set; }
        public float VerticalThrustSmoothing { get; set; }
        public float VerticalLiftRate { get; set; }
        public float CeilingHeight { get; set; }
        public float FloorHeight { get; set; }
        public float MaxScreenDrop { get; set; }
        public float HoverElapsed { get; set; }
        public float HoverFloatDuration { get; set; }
        public float HoverRampDuration { get; set; }
        public float HoverMinGravityScale { get; set; }
        public float AirborneSettleRate { get; set; }

        public IVector3 ApplyDragForce(IVector3 currentPosition, float deltaTime) => currentPosition;
        public IVector3 ApplyForces(IVector3 currentPosition, float deltaTime) => currentPosition;
        public IVector3 ApplyGravityForce(IVector3 currentPosition, float deltaTime) => currentPosition;
        public IVector3 ApplyThrust(IVector3 currentPosition, IVector3 direction, float deltaTime) => currentPosition;
        public IVector3 ApplyRotationDragForce(IVector3 rotationVector) => rotationVector;
        public void Bounce(Vector3 normal, ImpactDirection? direction) { }
        public void TiltStabilization(ref IVector3 tiltState) { }
        public I3dObject ExplodeObject(I3dObject explodingObject, float explosionForece)
        {
            LastExplosionForce = explosionForece;
            return explodingObject;
        }
        public I3dObject UpdateExplosion(I3dObject explodingObject, DateTime deltaTime) => explodingObject;
        public void ResetHover() { }
        public float ApplyFallGravity(float rotationDegrees, float deltaTime) => 0f;
        public void ReduceFallWithThrust(float thrust, float rotationDegrees, float deltaTime) { }
        public float CalculateThrustForces(float thrust, float tiltDegrees, float rotationDegrees, float deltaTime) => 0f;
        public float CalculateCurrentSpeed(bool isLanded) => 0f;
        public float ClampToHeightRange(float value) => value;
        public float ClampToScreenDrop(float value) => value;
        public float WrapPosition(float position, float diff, float minValue, float maxValue) => position;
    }
}
