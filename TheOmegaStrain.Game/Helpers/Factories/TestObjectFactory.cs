using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Game.World;
using TheOmegaStrain.Domain;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.Helpers
{
    public static class TestObjectFactory
    {
        public static OmegaObject3D CreateDynamicTestObject()
        {
            return new OmegaObject3D
            {
                ObjectId = 1,
                ObjectName = "DynamicObject",
                SurfaceBasedId = 0,
                CrashBoxes = new List<List<IVector3>>
                {
                    new List<IVector3>
                    {
                        new Vector3 { x = -25, y = 0, z = -25 },
                        new Vector3 { x =  25, y = 50, z =  25 }
                    }
                },
                ObjectParts = new List<I3dObjectPart>
                {
                    new OmegaObjectPart3D
                    {
                        IsVisible = true,
                        PartName = "Body",
                        Triangles = new List<ITriangleMeshWithColor>
                        {
                            new TriangleMeshWithColor
                            {
                                vert1 = new Vector3 { x = -25, y = 0, z = -25 },
                                vert2 = new Vector3 { x =  25, y = 0, z = -25 },
                                vert3 = new Vector3 { x =  0,  y = 50, z =  25 }
                            }
                        }
                    }
                },
                ObjectOffsets = new Vector3 { x = 1000, y = 0, z = 1000 } // position it onscreen
            };
        }
        public static OmegaObject3D CreateSurfaceBasedTestObject()
        {
            return new OmegaObject3D
            {
                ObjectId = 2,
                ObjectName = "House",
                SurfaceBasedId = 1,
                CrashBoxes = new List<List<IVector3>>
                {
                    new List<IVector3>
                    {
                        new Vector3 { x = -50, y = -25, z = -25 },
                        new Vector3 { x =  50, y =  25, z =  25 }
                    }
                },
                ObjectParts = new List<I3dObjectPart>
                {
                    new OmegaObjectPart3D
                    {
                        IsVisible = true,
                        PartName = "Main",
                        Triangles = new List<ITriangleMeshWithColor>
                        {
                            new TriangleMeshWithColor
                            {
                                vert1 = new Vector3 { x = -50, y = -25, z = -25 },
                                vert2 = new Vector3 { x =  50, y = -25, z = -25 },
                                vert3 = new Vector3 { x =  0,  y =  25, z =  25 }
                            }
                        }
                    }
                },
                ParentSurface = new Surface
                {
                    RotatedSurfaceTriangles = new List<ITriangleMeshWithColor>
                    {
                        new TriangleMeshWithColor
                        {
                            landBasedPosition = 1,
                            vert1 = new Vector3 { x = 1000, y = 50, z = 1000 }
                        }
                    }
                }
            };
        }
    }
    internal class ObjectPart : I3dObjectPart
    {
        public string PartName { get; set; }
        public bool IsVisible { get; set; }
        public List<ITriangleMeshWithColor> Triangles { get; set; } = new();
    }
}
