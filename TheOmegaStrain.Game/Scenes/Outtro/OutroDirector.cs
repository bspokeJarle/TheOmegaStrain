using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;

namespace TheOmegaStrain.Game.Scenes.Outro
{
    public class OutroDirector : ISceneDirector
    {
        private const float SpaceApproachFadeOutSeconds = 1.5f;
        private const float GroundRevealFadeInSeconds = 1.5f;
        private const float GroundRevealSeconds = 2.4f;
        private const float CongratulationsOverlayDelaySeconds = 10f;

        private readonly OutroLandingSceneBuilder _landingSceneBuilder = new();
        private I3dWorld? _world;
        private OutroPhase _phase = OutroPhase.SpaceApproach;
        private float _groundRevealElapsedSeconds;
        private float _pilotRevealElapsedSeconds;
        private float _overlayPageElapsedSeconds;
        private bool _congratulationsOverlayShown;

        public bool IsVictory { get; private set; }
        public bool IsDefeat { get; private set; }
        public OutroPhase Phase => _phase;

        public void Initialize(IGameEventBus eventBus, I3dWorld world)
        {
            _world = world;
            _phase = OutroPhase.SpaceApproach;
            _groundRevealElapsedSeconds = 0f;
            _pilotRevealElapsedSeconds = 0f;
            _overlayPageElapsedSeconds = 0f;
            _congratulationsOverlayShown = false;
            IsVictory = false;
            IsDefeat = false;
        }

        public void Update()
        {
            if (_world == null)
                return;

            if (_phase == OutroPhase.SpaceApproach && TryGetOutroShipControls(out var shipControls) && shipControls.HasReachedEarth)
            {
                GameState.WorldFade.RequestFadeOut(SpaceApproachFadeOutSeconds, "OutroShipReachedEarth");
                _phase = OutroPhase.SpaceFadeOut;
            }

            if (_phase == OutroPhase.SpaceFadeOut && GameState.WorldFade.IsBlack)
            {
                _phase = OutroPhase.GroundRevealPending;
            }

            if (_phase == OutroPhase.GroundRevealPending)
            {
                BuildGroundReveal();
            }

            if (_phase == OutroPhase.GroundReveal)
            {
                UpdateGroundReveal();
            }

            if (_phase == OutroPhase.GroundShipApproachPending)
            {
                BeginGroundShipLanding();
            }

            if (_phase == OutroPhase.GroundShipLanding && TryGetOutroLandingShipControls(out var landingControls) && landingControls.IsLanded)
            {
                _phase = OutroPhase.GroundShipLanded;
            }

            if (_phase == OutroPhase.GroundShipLanded && TryGetOutroLandingShipControls(out landingControls) && landingControls.IsAstronautRevealReady)
            {
                _landingSceneBuilder.AddAstronaut(_world);
                _landingSceneBuilder.AddFireworks(_world);
                _pilotRevealElapsedSeconds = 0f;
                _phase = OutroPhase.GroundPilotReveal;
            }

            if (_phase == OutroPhase.GroundPilotReveal)
            {
                UpdateCongratulationsOverlay();
            }
        }

        public void Dispose()
        {
            _world = null;
            _phase = OutroPhase.SpaceApproach;
            _groundRevealElapsedSeconds = 0f;
            _pilotRevealElapsedSeconds = 0f;
            _overlayPageElapsedSeconds = 0f;
            _congratulationsOverlayShown = false;
            IsVictory = false;
            IsDefeat = false;
        }

        private void UpdateCongratulationsOverlay()
        {
            float delta = GetDeltaSeconds();
            _pilotRevealElapsedSeconds += delta;

            var overlay = GameState.ScreenOverlayState;
            if (overlay == null)
                return;

            if (_congratulationsOverlayShown)
                return;

            if (_pilotRevealElapsedSeconds < CongratulationsOverlayDelaySeconds)
                return;

            ShowCongratulationsOverlay(overlay);
            _congratulationsOverlayShown = true;
        }

        private static void ShowCongratulationsOverlay(ScreenOverlayState overlay)
        {
            overlay.ResetToDefaults();
            overlay.Type = ScreenOverlayType.Outro;
            overlay.Anchor = ScreenOverlayAnchor.Center;
            overlay.IsModal = false;
            overlay.CenterText = true;
            overlay.DimStrength = 0.45f;
            overlay.PanelWidthRatio = 0.72f;
            overlay.PanelHeightRatio = 0.36f;
            overlay.PanelYOffsetRatio = 0.04f;

            overlay.Pages.Clear();
            overlay.AddPage(
                header: "RETROMESH // TRANSMISSION COMPLETE",
                title: "OMEGA STRAIN CONTAINED",
                body:
                    "Pilot, the Seeders are silent.\n" +
                    "Containment probability climbed from 12% to 100%.\n\n" +
                    "The MotherShip is space dust,\n" +
                    "the Kamikaze Drones forgot what they were doing,\n" +
                    "and even the asteroids missed (barely).\n\n" +
                    "Now wave at the locals - they deserve it.",
                footer: "PAGE 1 / 3 - PRESS ANY KEY TO CONTINUE");
            overlay.AddPage(
                header: "RETROMESH // FIELD ADVISORY",
                title: "THE STRAIN MAY RETURN",
                body:
                    "Earth is clean - for now. But the galaxy is big,\n" +
                    "and somewhere out there a new strain is already\n" +
                    "rehearsing its entrance.\n\n" +
                    "Command has spun up a combat simulator so you can\n" +
                    "keep your reflexes sharp and your name on the\n" +
                    "leaderboard. Round after round, forever.\n\n" +
                    "Press any key to enter the simulation.",
                footer: "PAGE 2 / 3 - PRESS ANY KEY TO CONTINUE");
            overlay.AddPage(
                header: "RETROMESH // HALL OF FAME",
                title: "LEADERBOARD",
                body: HighscoreOverlayFormatter.BuildBody(),
                footer: "PAGE 3 / 3 - PRESS ANY KEY TO DEPLOY");

            overlay.CurrentPage = 0;
            overlay.ApplyPageContent();
            overlay.AutoPageSeconds = ScreenOverlayState.DefaultAutoPageSeconds;
            overlay.ShowOverlay = true;
        }

        private void BuildGroundReveal()
        {
            if (_world == null)
                return;

            _landingSceneBuilder.Build(_world);
            _groundRevealElapsedSeconds = 0f;
            GameState.WorldFade.RequestFadeIn(GroundRevealFadeInSeconds, "OutroGroundReveal");
            _phase = OutroPhase.GroundReveal;
        }

        private void UpdateGroundReveal()
        {
            _groundRevealElapsedSeconds += GetDeltaSeconds();
            float progress = Math.Clamp(_groundRevealElapsedSeconds / GroundRevealSeconds, 0f, 1f);
            float eased = SmoothStep(progress);

            UpdateGroundRevealOffsets(eased);

            if (progress >= 1f)
            {
                UpdateGroundRevealOffsets(1f);
                _phase = OutroPhase.GroundShipApproachPending;
            }
        }

        private void BeginGroundShipLanding()
        {
            if (_world == null)
                return;

            _landingSceneBuilder.AddLandingShip(_world);
            _phase = OutroPhase.GroundShipLanding;
        }

        private void UpdateGroundRevealOffsets(float eased)
        {
            var inhabitants = _world?.WorldInhabitants;
            if (inhabitants == null)
                return;

            for (int i = 0; i < inhabitants.Count; i++)
            {
                var obj = inhabitants[i];
                if (TryGetGroundRevealFinalOffset(obj, out var finalOffset))
                    obj.ObjectOffsets = LerpOffset(OutroLandingSceneBuilder.CreateRevealInitialOffset(finalOffset), finalOffset, eased);
            }
        }

        private static bool TryGetGroundRevealFinalOffset(I3dObject obj, out Vector3 finalOffset)
        {
            finalOffset = new Vector3();
            if (obj == null)
                return false;

            switch (obj.ObjectName)
            {
                case "Surface":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalSurfaceOffset();
                    return true;
                case "OutroGroundStars":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalGroundStarsOffset();
                    return true;
                case "OutroLandingPlatform":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalPlatformOffset();
                    return true;
                case "Tree":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalTreeOffset();
                    return true;
                case "House":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalHouseOffset();
                    return true;
                case "OutroLandingBanner":
                    finalOffset = OutroLandingSceneBuilder.CreateFinalBannerOffset();
                    return true;
                default:
                    return false;
            }
        }

        private static float GetDeltaSeconds()
        {
            if (GameState.DeltaTime > 0f)
                return Math.Min(GameState.DeltaTime, 0.1f);

            return GameState.GameplayBaselineDeltaTime;
        }

        private static float SmoothStep(float value)
        {
            value = Math.Clamp(value, 0f, 1f);
            return value * value * (3f - (2f * value));
        }

        private static float Lerp(float from, float to, float amount)
        {
            return from + ((to - from) * amount);
        }

        private static Vector3 LerpOffset(Vector3 from, Vector3 to, float amount)
        {
            return new Vector3
            {
                x = Lerp(from.x, to.x, amount),
                y = Lerp(from.y, to.y, amount),
                z = Lerp(from.z, to.z, amount)
            };
        }

        private bool TryGetOutroShipControls(out OutroShipControls controls)
        {
            controls = null!;

            var inhabitants = _world?.WorldInhabitants;
            if (inhabitants == null)
                return false;

            for (int i = 0; i < inhabitants.Count; i++)
            {
                if (!string.Equals(inhabitants[i].ObjectName, "Ship", StringComparison.Ordinal))
                    continue;

                if (inhabitants[i].Movement is OutroShipControls outroControls)
                {
                    controls = outroControls;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetOutroLandingShipControls(out OutroLandingShipControls controls)
        {
            controls = null!;

            var inhabitants = _world?.WorldInhabitants;
            if (inhabitants == null)
                return false;

            for (int i = 0; i < inhabitants.Count; i++)
            {
                if (!string.Equals(inhabitants[i].ObjectName, "Ship", StringComparison.Ordinal))
                    continue;

                if (inhabitants[i].Movement is OutroLandingShipControls landingControls)
                {
                    controls = landingControls;
                    return true;
                }
            }

            return false;
        }
    }

    public enum OutroPhase
    {
        SpaceApproach,
        SpaceFadeOut,
        GroundRevealPending,
        GroundReveal,
        GroundShipApproachPending,
        GroundShipLanding,
        GroundShipLanded,
        GroundPilotReveal
    }
}
