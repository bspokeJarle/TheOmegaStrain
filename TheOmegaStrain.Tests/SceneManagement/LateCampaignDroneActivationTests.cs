using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;

namespace TheOmegaStrain.Tests.SceneManagement;

[TestClass]
public class LateCampaignDroneActivationTests
{
    [TestMethod]
    public void Activation_RestartsHuntDelaysWithUnevenSpacing()
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
        var gaps = new List<double>();
        for (int i = 1; i < delays.Count; i++)
        {
            double gap = delays[i] - delays[i - 1];
            gaps.Add(gap);
            Assert.IsTrue(gap >= GameSetup.KamikazeDroneMinimumHuntSpacingSeconds &&
                gap < GameSetup.KamikazeDroneMinimumHuntSpacingSeconds + 20d,
                "Consecutive drones need a random launch gap of 20 to 40 seconds.");
        }
        Assert.IsTrue(gaps.Max() - gaps.Min() > 1d,
            "Drone launch gaps should vary rather than follow an even cadence.");
        Assert.IsTrue(delays[^1] > GameSetup.KamikazeDroneMaxHuntDelay,
            "Large waves need a longer launch window to preserve the minimum gap.");
    }

}
