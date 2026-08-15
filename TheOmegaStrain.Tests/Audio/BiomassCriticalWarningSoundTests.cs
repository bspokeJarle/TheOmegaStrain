using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace _3DSpesificsUnitTests.Audio;

[TestClass]
public class BiomassCriticalWarningSoundTests
{
    private static readonly (string Id, string File, string Usage)[] WarningSounds =
    [
        ("biomass_critical_warning", "OmegaStrain_biomass_critical.mp3", "BiomassCriticalWarning"),
        ("biomass_abort_warning", "OmegaStrain_biomass_reached_abort.mp3", "BiomassAbortWarning")
    ];

    [TestMethod]
    public void BiomassWarningSounds_AreRegisteredAndCopiedToOutput()
    {
        var repoRoot = FindRepoRoot();
        Assert.IsNotNull(repoRoot, "Could not locate repository root from test working dir.");

        var soundsJson = Path.Combine(repoRoot!, "TheOmegaStrain.Wpf", "Soundeffects", "sounds.json");
        var audioBasePath = Path.GetDirectoryName(soundsJson)!;
        var registry = new JsonSoundRegistry(soundsJson);
        var frontendProject = XDocument.Load(Path.Combine(repoRoot!, "TheOmegaStrain.Wpf", "TheOmegaStrain.Wpf.csproj"));

        foreach (var warningSound in WarningSounds)
        {
            Assert.IsTrue(
                registry.TryGet(warningSound.Id, out var definition),
                $"{warningSound.Id} is missing from sounds.json.");

            Assert.AreEqual(warningSound.File, definition.File);
            Assert.AreEqual(warningSound.Usage, definition.Usage);
            Assert.IsTrue(
                File.Exists(Path.Combine(audioBasePath, warningSound.File)),
                $"{warningSound.File} is missing.");

            var copied = frontendProject
                .Descendants("None")
                .Any(e => string.Equals((string?)e.Attribute("Update"), $@"Soundeffects\{warningSound.File}", StringComparison.OrdinalIgnoreCase) &&
                          e.Elements("CopyToOutputDirectory").Any(c => string.Equals(c.Value, "Always", StringComparison.OrdinalIgnoreCase)));

            Assert.IsTrue(copied, $"{warningSound.File} must be copied to output.");
        }
    }

    private static string? FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "TheOmegaStrain.sln")))
                return dir.FullName;

            dir = dir.Parent;
        }

        return null;
    }
}
