using System;

namespace TheOmegaStrain.Common.Diagnostics;

/// <summary>Opt-in threat warning diagnostics; disabled for normal gameplay.</summary>
public static class ThreatWarningDiagnostics
{
    public const bool Enabled = false;
    public const string Category = "ThreatWarning";

    public static bool ShouldSample(ref DateTime nextSampleUtc)
    {
        if (!Logger.ShouldLog(Enabled) || DateTime.UtcNow < nextSampleUtc) return false;
        nextSampleUtc = DateTime.UtcNow.AddSeconds(1);
        Logger.Flush();
        return true;
    }

    public static void Write(string message)
    {
        if (Logger.ShouldLog(Enabled)) Logger.Log(message, Category);
    }
}
