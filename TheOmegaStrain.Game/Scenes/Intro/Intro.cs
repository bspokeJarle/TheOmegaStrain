using _3dRotations.World.Objects.LogoCube;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.Persistence;
using Domain;
using GameAiAndControls.Controls;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Intro
{
    public class Intro : IScene
    {
        private const string StartFooter = "PRESS ANY KEY TO INITIATE PROTOCOL  //  [T] TRAINING  //  [S] SOUND  //  [G] GRAPHICS  //  [C] CONTROLS";

        public bool SkipLogoCube { get; set; } = false;

        public GameModes GameMode { get; } = GameModes.Live;

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
            o.Anchor = ScreenOverlayAnchor.Top;

            // Page 1: Story
            o.AddPage(
                "RETROMESH SYSTEM INITIALIZING",
                "THE OMEGA STRAIN",
                "Year 2147.\n\n" +
                "A foreign organism has spread across the outer colonies.\n" +
                "Designated: OMEGA STRAIN.\n\n" +
                "Autonomous Seeder units detected.\n" +
                "Containment probability: 12%.",
                StartFooter);

            // Page 2: Controls
            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "FLIGHT CONTROLS",
                "KEYBOARD:\n" +
                "  [SPACE]       THRUST\n" +
                "  [RIGHT SHIFT] FIRE CURRENT WEAPON\n" +
                "  [1] BULLET  - High fire rate, effective vs Seeders\n" +
                "  [2] DECOY   - Lures Kamikaze Drones away from your ship\n" +
                "  [3] LAZER   - Powerful beam, cuts through targets\n\n" +
                "MOUSE:\n" +
                "  LEFT BUTTON   FIRE CURRENT WEAPON\n" +
                "  RIGHT BUTTON  THRUST\n" +
                "  MOVE MOUSE    PITCH / TURN\n\n" +
                "XBOX CONTROLLER:\n" +
                "  LEFT STICK    PITCH / TURN\n" +
                "  [RT]          THRUST\n" +
                "  [LT]          FIRE CURRENT WEAPON\n" +
                "  [X]/[Y]/[B]   1 BULLET / 2 DECOY / 3 LAZER\n" +
                "  [A]           POWERUP 4 RESERVED\n\n" +
                "NAVIGATION:\n" +
                "  KEYBOARD [X] MENU  //  [T] TUTORIAL  //  [C] CONTROLS\n\n" +
                "NOTE:\n" +
                "  Input type and mappings can be changed in CONTROLS.",
                StartFooter);

            // Page 3: Gameplay tips
            o.AddPage(
                "RETROMESH // FIELD MANUAL",
                "TACTICAL TIPS",
                "COMBAT TIPS:\n" +
                "  - Destroy Seeders fast to control infection spread\n" +
                "  - Every Seeder kill helps slow the infection cascade\n" +
                "  - Kamikaze Drones will rush your ship - deploy Decoys!\n" +
                "  - Decoys unlock after collecting your first PowerUp\n" +
                "  - PowerUps drop from glowing Seeders\n" +
                "  - Eliminate all enemies to face the MotherShip",
                StartFooter);

            // Page 4: Highscores
            o.AddPage(
                "RETROMESH // HALL OF FAME",
                "TOP PILOTS",
                HighscoreOverlayFormatter.BuildBody(),
                StartFooter);

            o.CurrentPage = 0;
            o.ApplyPageContent();

            // LogoCube plays first
            o.ShowOverlay = false;

            // This is intro - don't auto-hide until player input
            o.AutoHide = false;
            o.AutoHideSeconds = 0f;

            // Optional: stronger cinematic feel
            o.DimStrength = 0.55f;
            o.PanelWidthRatio = 0.72f;
            o.PanelHeightRatio = 0.32f;
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
