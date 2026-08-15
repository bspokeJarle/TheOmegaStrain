using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Game.Helpers
{
    public class Colors
    {
        public static string getShadeOfColorFromNormal(float normal, string color)
            => RenderColorShading.GetShadeOfColorFromNormal(normal, color);
    }
}
