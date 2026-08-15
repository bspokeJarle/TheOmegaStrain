using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;
using TheOmegaStrain.Gameplay.Controls.MotherShipMediumControls;
using TheOmegaStrain.Gameplay.Controls.SpaceSwanControls;
using TheOmegaStrain.Gameplay.Controls.ZeppelinBomberControls;
using TheOmegaStrain.Gameplay.Controls.JumpingFishControls;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Game.Scenes.Scene8
{
    public class Scene8 : IScene
    {
        Surface Surface = new();

        public string SceneMusic { get; } = "music_dontstop";
        public SceneTypes SceneType { get; } = SceneTypes.Game;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.Rainforrest;
        public ISceneDirector Director { get; } = new Scene8Director();
        public GameModes GameMode { get; } = GameModes.Playback;

        public float InfectionThresholdPercent { get; } = 13.0f;
        public int InfectionSpreadRate { get; } = 9;
        public int SeederOffscreenSpeedFactor { get; } = 22;
        public float LocalInfectionSpreadDelaySec { get; } = 1.2f;
        public float LocalInfectionSpreadRadius { get; } = 6500f;
        public float MotherShipLargeAggression { get; } = 1.40f;

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            var ship = Ship.CreateShip(Surface);
            Surface.Create2DMap(30000, 15000, GameMode, "Scene8SurfaceRecording_20260526_223403.retro");
            var weapons = new List<I3dObject> { Lazer.CreateLazer(Surface), Bullet.CreateBullet(Surface) };
            ship.Rotation = new Vector3 { };
            ship.WorldPosition = new Vector3 { };
            ship.ObjectName = "Ship";
            ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
            ship.CrashBoxDebugMode = false;
            ship.WeaponSystems = new Weapons(weapons, ship.Movement!, ship);
            world.WorldInhabitants.Add(ship);

            var guidanceArrow = SeederGuidanceArrow.CreateSeederGuidanceArrow(Surface);
            guidanceArrow.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 200 };
            guidanceArrow.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 90 };
            guidanceArrow.WorldPosition = new Vector3 { };
            guidanceArrow.ObjectName = "SeederGuidanceArrow";
            guidanceArrow.ImpactStatus = new ImpactStatus { };
            guidanceArrow.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(guidanceArrow);

            SpawnJumpingFish(world);

            for (int b = 0; b < 8; b++)
            {
                var rmdBomber = new Random();
                var bomber = ZeppelinBomber.CreateZeppelinBomber(Surface);
                bomber.Rotation = new Vector3 { };
                bomber.WorldPosition = new Vector3 { x = (95700 + rmdBomber.Next(-42000, 42000)) * ws, y = 0, z = (92000 + rmdBomber.Next(-42000, 42000)) * ws };
                bomber.ObjectOffsets = new Vector3 { x = 0, y = -50, z = 400 };
                bomber.ObjectName = "ZeppelinBomber";
                bomber.Movement = new ZeppelinBomberControls();
                bomber.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.ZeppelinBomberHealth };
                bomber.CrashBoxDebugMode = false;
                bomber.IsActive = true;
                world.WorldInhabitants.Add(bomber);
                GameState.SurfaceState.AiObjects.Add(bomber);
            }

            for (int i = 0; i < 18; i++)
            {
                var rmd = new Random();
                var kamikaze = KamikazeDrone.CreateKamikazeDrone(Surface);
                kamikaze.WorldPosition = new Vector3 { x = (95700 + rmd.Next(-58000, 58000)) * ws, y = 0, z = (92000 + rmd.Next(-58000, 58000)) * ws };
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

            SeederPlacementHelpers.AddSeederGroup(
                world,
                Surface,
                GameState.SurfaceState.GlobalMapPosition,
                totalSeederCount: 25,
                regularSeed: 8081,
                nearSeederCount: 11);

            var motherShipLarge = MotherShipLarge.CreateMotherShipLarge(Surface);
            motherShipLarge.Rotation = new Vector3 { };
            motherShipLarge.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 88000 * ws };
            motherShipLarge.ObjectOffsets = new Vector3 { x = 0, y = -1500, z = 400 };
            motherShipLarge.ObjectName = "MotherShipLarge";
            motherShipLarge.Movement = new MotherShipLargeControls();

            var motherShipLargeLazer = Lazer.CreateLazer(Surface, scaleMultiplier: 2.5f);
            motherShipLargeLazer.CrashBoxDebugMode = false;
            var motherShipLargeWeapons = new List<I3dObject> { motherShipLargeLazer };
            motherShipLarge.WeaponSystems = new Weapons(motherShipLargeWeapons, motherShipLarge.Movement!, (OmegaObject3D)motherShipLarge)
            {
                ShowAimAssist = false,
                FireAsEnemyWeapon = true,
                EnemyLazerName = "EnemyLazerLarge"
            };

            motherShipLarge.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShipLarge.ObjectName, MotherShipLargeAggression) };
            motherShipLarge.CrashBoxDebugMode = false;
            motherShipLarge.HasPowerUp = false;
            motherShipLarge.IsActive = false;
            world.WorldInhabitants.Add(motherShipLarge);
            GameState.SurfaceState.AiObjects.Add(motherShipLarge);

            for (int s = 0; s < 50; s++)
            {
                var rmdSwan = new Random();
                var spaceSwan = SpaceSwan.CreateSpaceSwan(Surface);
                spaceSwan.Rotation = new Vector3 { };
                spaceSwan.WorldPosition = new Vector3 { x = (95700 + rmdSwan.Next(-40000, 40000)) * ws, y = 0, z = (92000 + rmdSwan.Next(-40000, 40000)) * ws };
                spaceSwan.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 600 };
                spaceSwan.ObjectName = "SpaceSwan";
                spaceSwan.Movement = new SpaceSwanControls();
                spaceSwan.CrashBoxDebugMode = false;
                spaceSwan.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.SpaceSwanHealth };
                spaceSwan.HasPowerUp = false;
                spaceSwan.IsActive = true;
                world.WorldInhabitants.Add(spaceSwan);
                GameState.SurfaceState.AiObjects.Add(spaceSwan);
            }

            var surfaceObject = (OmegaObject3D)Surface.GetSurfaceViewPort();
            surfaceObject.ObjectName = "Surface";
            surfaceObject.ObjectOffsets = new Vector3 { x = 105 * ScreenSetup.ScreenScaleX, y = 500 * ScreenSetup.ScreenScaleY, z = 400 };
            surfaceObject.Rotation = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 };
            surfaceObject.WorldPosition = new Vector3 { };
            surfaceObject.Movement = new GroundControls();
            surfaceObject.ParentSurface = Surface;
            surfaceObject.ImpactStatus = new ImpactStatus { };
            surfaceObject.CrashBoxDebugMode = false;
            surfaceObject.CrashBoxesFollowRotation = false;
            world.WorldInhabitants.Add(surfaceObject);
            GameState.SurfaceState.SurfaceViewportObject = surfaceObject;

            if (SceneBiome == SceneBiomeTypes.Rainforrest)
            {
                world.WorldInhabitants.Add(RainEmitter.CreateRainEmitter(Surface));
                world.WorldInhabitants.Add(LightningEmitter.CreateLightningEmitter(Surface));
            }

            var towerPlacements = SurfaceGeneration.FindTowerPlacements(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight());
            SurfaceGeneration.FlattenTerrainAroundTowers_ToHighlands(
                GameState.SurfaceState.Global2DMap,
                Surface.MaxHeight(),
                towerPlacements,
                writeDebugLogs: false
            );

            foreach (var towerPlacement in towerPlacements)
            {
                var tower = Tower.CreateTower(Surface);
                tower.Rotation = new Vector3 { };
                tower.WorldPosition = new Vector3 { };
                tower.SurfaceBasedId = GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].hasLandbasedObject = true;
                tower.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.NudgedSurfaceFootprintOffsetYScaled, z = 400 };
                tower.ObjectName = "Tower";
                tower.Movement = new TowerControls();
                tower.CrashBoxDebugMode = false;
                tower.ImpactStatus = new ImpactStatus { };
                world.WorldInhabitants.Add(tower);
            }

            var palmPlacements = SurfaceGeneration.FindTreePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), palmPlacements, radius: 1);
            var palmIndex = 0;
            foreach (var palmPlacement in palmPlacements)
            {
                palmIndex++;

                bool useLargePalm = palmIndex % 4 == 0;
                bool useSmallPalm = palmIndex % 4 == 1;
                bool useLargeAlienPlant = palmIndex % 4 == 2;

                var plant = useLargePalm
                    ? PalmTree.CreateLargePalm(Surface)
                    : useSmallPalm
                        ? PalmTree.CreateSmallPalm(Surface)
                        : useLargeAlienPlant
                            ? AlienPlant.CreateLargeAlienPlant(Surface)
                            : AlienPlant.CreateSmallAlienPlant(Surface);

                plant.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                plant.SurfaceBasedId = GameState.SurfaceState.Global2DMap[palmPlacement.y, palmPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[palmPlacement.y, palmPlacement.x].hasLandbasedObject = true;
                plant.ObjectOffsets = new Vector3
                {
                    x = 75 * ScreenSetup.ScreenScaleX,
                    y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled,
                    z = 400
                };

                if (useLargePalm)
                {
                    plant.ObjectName = "LargePalm";
                    plant.Movement = new LargePalmControls();
                }
                else if (useSmallPalm)
                {
                    plant.ObjectName = "SmallPalm";
                    plant.Movement = new SmallPalmControls();
                }
                else if (useLargeAlienPlant)
                {
                    plant.ObjectName = "LargeAlienPlant";
                    plant.Movement = new LargePalmControls();
                }
                else
                {
                    plant.ObjectName = "SmallAlienPlant";
                    plant.Movement = new SmallPalmControls();
                }

                plant.ImpactStatus = new ImpactStatus { };
                plant.CrashBoxDebugMode = false;
                if (plant.SurfaceBasedId > 0) world.WorldInhabitants.Add(plant);
            }

            var housePlacements = SurfaceGeneration.FindHousePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.MaxHeight(), palmPlacements, 15000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), housePlacements, radius: 1);
            var bambooHutIndex = 0;
            foreach (var housePlacement in housePlacements)
            {
                if (GameState.SurfaceState.Global2DMap[housePlacement.y, housePlacement.x].hasLandbasedObject)
                    continue;

                bambooHutIndex++;
                var house = BambooHut.CreateBambooHut(Surface);
                house.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                house.SurfaceBasedId = GameState.SurfaceState.Global2DMap[housePlacement.y, housePlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[housePlacement.y, housePlacement.x].hasLandbasedObject = true;
                house.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                house.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                house.ObjectName = "BambooHut";
                house.Movement = new BambooHutControls();
                house.ImpactStatus = new ImpactStatus { };
                house.CrashBoxDebugMode = false;
                if (house.SurfaceBasedId > 0) world.WorldInhabitants.Add(house);
            }
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Top;
            o.Header = "RETROMESH // FINAL BRIEFING";
            o.Title = "PLANET TERRA-IX - PHASE VIII";
            o.Body =
                "Last stand on TERRA-IX - origin rainforest colony of the outer systems.\n\n" +
                "All previous planets compromised. This is the final canopy perimeter.\n" +
                "Twenty-five seeders confirmed. Kamikaze escort: EIGHTEEN units.\n" +
                "Bomber wing: EIGHT. Large-class war carrier: MAXIMUM aggression.\n" +
                "Spread delay: 1.2 seconds. Bio-tolerance: 13.0%.\n" +
                "Kill Seeders first; every Seeder destroyed slows the infection cascade.\n\n" +
                "DIRECTIVE:\n" +
                "Win here. There is nowhere left to fall back to.";
            o.Footer = "PRESS ANY KEY TO BEGIN DESCENT";
            o.ShowOverlay = true;
            o.AutoHide = false;
            o.AutoHideSeconds = 0f;
            o.DimStrength = 0.60f;
            o.PanelWidthRatio = 0.74f;
            o.PanelHeightRatio = 0.34f;
            o.ShowDebugOverlay = false;
        }

        public void SetupGameOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", "Planet Terra-IX", "", "");
            GameState.ScreenOverlayState.ShowOverlay = false;
            GameState.ScreenOverlayState.ShowDebugOverlay = false;
        }

        public void SetupVideoOverlay(string fileName)
        {
            throw new NotImplementedException();
        }

        private void SpawnJumpingFish(I3dWorld world)
        {
            var fishJumpAreas = GameState.SurfaceState.FishJumpAreas;
            if (fishJumpAreas == null || fishJumpAreas.Count == 0)
                return;

            int fishCount = Math.Min(100, fishJumpAreas.Count);
            int tileSize = Surface.TileSize();
            var jumpStyleRandom = new Random();
            for (int i = 0; i < fishCount; i++)
            {
                int areaIndex = (int)MathF.Floor(i * fishJumpAreas.Count / (float)fishCount);
                var area = fishJumpAreas[areaIndex];
                float jumpSpan = Math.Min(tileSize * 2f, Math.Max(tileSize, (area.EndTileX - area.StartTileX - 1) * tileSize));
                float baseOffsetX = 75 * ScreenSetup.ScreenScaleX;
                float minPathOffsetX = baseOffsetX + ((area.StartTileX - area.CenterTileX) * tileSize);
                float maxPathOffsetX = baseOffsetX + ((area.EndTileX - area.CenterTileX) * tileSize);
                var jumpStyle = JumpStyleVariants.PickRandom(jumpStyleRandom);
                int initialJumpDirection = JumpStyleVariants.PickAlternatingDirection(jumpStyleRandom, i);
                var jumpTiming = JumpStyleVariants.PickSpawnTiming(jumpStyleRandom);

                var jumpingFish = JumpingFish.CreateJumpingFish(Surface);
                jumpingFish.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                jumpingFish.WorldPosition = new Vector3 { };
                jumpingFish.SurfaceBasedId = GameState.SurfaceState.Global2DMap[area.CenterTileZ, area.CenterTileX].mapId;
                jumpingFish.ObjectOffsets = new Vector3
                {
                    x = baseOffsetX,
                    y = 500 * ScreenSetup.ScreenScaleY,
                    z = 400
                };
                jumpingFish.ObjectName = "JumpingFish";
                jumpingFish.Movement = new JumpingFishControls(jumpSpan, minPathOffsetX, maxPathOffsetX, initialJumpDirection, jumpStyle, jumpTiming);
                jumpingFish.ImpactStatus = new ImpactStatus { };
                jumpingFish.CrashBoxDebugMode = false;
                jumpingFish.CrashBoxes = new List<List<IVector3>>();
                jumpingFish.IsActive = true;
                world.WorldInhabitants.Add(jumpingFish);
            }
        }
    }
}
