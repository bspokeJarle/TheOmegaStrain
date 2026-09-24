using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class SpaceSwanPlacementHelpersTests
{
    [TestMethod]
    public void FiftySwansMaintainMinimumSpacingAtDifferentWorldScales()
    {
        foreach (float scale in new[] { 0.55f, 1f })
        {
            var random = new Random(42);
            var objects = new List<I3dObject>();
            for (int i = 0; i < 50; i++)
            {
                var position = SpaceSwanPlacementHelpers.GetNextPosition(random, objects, scale);
                foreach (var swan in objects)
                {
                    float dx = position.x - swan.WorldPosition.x;
                    float dz = position.z - swan.WorldPosition.z;
                    float distance = MathF.Sqrt(dx * dx + dz * dz);
                    Assert.IsTrue(distance >= SpaceSwanPlacementHelpers.MinimumSpacing * scale,
                        $"Swan {i} was only {distance} units from another swan at scale {scale}.");
                }
                objects.Add(new OmegaObject3D { ObjectId = i + 1, ObjectName = "SpaceSwan", WorldPosition = position });
            }
        }
    }
}
