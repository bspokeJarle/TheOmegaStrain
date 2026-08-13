using _3dRotations.World.Objects;
using _3dRotations.World;
using Domain;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Helpers
{
    public static class TestObjectFactory
    {
        public static _3dObject CreateDynamicTestObject()
        {
            return new _3dObject
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
                    new _3dObjectPart
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
        public static _3dObject CreateSurfaceBasedTestObject()
        {
            return new _3dObject
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
                    new _3dObjectPart
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
