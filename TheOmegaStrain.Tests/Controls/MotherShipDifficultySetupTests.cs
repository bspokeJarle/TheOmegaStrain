using CommonUtilities.CommonSetup;

namespace _3DSpesificsUnitTests.Controls;

[TestClass]
public class MotherShipDifficultySetupTests
{
    [TestMethod]
    public void HigherAggression_MakesMothershipsFasterAndMoreAccurate()
    {
        float aggression = 1.5f;

        Assert.IsTrue(MotherShipDifficultySetup.ScaleTurnSpeed(45f, aggression) > 45f);
        Assert.IsTrue(MotherShipDifficultySetup.ScaleTravelSpeed(120f, aggression) > 120f);
        Assert.IsTrue(MotherShipDifficultySetup.ScaleCooldown(5f, aggression) < 5f);
        Assert.IsTrue(MotherShipDifficultySetup.ScaleCooldown(5f, 1.35f) < MotherShipDifficultySetup.ScaleCooldown(5f, 1.05f));
        Assert.IsTrue(MotherShipDifficultySetup.ScaleUpdateInterval(0.5f, aggression) < 0.5f);
        Assert.IsTrue(MotherShipDifficultySetup.ScaleChargeWindow(2f, aggression) < 2f);
    }

    [TestMethod]
    public void MotherShipHealths_AreLongerAndProgressive()
    {
        int lazerDamage = WeaponSetup.GetWeaponDamage("Lazer");

        Assert.AreEqual(26, HitsToDestroy(EnemySetup.MotherShipSmallHealth, lazerDamage));
        Assert.AreEqual(36, HitsToDestroy(EnemySetup.MotherShipMediumHealth, lazerDamage));
        Assert.AreEqual(60, HitsToDestroy(EnemySetup.MotherShipLargeHealth, lazerDamage));
    }

    [TestMethod]
    public void MotherShipHealth_ScalesUpWithAggressionWithoutDroppingBelowBase()
    {
        Assert.AreEqual(
            EnemySetup.MotherShipSmallHealth,
            EnemySetup.GetMotherShipHealth("MotherShipSmall", 0.90f));

        Assert.AreEqual(
            1573,
            EnemySetup.GetMotherShipHealth("MotherShipSmall", 1.10f));

        Assert.AreEqual(
            2600,
            EnemySetup.GetMotherShipHealth("MotherShipMedium", 1.35f));
    }

    [TestMethod]
    public void Aggression_IsClampedToPlayableRange()
    {
        Assert.AreEqual(
            MotherShipDifficultySetup.MinAggression,
            MotherShipDifficultySetup.GetAggression(0.01f));

        Assert.AreEqual(
            MotherShipDifficultySetup.MaxAggression,
            MotherShipDifficultySetup.GetAggression(99f));
    }

    private static int HitsToDestroy(int health, int damage)
    {
        return (health + damage - 1) / damage;
    }
}
