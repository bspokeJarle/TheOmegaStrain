using System;
using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;

namespace TheOmegaStrain.Game.World.Objects;

public enum CloudVariant { Wide, Tall, Broken }

public static class Cloud
{
    // WideCloud spans approximately -119..123 on X before the 0.85 scale.
    public const float MaximumWidth = 250f;
    private const float CloudScale = 0.85f;

    public static OmegaObject3D CreateCloud(ISurface parentSurface, CloudVariant variant,
        SceneBiomeTypes biome = SceneBiomeTypes.Rainforrest)
    {
        var cloud = new OmegaObject3D
        {
            ObjectId = GameState.ObjectIdCounter++,
            ObjectName = "Cloud",
            ObjectOffsets = new Vector3(),
            WorldPosition = new Vector3(),
            Rotation = new Vector3(),
            ParentSurface = parentSurface,
            CrashBoxes = new List<List<IVector3>>(),
            CrashBoxDebugMode = false,
            HasShadow = true,
            IsActive = true,
            ImpactStatus = new ImpactStatus { ObjectName = "Cloud" }
        };
        cloud.ObjectParts.Add(new OmegaObjectPart3D
        {
            PartName = CloudVisualSetup.BodyPartName,
            Triangles = biome switch
            {
                SceneBiomeTypes.Winter => BuildWinterCloud(),
                _ => variant switch
                {
                    CloudVariant.Wide => BuildWideCloud(),
                    CloudVariant.Tall => BuildTallCloud(),
                    _ => BuildBrokenCloud()
                }
            },
            IsVisible = true
        });
        OmegaObject3DHelpers.ApplyScaleToObject(cloud, CloudScale);
        OmegaObject3DHelpers.AddSimplifiedShadowPart(cloud, useFlatQuad: true);
        return cloud;
    }

    private static List<ITriangleMeshWithColorAndTexture> BuildWideCloud()
    {
        var tris = new List<ITriangleMeshWithColorAndTexture>();
        AddLobe(tris, -67f, 5f, 0f, 52f, 25f, 30f);
        AddLobe(tris, -26f, -12f, 0f, 57f, 39f, 37f);
        AddLobe(tris, 27f, -18f, -2f, 61f, 43f, 39f);
        AddLobe(tris, 77f, 5f, 0f, 46f, 24f, 29f);
        AddLobe(tris, 4f, 11f, 7f, 81f, 21f, 40f);
        return tris;
    }

    private static List<ITriangleMeshWithColorAndTexture> BuildTallCloud()
    {
        var tris = new List<ITriangleMeshWithColorAndTexture>();
        AddLobe(tris, -58f, 12f, 0f, 42f, 23f, 27f);
        AddLobe(tris, -23f, -13f, 0f, 47f, 41f, 34f);
        AddLobe(tris, 10f, -42f, 2f, 45f, 52f, 35f);
        AddLobe(tris, 44f, -13f, -1f, 49f, 36f, 34f);
        AddLobe(tris, 71f, 11f, 0f, 35f, 20f, 25f);
        AddLobe(tris, 3f, 17f, 7f, 75f, 20f, 36f);
        return tris;
    }

    private static List<ITriangleMeshWithColorAndTexture> BuildBrokenCloud()
    {
        var tris = new List<ITriangleMeshWithColorAndTexture>();
        AddLobe(tris, -83f, 13f, 0f, 36f, 20f, 25f);
        AddLobe(tris, -53f, -11f, 0f, 48f, 36f, 33f);
        AddLobe(tris, -19f, -27f, 1f, 42f, 42f, 33f);
        AddLobe(tris, 18f, 7f, 3f, 41f, 25f, 30f);
        AddLobe(tris, 51f, -17f, 0f, 49f, 37f, 33f);
        AddLobe(tris, 84f, 11f, 0f, 35f, 20f, 25f);
        AddLobe(tris, 0f, 17f, 8f, 91f, 19f, 38f);
        return tris;
    }

    private static List<ITriangleMeshWithColorAndTexture> BuildWinterCloud()
    {
        var tris = new List<ITriangleMeshWithColorAndTexture>();
        AddLobe(tris, -37f, 3f, 0f, 41f, 27f, 29f, snowWhite: true);
        AddLobe(tris, 0f, -12f, 0f, 51f, 35f, 34f, snowWhite: true);
        AddLobe(tris, 41f, 4f, 0f, 40f, 25f, 28f, snowWhite: true);
        return tris;
    }

    // Each overlapping, faceted lobe has a bright crown and a flatter shaded underside.
    // Only the object mesh is shaped here; its world position remains fixed.
    private static void AddLobe(List<ITriangleMeshWithColorAndTexture> tris,
        float x, float y, float z, float radiusX, float radiusY, float radiusZ,
        bool snowWhite = false)
    {
        const int segments = 10;
        var center = new Vector3(x, y, z);
        var top = new Vector3(x, y - radiusY, z);
        var bottom = new Vector3(x, y + radiusY * 0.8f, z);
        var upper = new Vector3[segments];
        var middle = new Vector3[segments];
        var lower = new Vector3[segments];

        for (int i = 0; i < segments; i++)
        {
            float angle = 2f * MathF.PI * i / segments;
            float cosine = MathF.Cos(angle);
            float sine = MathF.Sin(angle);
            upper[i] = new Vector3(x + radiusX * 0.72f * cosine, y - radiusY * 0.65f, z + radiusZ * 0.72f * sine);
            middle[i] = new Vector3(x + radiusX * cosine, y, z + radiusZ * sine);
            lower[i] = new Vector3(x + radiusX * 0.82f * cosine, y + radiusY * 0.46f, z + radiusZ * 0.82f * sine);
        }

        for (int i = 0; i < segments; i++)
        {
            int next = (i + 1) % segments;
            tris.Add(OmegaObject3DHelpers.CreateTriangleOutward(top, upper[i], upper[next], center,
                snowWhite ? "FFFFFF" : "F7FAFB"));
            OmegaObject3DHelpers.AddQuadOutward(tris, upper[i], middle[i], middle[next], upper[next], center,
                snowWhite ? "FDFEFF" : "E9F1F3");
            OmegaObject3DHelpers.AddQuadOutward(tris, middle[i], lower[i], lower[next], middle[next], center,
                snowWhite ? "F8FCFF" : "CDD8DC");
            tris.Add(OmegaObject3DHelpers.CreateTriangleOutward(lower[i], bottom, lower[next], center,
                snowWhite ? "F3F9FF" : "ABBBC1"));
        }
    }
}
