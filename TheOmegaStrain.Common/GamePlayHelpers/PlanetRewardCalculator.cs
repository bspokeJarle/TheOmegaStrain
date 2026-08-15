using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using TheOmegaStrain.Common.CommonGlobalState.States;

namespace TheOmegaStrain.Common.GamePlayHelpers
{
    public sealed record PlanetRewardLine(string Label, int Points);

    public sealed class PlanetRewardBreakdown
    {
        public PlanetRewardBreakdown(int sceneIndex, IReadOnlyList<PlanetRewardLine> lines)
        {
            SceneIndex = sceneIndex;
            Lines = lines;
        }

        public int SceneIndex { get; }
        public IReadOnlyList<PlanetRewardLine> Lines { get; }
        public int TotalPoints => Lines.Sum(line => line.Points);

        public int GetDisplayedTotal(float progress)
        {
            progress = Math.Clamp(progress, 0f, 1f);
            return Lines.Sum(line => GetDisplayedPoints(line, progress));
        }

        public string BuildOverlayBody(float progress)
        {
            progress = Math.Clamp(progress, 0f, 1f);

            var body = new StringBuilder();
            foreach (var line in Lines)
            {
                body.Append(line.Label.PadRight(24));
                body.Append("+");
                body.Append(GetDisplayedPoints(line, progress).ToString().PadLeft(5));
                body.AppendLine();
            }

            if (Lines.Count > 0)
                body.AppendLine();

            body.Append("TOTAL BONUS".PadRight(24));
            body.Append("+");
            body.Append(GetDisplayedTotal(progress).ToString().PadLeft(5));
            return body.ToString();
        }

        private static int GetDisplayedPoints(PlanetRewardLine line, float progress)
        {
            return (int)MathF.Round(line.Points * EaseOut(progress));
        }

        private static float EaseOut(float progress)
        {
            return 1f - (1f - progress) * (1f - progress);
        }
    }

    public static class PlanetRewardCalculator
    {
        public static PlanetRewardBreakdown Calculate(GamePlayState gameplay)
        {
            var lines = new List<PlanetRewardLine>();

            Add(lines, "BIOMASS CONTAINED", CalculateBiomassContainedBonus(gameplay));
            Add(lines, "HULL INTEGRITY", CalculateHullIntegrityBonus(gameplay));
            Add(lines, "LIVES PRESERVED", Math.Max(0, gameplay.Lives) * GameSetup.PlanetLifePreservedBonus);
            Add(lines, "PRECISION BONUS", CalculatePrecisionBonus(gameplay.Accuracy));
            Add(lines, "STYLE FLYING", Math.Max(0, gameplay.PlanetStyleBonusScore));
            Add(lines, "CLEAN OPERATION", CalculateDeathlessBonus(gameplay));
            Add(lines, "MOTHERSHIP TAKEDOWN", CalculateMothershipTakedownBonus(gameplay));

            return new PlanetRewardBreakdown(gameplay.SceneIndex, lines);
        }

        private static void Add(List<PlanetRewardLine> lines, string label, int points)
        {
            if (points > 0)
                lines.Add(new PlanetRewardLine(label, points));
        }

        private static int CalculateBiomassContainedBonus(GamePlayState gameplay)
        {
            float cleanPercent = Math.Clamp(100f - gameplay.InfectionPercent, 0f, 100f);
            return (int)MathF.Round(cleanPercent) * GameSetup.PlanetBiomassContainedPointsPerCleanPercent;
        }

        private static int CalculateHullIntegrityBonus(GamePlayState gameplay)
        {
            if (gameplay.MaxHealth <= 0f)
                return 0;

            float healthPercent = Math.Clamp(gameplay.Health / gameplay.MaxHealth, 0f, 1f);
            return (int)MathF.Round(healthPercent * GameSetup.PlanetHullIntegrityMaxBonus);
        }

        private static int CalculatePrecisionBonus(float accuracy)
        {
            if (accuracy >= 0.70f)
                return GameSetup.PlanetPrecisionBonusTier3;
            if (accuracy >= 0.50f)
                return GameSetup.PlanetPrecisionBonusTier2;
            if (accuracy >= 0.30f)
                return GameSetup.PlanetPrecisionBonusTier1;

            return 0;
        }

        private static int CalculateDeathlessBonus(GamePlayState gameplay)
        {
            if (!gameplay.HasPlanetStartSnapshot)
                return 0;

            return gameplay.TotalDeaths == gameplay.PlanetStartTotalDeaths
                ? GameSetup.PlanetDeathlessBonus
                : 0;
        }

        private static int CalculateMothershipTakedownBonus(GamePlayState gameplay)
        {
            if (gameplay.InitialMotherShips <= 0 || gameplay.MotherShipsRemaining > 0)
                return 0;

            return Math.Max(1, gameplay.SceneIndex) * GameSetup.PlanetMothershipTakedownBonusPerScene;
        }
    }
}
