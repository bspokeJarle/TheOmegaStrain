using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.World.Objects;

namespace TheOmegaStrain.Tests.WorldObjects;

[TestClass]
public class MedKitPickupGeometryTests
{
    private static readonly string[] BodyParts =
    {
        "MedKitCase", "MedKitCross", "MedKitBandAid", "MedKitHandle", "MedKitHardware"
    };

    [TestMethod]
    public void MedKit_BodyTriangleBudget_IsAroundOneThousand()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);
        int body = BodyTriangleCount(kit);

        Assert.IsTrue(body is >= 900 and <= 1100,
            $"Expected 900-1100 body triangles, got {body}. Per-part: {PerPart(kit)}");
    }

    [TestMethod]
    public void MedKit_RequiredParts_ArePresentVisibleAndNonEmpty()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);

        foreach (var name in BodyParts)
        {
            var part = kit.ObjectParts.SingleOrDefault(p => p.PartName == name);
            Assert.IsNotNull(part, $"Missing part '{name}'.");
            Assert.IsTrue(part!.IsVisible, $"Part '{name}' should be visible.");
            Assert.IsTrue(part.Triangles.Count > 0, $"Part '{name}' has no triangles.");
        }

        // The shadow is a separate, non-body part and must not inflate the budget.
        Assert.IsTrue(kit.ObjectParts.Any(p => p.PartName == "Shadow"),
            "Expected a generated Shadow part.");
    }

    [TestMethod]
    public void MedKit_AllVerticesFinite_AndBodyTrianglesHaveArea()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);

        foreach (var part in kit.ObjectParts.Where(p => BodyParts.Contains(p.PartName)))
        {
            foreach (var tri in part.Triangles)
            {
                foreach (var v in new[] { tri.vert1, tri.vert2, tri.vert3 })
                {
                    Assert.IsTrue(float.IsFinite(v.x) && float.IsFinite(v.y) && float.IsFinite(v.z),
                        $"Non-finite vertex in '{part.PartName}'.");
                }

                Assert.IsTrue(TriangleArea(tri) > 1e-3f,
                    $"Degenerate (zero-area) triangle in '{part.PartName}'.");
            }
        }
    }

    [TestMethod]
    public void MedKit_AllColors_AreValidSixDigitHex()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);

        foreach (var part in kit.ObjectParts)
            foreach (var tri in part.Triangles)
            {
                Assert.IsNotNull(tri.Color, $"Null colour in '{part.PartName}'.");
                Assert.AreEqual(6, tri.Color!.Length, $"Colour '{tri.Color}' is not 6 hex digits.");
                Assert.IsTrue(int.TryParse(tri.Color, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out _),
                    $"Colour '{tri.Color}' is not valid hex in '{part.PartName}'.");
            }
    }

    [TestMethod]
    public void MedKit_Dimensions_MatchTheAuthoredCaseAndHandle()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);
        var (min, max) = BodyBounds(kit);

        // Case footprint ~ +/-40 x, +/-27 y (ZoomRatio = 1).
        Assert.IsTrue(Math.Abs(max.x - 40f) < 3f && Math.Abs(min.x + 40f) < 3f,
            $"X extent {min.x:F1}..{max.x:F1} not near +/-40.");
        Assert.IsTrue(Math.Abs(max.y - 27f) < 3f && Math.Abs(min.y + 27f) < 3f,
            $"Y extent {min.y:F1}..{max.y:F1} not near +/-27.");
        // Handle rises well above the lid; case floor near -17.
        Assert.IsTrue(max.z > 28f, $"Top z {max.z:F1} too low; handle should rise above the lid.");
        Assert.IsTrue(Math.Abs(min.z + 17f) < 3f, $"Bottom z {min.z:F1} not near -17.");
    }

    [TestMethod]
    public void MedKit_CrashBox_IsSingleAabbEnclosingTheBody()
    {
        var kit = MedKitPickup.CreateMedKit(parentSurface: null!);
        Assert.IsNotNull(kit.CrashBoxes);
        Assert.AreEqual(1, kit.CrashBoxes.Count, "Expected exactly one crash box.");
        Assert.AreEqual(8, kit.CrashBoxes[0].Count, "Crash box should have 8 corners.");

        var (bmin, bmax) = BodyBounds(kit);
        float cxMin = kit.CrashBoxes[0].Min(c => c.x), cxMax = kit.CrashBoxes[0].Max(c => c.x);
        float cyMin = kit.CrashBoxes[0].Min(c => c.y), cyMax = kit.CrashBoxes[0].Max(c => c.y);
        float czMin = kit.CrashBoxes[0].Min(c => c.z), czMax = kit.CrashBoxes[0].Max(c => c.z);

        Assert.IsTrue(cxMin <= bmin.x && cxMax >= bmax.x, "Crash box does not enclose body in X.");
        Assert.IsTrue(cyMin <= bmin.y && cyMax >= bmax.y, "Crash box does not enclose body in Y.");
        Assert.IsTrue(czMin <= bmin.z && czMax >= bmax.z, "Crash box does not enclose body in Z.");
    }

    [TestMethod]
    public void MedKit_TwoCalls_DoNotShareMutableVerticesAndGetDistinctIds()
    {
        var a = MedKitPickup.CreateMedKit(parentSurface: null!);
        var b = MedKitPickup.CreateMedKit(parentSurface: null!);

        Assert.AreNotEqual(a.ObjectId, b.ObjectId, "Two pickups should get distinct ObjectIds.");

        var triA = a.ObjectParts.First(p => p.PartName == "MedKitCase").Triangles[0];
        var triB = b.ObjectParts.First(p => p.PartName == "MedKitCase").Triangles[0];

        float before = triB.vert1.x;
        triA.vert1.x += 12345f;
        Assert.AreEqual(before, triB.vert1.x,
            "Mutating one pickup changed another -> vertices are shared between instances.");
    }

    // ---- helpers -------------------------------------------------------------

    private static int BodyTriangleCount(OmegaObject3D kit) =>
        kit.ObjectParts.Where(p => BodyParts.Contains(p.PartName)).Sum(p => p.Triangles.Count);

    private static string PerPart(OmegaObject3D kit) =>
        string.Join(", ", kit.ObjectParts
            .Where(p => BodyParts.Contains(p.PartName))
            .Select(p => $"{p.PartName}={p.Triangles.Count}"));

    private static float TriangleArea(ITriangleMeshWithColorAndTexture t)
    {
        float ux = t.vert2.x - t.vert1.x, uy = t.vert2.y - t.vert1.y, uz = t.vert2.z - t.vert1.z;
        float vx = t.vert3.x - t.vert1.x, vy = t.vert3.y - t.vert1.y, vz = t.vert3.z - t.vert1.z;
        float cx = uy * vz - uz * vy, cy = uz * vx - ux * vz, cz = ux * vy - uy * vx;
        return 0.5f * (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);
    }

    private static (Vector3 min, Vector3 max) BodyBounds(OmegaObject3D kit)
    {
        var min = new Vector3 { x = float.MaxValue, y = float.MaxValue, z = float.MaxValue };
        var max = new Vector3 { x = float.MinValue, y = float.MinValue, z = float.MinValue };
        foreach (var part in kit.ObjectParts.Where(p => BodyParts.Contains(p.PartName)))
            foreach (var tri in part.Triangles)
                foreach (var v in new[] { tri.vert1, tri.vert2, tri.vert3 })
                {
                    min.x = Math.Min(min.x, v.x); min.y = Math.Min(min.y, v.y); min.z = Math.Min(min.z, v.z);
                    max.x = Math.Max(max.x, v.x); max.y = Math.Max(max.y, v.y); max.z = Math.Max(max.z, v.z);
                }
        return (min, max);
    }
}
