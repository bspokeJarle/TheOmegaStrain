using Domain;

namespace _3dRotations.Helpers
{
    public class Colors
    {
        public static string getShadeOfColorFromNormal(float normal, string color)
            => RenderColorShading.GetShadeOfColorFromNormal(normal, color);
    }
}
