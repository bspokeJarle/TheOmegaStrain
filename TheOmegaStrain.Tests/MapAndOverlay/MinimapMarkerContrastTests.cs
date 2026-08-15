using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Wpf.Helpers;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Tests.MapAndOverlay;

[TestClass]
public class MinimapMarkerContrastTests
{
    [TestMethod]
    public void GetMinimapMarkerBlinkBgra_OffPhaseUsesStrongerSameColorFamily()
    {
        byte[] droneBlue = { 255, 80, 0, 255 };

        var blink = GameHelpers.GetMinimapMarkerBlinkBgra(
            SceneBiomeTypes.HillsWoods,
            droneBlue,
            usePrimaryColor: false);

        Assert.AreEqual(255, blink[3], "Blink phase must stay fully visible.");
        Assert.IsFalse(IsSameColor(droneBlue, blink),
            "Blink phase should be a stronger color, not the same primary color.");
        Assert.AreEqual(255, blink[0],
            "A blue marker should keep blue as the dominant channel.");
        Assert.IsTrue(blink[1] > droneBlue[1],
            "The secondary blue/cyan channel should be boosted for contrast.");
    }

    [TestMethod]
    public void GetMinimapMarkerBlinkBgra_OrangeMarkerKeepsRedDominant()
    {
        byte[] decoyOrange = { 0, 140, 255, 255 };

        var blink = GameHelpers.GetMinimapMarkerBlinkBgra(
            SceneBiomeTypes.HillsWoods,
            decoyOrange,
            usePrimaryColor: false);

        Assert.AreEqual(255, blink[3]);
        Assert.IsFalse(IsSameColor(decoyOrange, blink));
        Assert.AreEqual(255, blink[2],
            "An orange marker should keep red as the dominant channel.");
        Assert.IsTrue(blink[1] > decoyOrange[1],
            "The green component should be boosted to make orange easier to see.");
    }

    [TestMethod]
    public void GetMinimapMarkerBlinkBgra_BlackMarkerBlinksToDarkGrey()
    {
        byte[] blackMarker = { 0, 0, 0, 255 };
        byte[] darkGrey = { 90, 90, 90, 255 };

        var blink = GameHelpers.GetMinimapMarkerBlinkBgra(
            SceneBiomeTypes.HillsWoods,
            blackMarker,
            usePrimaryColor: false);

        Assert.AreEqual(255, blink[3]);
        Assert.IsTrue(IsSameColor(darkGrey, blink),
            "Black markers should blink to dark grey so they remain recognizable as the same marker family.");
    }

    private static bool IsSameColor(byte[] a, byte[] b) =>
        a.Length == b.Length && a.SequenceEqual(b);
}
