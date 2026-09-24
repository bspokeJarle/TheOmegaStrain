namespace TheOmegaStrain.Common.CommonSetup;

public static class CloudVisualSetup
{
    public const string BodyPartName = "CloudBody";
    public const float Opacity = 1f;

    public static bool IsCloudPart(string? partName) =>
        partName == BodyPartName;

    public static float GetOpacity(string? partName) =>
        Opacity;
}
