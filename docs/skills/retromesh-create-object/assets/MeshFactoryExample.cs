using System;
using System.Collections.Generic;
using RetroMesh.Engine;

namespace RetroMeshSkillExample;

// Engine-only API example, not a finished truck. Reuse the host's triangle type
// instead of adding this class when that host already supplies one.
public sealed class ModelTriangle : EngineTriangleMesh, ITriangleMeshWithColorAndTexture
{
    public string? Color { get; set; }
    public string? TextureId { get; set; }
    public TextureCoordinate Uv1 { get; set; }
    public TextureCoordinate Uv2 { get; set; }
    public TextureCoordinate Uv3 { get; set; }
}

public static class MeshFactoryExample
{
    // Local authoring choice: X length, Y width, Z height; pivot at geometric centre.
    // No camera/world convention, movement, scene registration or global state.
    public static Engine3dObject CreateBlock(int objectId, float scale = 1f)
    {
        if (!float.IsFinite(scale) || scale <= 0f)
            throw new ArgumentOutOfRangeException(nameof(scale));

        var obj = new Engine3dObject
        {
            ObjectId = objectId,
            ObjectName = "ExampleBlock",
            WorldPosition = new EngineVector3(),
            ObjectOffsets = new EngineVector3(),
            Rotation = new EngineVector3(),
            CrashBoxes = new List<List<IVector3>>()
        };

        var min = new EngineVector3(-60f, -30f, -20f);
        var max = new EngineVector3(60f, 30f, 20f);
        var centre = new EngineVector3();
        var a = new EngineVector3(min.x, min.y, min.z);
        var b = new EngineVector3(max.x, min.y, min.z);
        var c = new EngineVector3(max.x, max.y, min.z);
        var d = new EngineVector3(min.x, max.y, min.z);
        var e = new EngineVector3(min.x, min.y, max.z);
        var f = new EngineVector3(max.x, min.y, max.z);
        var g = new EngineVector3(max.x, max.y, max.z);
        var h = new EngineVector3(min.x, max.y, max.z);
        var triangles = new List<ITriangleMeshWithColorAndTexture>();

        AddFace(a, b, c, d, "4C6178");
        AddFace(e, f, g, h, "94B0C9");
        AddFace(a, b, f, e, "6C89A3");
        AddFace(b, c, g, f, "7896B1");
        AddFace(c, d, h, g, "6C89A3");
        AddFace(d, a, e, h, "7896B1");

        void AddFace(IVector3 v1, IVector3 v2, IVector3 v3, IVector3 v4, string color)
        {
            MeshGeometryOperations.AddQuadOutward(triangles, v1, v2, v3, v4,
                centre, color, static () => new ModelTriangle());
        }

        // Rotation works triangle by triangle: detach shared corner references first.
        var body = EngineObjectCloner.CopyTriangles(triangles,
            static () => new ModelTriangle(), CopyVector);
        obj.ObjectParts.Add(new Engine3dObjectPart
        {
            PartName = "Body",
            IsVisible = true,
            Triangles = body
        });

        // Optional collision example; remove this box for a geometry-only prop.
        obj.CrashBoxes.Add(MeshGeometryOperations.GenerateCrashBoxCorners<IVector3>(
            min, max, static (x, y, z) => new EngineVector3(x, y, z)));
        MeshGeometryOperations.ApplyScaleToObject(obj, scale, CopyVector);

        // No pose change: initialize normals for an unrotated preview. A real host
        // applies its own rotation order and refreshes normals as part of that pass.
        new MeshRotation().RotateZMesh(body, 0f);
        return obj;
    }

    private static IVector3 CopyVector(IVector3 value)
        => new EngineVector3(value.x, value.y, value.z);
}
