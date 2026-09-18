using TheOmegaStrain.Game.World.Objects.LogoCube;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.Persistence;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace TheOmegaStrain.Game.Scenes.Intro
{
    public class Intro : IScene
    {
        private static string BuildMainMenuFooter()
        {
            var activeScheme = GameState.SettingsState.EffectiveControlScheme;
            if (activeScheme == ControlInputMode.XboxController)
                return "D-PAD UP/DOWN SELECT | [A] CONFIRM\n" +
                       "D-PAD LEFT/RIGHT VIEW INFO\n\n" +
                       "HOLD [VIEW] FOR 2 SECONDS TO QUIT";

            return "UP/DOWN SELECT | ENTER CONFIRM\n" +
                   "LEFT/RIGHT VIEW INFO";
        }

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
                string expectedControlMode = GetControlModeLabel();
                string expectedPrefix = $"ACTIVE CONTROL: {expectedControlMode}";
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

        private static void ConfigureMainMenu(ScreenOverlayState overlay)
        {
            string controlMode = GetControlModeLabel();

            overlay.SetChoiceOptions(
                ScreenOverlayChoiceAction.IntroMainMenu,
                $"ACTIVE CONTROL: {controlMode}",
                "START GAME",
                "TRAINING",
                "SETTINGS",
                "QUIT");
            overlay.Footer = BuildMainMenuFooter();
        }

        private static string GetControlModeLabel() =>
            GameState.SettingsState.EffectiveControlScheme switch
            {
                ControlInputMode.XboxController => "XBOX CONTROLLER",
                ControlInputMode.Mouse => "MOUSE + KEYBOARD",
                _ => "KEYBOARD"
            };

        private const string InfoFooter = "LEFT/RIGHT CHANGE PAGE | ESC/B BACK TO MENU";

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

            // Page 1: Main menu
            o.AddPage(
                "RETROMESH COMMAND CONSOLE",
                MainMenuPageTitle,
                "",
                BuildMainMenuFooter());

            // Page 2: Story
            o.AddPage(
                "RETROMESH SYSTEM INITIALIZING",
                "THE OMEGA STRAIN // BRIEFING",
                "Year 2147.\n\n" +
                "A foreign organism has spread across the outer colonies.\n" +
                "Designated: OMEGA STRAIN.\n\n" +
                "Autonomous Seeder units detected.\n" +
                "Containment probability: 12%.",
                InfoFooter);

            // Pages 3-5: Gameplay tips. Keep each page short enough to remain
            // readable on the centered menu panel at every supported resolution.
            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "GAMEPLAY TIPS & TRICKS",
                "NAVIGATION & OBJECTIVES:\n" +
                "  - The green arrow below the HUD points to the closest Seeder\n" +
                "  - Keep the arrow ahead of you to reach the next target quickly\n" +
                "  - Destroy Seeders fast to stop their infection cascades\n" +
                "  - Watch the infection meter - reaching the limit loses the planet\n" +
                "  - Clear the enemy wave to bring in the MotherShip",
                InfoFooter);

            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "WEAPONS & POWERUPS",
                "COMBAT SYSTEMS:\n" +
                "  - Keyboard 1: Bullet | 2: Decoy | 3: Laser\n" +
                "  - Xbox defaults: [X] Bullet | [Y] Decoy | [B] Laser\n" +
                "  - Select a system, then use your configured FIRE control\n" +
                "  - DECOY is the best way to fight Kamikaze Drones\n" +
                "  - Seeder kills can drop PowerUps - fly into them to collect\n" +
                "  - Some PowerUps permanently improve travel speed",
                InfoFooter);

            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "SHIELDS & DRONE DEFENCE",
                "DEFENSIVE SYSTEMS:\n" +
                "  - SpaceSwans can drop Shield pickups when destroyed\n" +
                "  - Fly into a Shield pickup to activate it immediately\n" +
                "  - Shield lasts 45 seconds and reduces normal damage to 20%\n" +
                "  - The Shield icon shrinks in the HUD as protection runs out\n" +
                "  - Your ship glows while Shield protection is active\n" +
                "  - DECOY remains the best way to defeat Kamikaze Drones",
                InfoFooter);

            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "FLIGHT & SURVIVAL",
                "PILOT NOTES:\n" +
                "  - Thrust, steering, fire and Xbox buttons are editable in Settings\n" +
                "  - Flight Settings tune coasting, thrust and gravity response\n" +
                "  - Releasing thrust preserves momentum; plan turns before the target\n" +
                "  - Hard surface impacts damage the ship - control your descent\n" +
                "  - Use Training from the main menu to practise safely\n" +
                "  - ESC opens the menu; on its first page hold [VIEW] 2 seconds to quit",
                InfoFooter);

            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "READING THE HUD",
                "SHIPBOARD DISPLAY:\n" +
                "  - Minimap: your ship stays centered; blinking marks show threats and pickups\n" +
                "  - POWER is hull health | ALT is altitude | THR is current thrust\n" +
                "  - BIO shows infection progress toward the planet's critical limit\n" +
                "  - Drone and Seeder bars show how much of each enemy wave remains\n" +
                "  - Bright weapon icon = selected; very faint icon = not yet unlocked\n" +
                "  - Speed icon shows your permanent travel upgrade level\n" +
                "  - MotherShip health appears during its battle; FPS/TRI are performance data",
                InfoFooter);

            // Final page: Highscores
            o.AddPage(
                "RETROMESH // HALL OF FAME",
                "TOP PILOTS",
                HighscoreOverlayFormatter.BuildBody(),
                InfoFooter);

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

        public void SetupVideoOverlay(string fileName)
        {
            GameState.ScreenOverlayState.ShowVideoOverlay = true;
            GameState.ScreenOverlayState.VideoClipPath = Path.Combine("gamegraphics", "introclip.mp4");
        }

    }
}
