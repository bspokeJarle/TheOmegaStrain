namespace TheOmegaStrain.Common.Persistence
{
    public static class PlayerNameFormatter
    {
        public static string Normalize(string? playerName)
        {
            return string.IsNullOrWhiteSpace(playerName)
                ? string.Empty
                : playerName.Trim().ToUpperInvariant();
        }
    }
}
