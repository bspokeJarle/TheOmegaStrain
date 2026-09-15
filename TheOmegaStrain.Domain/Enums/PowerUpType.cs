namespace TheOmegaStrain.Domain
{
    public enum PowerUpType
    {
        Standard = 0,
        TravelSpeedLevel1 = 1,
        TravelSpeedLevel2 = 2,
        // Med-kit pickup: restores ship health instead of granting progression.
        Health = 3
    }
}
