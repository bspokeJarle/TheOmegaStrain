using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonSetup;
using Domain;
using _3dRotations.Helpers;
using _3dRotations.World.Objects;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Tutorial
{
    public class TutorialScene : IScene
    {
        private const string TutorialSurfaceFile = "Scene1SurfaceRecording.retro";
        private readonly Surface Surface = new();

        public string SceneMusic { get; } = "music_kanpai";
        public SceneTypes SceneType { get; } = SceneTypes.Tutorial;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.HillsWoods;
        public GameModes GameMode { get; } = GameModes.Playback;
        public ISceneDirector Director { get; } = new TutorialSceneDirector();

        public void SetupScene(I3dWorld world)
        {
            GameState.TutorialState.Reset();
            var ship = Ship.CreateShip(Surface);
            Surface.Create2DMap(30000, 15000, GameMode, TutorialSurfaceFile);
            var weapons = new List<I3dObject> { Lazer.CreateLazer(Surface), Bullet.CreateBullet(Surface) };

            ship.Rotation = new Vector3 { };
            ship.WorldPosition = new Vector3 { };
            ship.ObjectName = "Ship";
            ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
            ship.CrashBoxDebugMode = false;
            ship.WeaponSystems = new Weapons(weapons, ship.Movement!, ship);
            world.WorldInhabitants.Add(ship);

            AddGuidanceArrow(world);
            AddTutorialSeeders(world);
            AddTutorialDrone(world);
            AddSurfaceViewport(world);
            world.WorldInhabitants.Add(CreateTutorialVoicePromptObject());
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Game;
            o.Anchor = ScreenOverlayAnchor.Top;
            o.Header = "ASTERION TRAINING PROTOCOL";
            o.Title = "HAL-E ONLINE";
            o.Body =
                "Welcome pilot.\n\n" +
                "This training run will walk you through thrust, weapons, powerups and decoy use.\n\n" +
                "Press ESC on training overlays to continue. Press ESC again outside an overlay to return to the menu.";
            o.Footer = "PRESS ANY KEY OR [ESC] TO CONTINUE - [X] SKIPS TRAINING";
            o.ShowOverlay = true;
            o.AutoHide = false;
            o.DimStrength = 0.45f;
            o.PanelWidthRatio = 0.68f;
            o.PanelHeightRatio = 0.34f;
            o.ShowDebugOverlay = false;
        }

        public void SetupGameOverlay()
        {
            GameState.TutorialState.ClearInstructionOverlay();
            GameState.ScreenOverlayState.HardHide();
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
        }

        public void SetupVideoOverlay(string fileName)
        {
        }

        private void AddGuidanceArrow(I3dWorld world)
        {
            var guidanceArrow = SeederGuidanceArrow.CreateSeederGuidanceArrow(Surface);
            guidanceArrow.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 200 };
            guidanceArrow.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 90 };
            guidanceArrow.WorldPosition = new Vector3 { };
            guidanceArrow.ObjectName = "SeederGuidanceArrow";
            guidanceArrow.ImpactStatus = new ImpactStatus { };
            guidanceArrow.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(guidanceArrow);
        }

        private void AddTutorialSeeders(I3dWorld world)
        {
            var positions = SeederPlacementHelpers.CreateRingSeederPositions(
                count: 3,
                center: GameState.SurfaceState.GlobalMapPosition,
                seed: 9101,
                nearSeederCount: 3,
                firstRingRadius: 5200f,
                ringRadiusStep: 8500f,
                radiusJitter: 650f,
                angleJitterDegrees: 8f);

            for (int i = 0; i < positions.Count; i++)
            {
                var seeder = Seeder.CreateSeeder(Surface);
                seeder.Rotation = new Vector3 { };
                seeder.WorldPosition = positions[i];
                seeder.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 600 };
                seeder.ObjectName = "Seeder";
                seeder.Movement = new TutorialSeederControls();
                seeder.CrashBoxDebugMode = false;
                seeder.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.SeederHealth };
                seeder.HasPowerUp = i == positions.Count - 1;
                seeder.IsActive = true;

                world.WorldInhabitants.Add(seeder);
                GameState.SurfaceState.AiObjects.Add(seeder);
            }
        }

        private void AddTutorialDrone(I3dWorld world)
        {
            var mapPosition = GameState.SurfaceState.GlobalMapPosition;
            var kamikaze = KamikazeDrone.CreateKamikazeDrone(Surface);
            kamikaze.WorldPosition = new Vector3 { x = mapPosition.x + 1600f, y = 0, z = mapPosition.z + 900f };
            kamikaze.Rotation = new Vector3 { };
            kamikaze.ObjectOffsets = new Vector3 { x = 0, y = 150, z = 400 };
            kamikaze.ObjectName = "KamikazeDrone";
            kamikaze.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.KamikazeDroneHealth };
            kamikaze.CrashBoxDebugMode = false;
            kamikaze.WeaponSystems = null;
            kamikaze.HasPowerUp = false;
            kamikaze.IsActive = false;

            world.WorldInhabitants.Add(kamikaze);
            GameState.SurfaceState.AiObjects.Add(kamikaze);
        }

        private void AddSurfaceViewport(I3dWorld world)
        {
            var surfaceObject = (_3dObject)Surface.GetSurfaceViewPort();
            surfaceObject.ObjectName = "Surface";
            surfaceObject.ObjectOffsets = new Vector3 { x = 70 * ScreenSetup.ScreenScaleX, y = 500 * ScreenSetup.ScreenScaleY, z = 400 };
            surfaceObject.Rotation = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 };
            surfaceObject.WorldPosition = new Vector3 { };
            surfaceObject.Movement = new GroundControls();
            surfaceObject.ParentSurface = Surface;
            surfaceObject.ImpactStatus = new ImpactStatus { };
            surfaceObject.CrashBoxDebugMode = false;
            surfaceObject.CrashBoxesFollowRotation = false;

            world.WorldInhabitants.Add(surfaceObject);
            GameState.SurfaceState.SurfaceViewportObject = surfaceObject;
        }

        private static _3dObject CreateTutorialVoicePromptObject()
        {
            return new _3dObject
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "TutorialVoicePrompt",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                Movement = new TutorialVoicePromptControls(),
                ImpactStatus = new ImpactStatus(),
                CrashBoxes = new List<List<IVector3>>(),
                ObjectParts = new List<I3dObjectPart>
                {
                    new _3dObjectPart
                    {
                        PartName = "TutorialVoicePromptMarker",
                        IsVisible = true,
                        Triangles = new List<ITriangleMeshWithColor>
                        {
                            new TriangleMeshWithColor
                            {
                                Color = "000000",
                                vert1 = new Vector3 { x = 0f, y = 0f, z = 0f },
                                vert2 = new Vector3 { x = 1f, y = 0f, z = 0f },
                                vert3 = new Vector3 { x = 0f, y = 1f, z = 0f },
                                noHidden = true
                            }
                        }
                    }
                }
            };
        }
    }
}
