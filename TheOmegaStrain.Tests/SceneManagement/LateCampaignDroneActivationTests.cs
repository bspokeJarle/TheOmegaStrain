using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class LateCampaignDroneActivationTests
{
    [TestMethod]
    public void Activation_RestartsAndSpreadsHuntDelaysAcrossConfiguredRange()
    {
        var activationTime = new DateTime(2026, 9, 22, 12, 0, 0, DateTimeKind.Local);
        var controls = new List<KamikazeDroneControls>();
        var aiObjects = new List<OmegaObject3D>();

        for (int i = 0; i < 14; i++)
        {
            var movement = new KamikazeDroneControls
            {
                StartHuntDateTime = activationTime.AddMinutes(-5)
            };
            controls.Add(movement);
            aiObjects.Add(new OmegaObject3D
            {
                ObjectId = 6000 + i,
                ObjectName = "KamikazeDrone",
                IsActive = false,
                Movement = movement
            });
        }

        int activated = DroneActivationHelpers.ActivateWithStaggeredHuntDelays(
            aiObjects,
            new Random(6061),
            activationTime);

        var delays = controls
            .Select(control => (control.StartHuntDateTime!.Value - activationTime).TotalSeconds)
            .ToList();
        Assert.AreEqual(aiObjects.Count, activated);
        Assert.IsTrue(aiObjects.All(drone => drone.IsActive));
        Assert.IsTrue(delays.All(delay => delay >= GameSetup.KamikazeDroneMinHuntDelay));
        Assert.IsTrue(delays.All(delay => delay < GameSetup.KamikazeDroneMaxHuntDelay));
        Assert.IsTrue(delays.Max() - delays.Min() > 30d,
            "Late-campaign drone hunts should be distributed broadly instead of arriving together.");
    }
}
