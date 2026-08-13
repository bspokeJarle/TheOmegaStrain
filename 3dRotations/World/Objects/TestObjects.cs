using _3dRotations.World;
using _3dTesting.Helpers;
using CommonUtilities.CommonGlobalState;
using STL_Tools;
using System;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class TestObjects
    {
        public static _3dObject CreateTestObject()
        {
            var rd = new Random();
            var obj = rd.Next(1, 4);

            var testobj = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            var modelReader = new STLReader();

            if (obj == 1)
            {
                // Add orb as an inhabitant                
                modelReader = new STLReader("C:\\Users\\kh979\\Documents\\Privat\\Bspoke prosjekter\\3dProsjekt\\3dProsjekt\\3dTesting\\3d objects\\div\\complexorb.stl");
                testobj.ObjectParts.Add(new _3dObjectPart { PartName = "ComplexOrb", Triangles = _3dObjectHelpers.ConvertToTrianglesWithColor(modelReader.ReadFile().ToList(), "FF6644") });
                return testobj;
            }
            if (obj==2)
            {
                //Add cube as an inhabitant                
                modelReader = new STLReader("C:\\Users\\kh979\\Documents\\Privat\\Bspoke prosjekter\\3dProsjekt\\3dProsjekt\\3dTesting\\3d objects\\div\\3dcube.stl");
                testobj.ObjectParts.Add(new _3dObjectPart { PartName = "Cube", Triangles = _3dObjectHelpers.ConvertToTrianglesWithColor(modelReader.ReadFile().ToList(), "00FF00") });
                return testobj;
            }            
            modelReader = new STLReader("C:\\Users\\kh979\\Documents\\Privat\\Bspoke prosjekter\\3dProsjekt\\3dProsjekt\\3dTesting\\3d objects\\div\\complexorb.stl");
            testobj.ObjectParts.Add(new _3dObjectPart { PartName = "ComplexOrb", Triangles = _3dObjectHelpers.ConvertToTrianglesWithColor(modelReader.ReadFile().ToList(), "FF6644") });
            return testobj;
        }
    }
}
