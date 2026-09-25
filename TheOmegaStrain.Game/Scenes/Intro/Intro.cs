using TheOmegaStrain.Game.World.Objects.LogoCube;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Common.Localization;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TheOmegaStrain.Game.Scenes.Intro
{
    public class Intro : IScene
    {
        public const int LanguageChoiceIndex = 4;
        public const int InfoChoiceIndex = 5;

        private static string BuildMainMenuFooter()
        {
            var activeScheme = GameState.SettingsState.EffectiveControlScheme;
            return Text(activeScheme == ControlInputMode.XboxController
                ? "menu.footerController" : "menu.footerKeyboard");
        }

        private static string Text(string key) => GameText.Get(key, GameState.SettingsState.LanguageCode);

        private static string BuildMainMenuPrefix() =>
            GameText.Format("menu.activeControlLine", GameState.SettingsState.LanguageCode,
                ("control", GetControlModeLabel())) + "\n" +
            Text("menu.languageHint");

        private const string MainMenuPageTitle = "THE OMEGA STRAIN";

        /// <summary>
        /// Controllers can be plugged in after the overlay was built, so the story
        /// footer is refreshed whenever the detected controller state changes.
        /// </summary>
        public static void RefreshControlFooter()
        {
            var overlay = GameState.ScreenOverlayState;
            if (overlay.Type != ScreenOverlayType.Intro)
                return;

            if (overlay.CurrentPage == 0 &&
                overlay.ChoiceAction == ScreenOverlayChoiceAction.IntroMainMenu)
            {
                string expectedPrefix = BuildMainMenuPrefix();
                string expectedFooter = BuildMainMenuFooter();
                if (overlay.ChoiceBodyPrefix == expectedPrefix &&
                    overlay.Footer == expectedFooter)
                    return;

                int selectedIndex = overlay.SelectedChoiceIndex;
                ConfigureMainMenu(overlay);
                overlay.MoveChoiceSelection(selectedIndex);
            }
        }

        public static void ConfigurePageMode(ScreenOverlayState overlay)
        {
            if (overlay.CurrentPage == 0)
                ConfigureMainMenu(overlay);
            else
                overlay.ClearChoiceOptions();
        }

        public static void RefreshLocalizedPages(ScreenOverlayState overlay)
        {
            int currentPage = overlay.CurrentPage;
            overlay.Pages.Clear();
            PopulatePages(overlay);
            overlay.CurrentPage = currentPage;
            overlay.ApplyPageContent();
            overlay.AutoPageSeconds = 0f;
            ConfigurePageMode(overlay);
        }

        private static void ConfigureMainMenu(ScreenOverlayState overlay)
        {
            overlay.SetChoiceOptions(
                ScreenOverlayChoiceAction.IntroMainMenu,
                BuildMainMenuPrefix(),
                Text("menu.start"),
                Text("menu.training"),
                Text("menu.settings"),
                Text("menu.quit"),
                GameText.Format("menu.languageChoice", GameState.SettingsState.LanguageCode,
                    ("language", GameState.SettingsState.LanguageCode.ToUpperInvariant())),
                Text("menu.info"));
            overlay.Footer = BuildMainMenuFooter();
        }

        private static string GetControlModeLabel() =>
            GameState.SettingsState.EffectiveControlScheme switch
            {
                ControlInputMode.XboxController => Text("menu.controller"),
                ControlInputMode.Mouse => Text("menu.mouse"),
                _ => Text("menu.keyboard")
            };

        private static string InfoFooter => Text("intro.infoFooter");

        public bool SkipLogoCube { get; set; } = false;

        public GameModes GameMode { get; } = GameModes.Playback;

        public string SceneMusic { get; } = "music_intro";

        public SceneTypes SceneType { get; } = SceneTypes.Intro;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.HillsWoods;

        public void SetupGameOverlay()
        {
            //No need for that in the intro
        }

        public void SetupScene(I3dWorld world)
        {
            if (SkipLogoCube)
            {
                // Returning mid-game - show the overlay immediately without the logo animation
                GameState.ScreenOverlayState.ShowOverlay = true;
                return;
            }

            var TheOmegaStrainLogo = LogoCube.CreateLogoCube();
            TheOmegaStrainLogo.ObjectOffsets = new Vector3 { x = 1000, y = 0, z = 0 };
            TheOmegaStrainLogo.Rotation = new Vector3 { x = 0, y = 0, z = 0 };
            //This object is centered on the world origin, so no offsets are needed, and it starts with no rotation.
            TheOmegaStrainLogo.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            //In here the logo will be moved according to the intro design, but it starts at the world origin.
            TheOmegaStrainLogo.Movement = new OmegaStrainLogoControls();
            world.WorldInhabitants.Add(TheOmegaStrainLogo);
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Center;

            PopulatePages(o);
            o.CurrentPage = 0;
            o.ApplyPageContent();
            o.AutoPageSeconds = 0f;
            ConfigureMainMenu(o);

            // LogoCube plays first
            o.ShowOverlay = false;

            // This is intro - don't auto-hide until player input
            o.AutoHide = false;
            o.AutoHideSeconds = 0f;

            // Optional: stronger cinematic feel
            o.DimStrength = 0.55f;
            o.PanelWidthRatio = 0.72f;
            o.PanelHeightRatio = 0.32f;
            o.PanelYOffsetRatio = 0.00f;
            //Hide Debug overlay
            o.ShowDebugOverlay = false;
        }

        private static void PopulatePages(ScreenOverlayState o)
        {

            // Page 1: Main menu
            o.AddPage(
                "RETROMESH COMMAND CONSOLE",
                MainMenuPageTitle,
                "",
                BuildMainMenuFooter());

            // Page 2: Story
            o.AddPage(
                Text("intro.story.header"),
                Text("intro.story.title"),
                Text("intro.story.body"),
                InfoFooter);

            // Pages 3-7: Gameplay tips. Keep each page short enough to remain
            // readable on the centered menu panel at every supported resolution.
            o.AddPage(
                Text("intro.manual.header"),
                Text("intro.tips.title"),
                Text("intro.tips.body"),
                InfoFooter);

            o.AddPage(
                Text("intro.manual.header"),
                Text("intro.weapons.title"),
                Text("intro.weapons.body"),
                InfoFooter);

            o.AddPage(
                Text("intro.manual.header"),
                Text("intro.shields.title"),
                Text("intro.shields.body"),
                InfoFooter);

            o.AddPage(
                Text("intro.manual.header"),
                Text("intro.flight.title"),
                Text("intro.flight.body"),
                InfoFooter);

            o.AddPage(
                Text("intro.manual.header"),
                Text("intro.hud.title"),
                Text("intro.hud.body"),
                InfoFooter);

            // Final page: Highscores
            o.AddPage(
                Text("highscore.header"),
                Text("highscore.introTitle"),
                HighscoreOverlayFormatter.BuildBody(),
                InfoFooter);
        }

        public void SetupVideoOverlay(string fileName)
        {
            GameState.ScreenOverlayState.ShowVideoOverlay = true;
            GameState.ScreenOverlayState.VideoClipPath = Path.Combine("gamegraphics", "introclip.mp4");
        }

    }
}
