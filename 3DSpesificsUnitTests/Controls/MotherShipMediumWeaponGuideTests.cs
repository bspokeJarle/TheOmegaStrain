using CommonUtilities.OmegaEngineAdapters;
using Domain;
using GameAiAndControls.Controls.MotherShipMediumControls;
using _3dRotations.World.Objects;
using static Domain._3dSpecificsImplementations;

namespace _3DSpesificsUnitTests.Controls;

// These tests pin down the MotherShipMedium weapon-guide pipeline so we stop
// regressing on the same bug.
//
// The live wiring is:
//   1. `MotherShipMedium.CreateMotherShipMedium` MUST add two parts named
//      EXACTLY "WeaponStartGuide" and "WeaponDirectionGuide" (same names the
//      Ship uses) — any other names silently bypass LiveGameLoop's dispatch
//      and the lazer spawns at (0,0,0).
//   2. `LiveGameLoop.SetMovementGuides` routes those parts (after per-frame
//      rotation) into `IObjectMovement.SetWeaponGuideCoordinates` passing the
//      START triangle as the first arg and NULL as the second, and the
//      DIRECTION triangle as the second arg and NULL as the first.
//   3. `MotherShipMediumControls.SetWeaponGuideCoordinates` must therefore
//      accept each call independently (not clobber one with null) and end up
//      with both `_weaponStartGuide` and `_weaponDirectionGuide` populated.
[TestClass]
public class MotherShipMediumWeaponGuideTests
{
    [TestMethod]
    public void MotherShipMedium_exposes_WeaponStartGuide_and_WeaponDirectionGuide_parts()
    {
        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        Assert.IsNotNull(ship.ObjectParts, "ObjectParts missing on MotherShipMedium");

        var start = ship.ObjectParts.Find(p => p.PartName == "WeaponStartGuide");
        var dir   = ship.ObjectParts.Find(p => p.PartName == "WeaponDirectionGuide");

        Assert.IsNotNull(start, "WeaponStartGuide part not found — LiveGameLoop will not dispatch the start vertex and the lazer will spawn at (0,0,0).");
        Assert.IsNotNull(dir,   "WeaponDirectionGuide part not found — LiveGameLoop will not dispatch the direction vertex.");

        Assert.IsTrue(start.Triangles.Count >= 1, "WeaponStartGuide has no triangles.");
        Assert.IsTrue(dir.Triangles.Count   >= 1, "WeaponDirectionGuide has no triangles.");
    }

    [TestMethod]
    public void WeaponStartGuide_vert1_is_near_muzzle_and_WeaponDirectionGuide_vert1_is_far_ahead()
    {
        var ship = MotherShipMedium.CreateMotherShipMedium(parentSurface: null!);

        var start = ship.ObjectParts.Find(p => p.PartName == "WeaponStartGuide")!.Triangles[0];
        var dir   = ship.ObjectParts.Find(p => p.PartName == "WeaponDirectionGuide")!.Triangles[0];

        // The muzzle tip sits at x = 148 in raw geometry, but CreateMotherShipMedium
        // scales everything by ZoomRatio (~1.38), so after construction muzzleX ≈ 204.
        // The WeaponStartGuide should be just outside the (scaled) muzzle; the
        // WeaponDirectionGuide must be substantially further ahead so (direction - start)
        // is a clearly-forward unit vector.
        const float scaledMuzzleX = 148f * 1.38f; // ≈ 204.24
        Assert.IsTrue(start.vert1.x > scaledMuzzleX && start.vert1.x < scaledMuzzleX + 60f,
            $"WeaponStartGuide.vert1.x = {start.vert1.x}; expected just outside the scaled muzzle (around {scaledMuzzleX:F1}..{scaledMuzzleX + 60f:F1}).");
        Assert.IsTrue(dir.vert1.x > start.vert1.x + 50f,
            $"WeaponDirectionGuide.vert1.x ({dir.vert1.x}) must be well ahead of WeaponStartGuide.vert1.x ({start.vert1.x}).");
    }

    [TestMethod]
    public void SetWeaponGuideCoordinates_accepts_start_and_direction_independently()
    {
        // This mimics LiveGameLoop.SetMovementGuides — which always calls the
        // setter twice, once for the start (passing null for direction) and once
        // for the direction (passing null for start). A naive setter that stores
        // null into the other field would wipe it out.
        var ctrl = new MotherShipMediumControls();

        var startTri = new TriangleMeshWithColor
        {
            vert1 = new Vector3 { x = 156f, y = 4f, z = 2f },
            vert2 = new Vector3 { x = 156f, y = -4f, z = 2f },
            vert3 = new Vector3 { x = 164f, y = 0f, z = 0f },
        };
        var dirTri = new TriangleMeshWithColor
        {
            vert1 = new Vector3 { x = 280f, y = 4f, z = 2f },
            vert2 = new Vector3 { x = 280f, y = -4f, z = 2f },
            vert3 = new Vector3 { x = 290f, y = 0f, z = 0f },
        };

        // Order the same way LiveGameLoop would dispatch them each frame.
        ctrl.SetWeaponGuideCoordinates(startTri, null!);
        ctrl.SetWeaponGuideCoordinates(null!, dirTri);

        // Reflection — _weaponStartGuide / _weaponDirectionGuide are private.
        var t = typeof(MotherShipMediumControls);
        var startField = t.GetField("_weaponStartGuide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var dirField   = t.GetField("_weaponDirectionGuide", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

        Assert.IsNotNull(startField, "_weaponStartGuide field missing.");
        Assert.IsNotNull(dirField,   "_weaponDirectionGuide field missing.");

        var storedStart = startField!.GetValue(ctrl) as ITriangleMeshWithColor;
        var storedDir   = dirField!.GetValue(ctrl)   as ITriangleMeshWithColor;

        Assert.IsNotNull(storedStart, "WeaponStartGuide was not stored — second call with null start must not clobber it.");
        Assert.IsNotNull(storedDir,   "WeaponDirectionGuide was not stored.");

        Assert.AreEqual(156f, storedStart!.vert1.x, 0.001f);
        Assert.AreEqual(280f, storedDir!.vert1.x,   0.001f);
    }
}
