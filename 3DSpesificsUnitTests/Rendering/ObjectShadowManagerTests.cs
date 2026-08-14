using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using Domain;
using TheOmegaStrain.Runtime.Rendering;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Rendering;

[TestClass]
public class ObjectShadowManagerTests
{
    [TestInitialize]
    public void Setup()
    {
        GameState.SurfaceState = new SurfaceState
        {
            GlobalMapPosition = new Vector3(),
            SurfaceViewportObject = new _3dObject
            {
                ObjectId = 1,
                ObjectName = "Surface",
                ObjectOffsets = new Vector3 { x = 0f, y = 500f, z = 0f },
                WorldPosition = new Vector3(),
                Rotation = new Vector3()
            }
        };
        GameState.ObjectIdCounter = 10;
    }

    [TestMethod]
    public void FreeFlyingShadow_InterpolatesGroundYInsideSurfaceTriangle()
    {
        float oldStaticOffsetY = ObjectShadowManager.StaticOffsetY;
        try
        {
            ObjectShadowManager.StaticOffsetY = -40f;

            var surface = new Surface
            {
                RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>
                {
                    new TriangleMeshWithColor
                    {
                        vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                        vert2 = new Vector3 { x = 100f, y = 100f, z = 0f },
                        vert3 = new Vector3 { x = 0f, y = 0f, z = 100f }
                    }
                }
            };

            var flyingObject = CreateFreeFlyingShadowCaster(surface, x: 25f, y: 400f, z: 25f);
            var shadows = new List<_3dObject>();

            new ObjectShadowManager().HandleObjectShadow(flyingObject, shadows);

            Assert.AreEqual(1, shadows.Count);
            var shadowVertex = shadows[0].ObjectParts[0].Triangles[0].vert1;

            Assert.AreEqual(
                25f + ObjectShadowManager.StaticOffsetY,
                shadowVertex.y,
                0.001f,
                "Free-flying shadows should use barycentric surface Y under the object, not the nearest tile center.");
        }
        finally
        {
            ObjectShadowManager.StaticOffsetY = oldStaticOffsetY;
        }
    }

    private static _3dObject CreateFreeFlyingShadowCaster(Surface surface, float x, float y, float z)
    {
        return new _3dObject
        {
            ObjectId = 2,
            ObjectName = "KamikazeDrone",
            HasShadow = true,
            ParentSurface = surface,
            ObjectOffsets = new Vector3 { x = x, y = y, z = z },
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            ObjectParts = new List<I3dObjectPart>
            {
                new _3dObjectPart
                {
                    PartName = "Shadow",
                    IsVisible = false,
                    Triangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            Color = "000000",
                            noHidden = true,
                            vert1 = new Vector3(),
                            vert2 = new Vector3(),
                            vert3 = new Vector3()
                        }
                    }
                }
            }
        };
    }
}
