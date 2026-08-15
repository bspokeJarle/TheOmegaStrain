using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Gameplay.Controls.ZeppelinBomberControls;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;
using TheOmegaStrain.Gameplay.Controls.MotherShipSmallControls;
using TheOmegaStrain.Gameplay.Controls.JumpingFishControls;
using TheOmegaStrain.Gameplay.Controls.SpaceSwanControls;
using System;
using System.Collections.Generic;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;

namespace TheOmegaStrain.Game.Scenes.Scene3
{
    public class Scene3 : IScene
    {
        Surface Surface = new();

        public string SceneMusic { get; } = "music_kanpai";
        public SceneTypes SceneType { get; } = SceneTypes.Game;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.Rainforrest;
        public ISceneDirector Director { get; } = new Scene3Director();
        public GameModes GameMode { get; } = GameModes.Playback;
        public float InfectionThresholdPercent { get; } = 13.0f;
        public int InfectionSpreadRate { get; } = 5;
        public int SeederOffscreenSpeedFactor { get; } = 14;
        public float LocalInfectionSpreadDelaySec { get; } = 4.5f;
        public float LocalInfectionSpreadRadius { get; } = 4500f;
        public float MotherShipSmallAggression { get; } = 1.10f;
        private const int MinimumVisibleBambooHuts = 6;
        private static readonly float[] BambooHutRotationVariants = { -32f, -21f, -10f, 0f, 13f, 24f, 35f };
        private static readonly (int x, int y)[] VisibleBambooHutOffsets =
        {
            (2, 4),
            (6, 6),
            (11, 5),
            (15, 8),
            (4, 10),
            (9, 12),
            (14, 13)
        };

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            var ship = Ship.CreateShip(Surface);
            Surface.Create2DMap(30000, 15000, GameMode, "Scene3SurfaceRecording_20260526_193216.retro");
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

            // ZeppelinBombers — 2 bombers introduced this scene
            for (int b = 0; b < 2; b++)
            {
                var rmdBomber = new Random();
                var bomber = ZeppelinBomber.CreateZeppelinBomber(Surface);
                bomber.Rotation = new Vector3 { };
                bomber.WorldPosition = new Vector3 { x = (95700 + rmdBomber.Next(-40000, 40000)) * ws, y = 0, z = (92000 + rmdBomber.Next(-40000, 40000)) * ws };
                bomber.ObjectOffsets = new Vector3 { x = 0, y = -50, z = 400 };
                bomber.ObjectName = "ZeppelinBomber";
                bomber.Movement = new ZeppelinBomberControls();
                bomber.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.ZeppelinBomberHealth };
                bomber.CrashBoxDebugMode = false;
                bomber.IsActive = true;
                world.WorldInhabitants.Add(bomber);
                GameState.SurfaceState.AiObjects.Add(bomber);
            }

            // Drones — waiting until the player has a Decoy powerup
            for (int i = 0; i < 8; i++)
            {
                var rmd = new Random();

                var kamikaze = KamikazeDrone.CreateKamikazeDrone(Surface);
                kamikaze.WorldPosition = new Vector3 { x = (95700 + rmd.Next(-55000, 55000)) * ws, y = 0, z = (92000 + rmd.Next(-55000, 55000)) * ws };
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
                totalSeederCount: 12,
                regularSeed: 3031,
                nearSeederCount: 6);

            // Mothership — spawns inactive, enters when all seeders and drones are destroyed
            var motherShip = MotherShipSmall.CreateMotherShipSmall(Surface);
            motherShip.Rotation = new Vector3 { };
            motherShip.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 92000 * ws };
            motherShip.ObjectOffsets = new Vector3 { x = 0, y = -2500, z = 400 };
            motherShip.ObjectName = "MotherShipSmall";
            motherShip.Movement = new MotherShipSmallControls();
            motherShip.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShip.ObjectName, MotherShipSmallAggression) };
            motherShip.CrashBoxDebugMode = false;
            motherShip.HasPowerUp = false;
            motherShip.IsActive = false;
            world.WorldInhabitants.Add(motherShip);
            GameState.SurfaceState.AiObjects.Add(motherShip);

            // SpaceSwans — passive wildlife
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

            // Surface
            var surfaceObject = (_3dObject)Surface.GetSurfaceViewPort();
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

            var towerIndex = 0;
            foreach (var towerPlacement in towerPlacements)
            {
                towerIndex++;

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
            EnsureVisibleBambooHutPlacements(housePlacements);
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
                house.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = GetBambooHutRotationZ(bambooHutIndex, housePlacement.x, housePlacement.y) };
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

            o.Header = "RETROMESH // SECTOR BRIEFING";
            o.Title = "PLANET CYGNUS-9 - PHASE III";

            o.Body =
                "Dense jungle world CYGNUS-9 is under siege.\n\n" +
                "Omega Strain spreads through root systems at extreme speed.\n" +
                "Twelve seeders confirmed. Escort drones: EIGHT.\n" +
                "New threat: ZEPPELIN BOMBERS - two patrolling upper canopy.\n" +
                "Infection spread delay: 4.5 seconds. Tolerance: 13.0%.\n\n" +
                "DIRECTIVE:\n" +
                "Neutralize all threats. Watch the skies and the roots.";

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
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", "The Omega Strain", "", "");
            GameState.ScreenOverlayState.ShowOverlay = false;
            GameState.ScreenOverlayState.ShowDebugOverlay = false;
        }

        public void SetupVideoOverlay(string fileName)
        {
            throw new NotImplementedException();
        }

        private static float GetBambooHutRotationZ(int index, int tileX, int tileY)
        {
            int variant = Math.Abs((tileX * 37 + tileY * 17 + index * 11) % BambooHutRotationVariants.Length);
            return BambooHutRotationVariants[variant];
        }

        private void EnsureVisibleBambooHutPlacements(List<(int x, int y, int height)> housePlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null || map.Length == 0)
                return;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            if (sizeX < 4 || sizeY < 4)
                return;

            int tileSize = Math.Max(1, Surface.TileSize());
            int centerX = Math.Clamp((int)(GameState.SurfaceState.GlobalMapPosition.x / tileSize), 2, sizeX - 3);
            int centerY = Math.Clamp((int)(GameState.SurfaceState.GlobalMapPosition.z / tileSize), 2, sizeY - 3);

            var used = new HashSet<(int x, int y)>();
            int visibleCount = 0;
            foreach (var placement in housePlacements)
            {
                used.Add((placement.x, placement.y));
                if (IsInInitialSurfaceViewport(placement.x, placement.y, centerX, centerY))
                    visibleCount++;
            }

            foreach (var offset in VisibleBambooHutOffsets)
            {
                if (visibleCount >= MinimumVisibleBambooHuts)
                    break;

                int targetX = centerX + offset.x;
                int targetY = centerY + offset.y;
                if (TryFindBambooHutPlacementNear(map, targetX, targetY, used, out var placement))
                {
                    housePlacements.Add(placement);
                    used.Add((placement.x, placement.y));
                    visibleCount++;
                }
            }
        }

        private static bool IsInInitialSurfaceViewport(int tileX, int tileY, int centerX, int centerY)
        {
            return tileX >= centerX
                && tileX <= centerX + SurfaceSetup.viewPortSize - 2
                && tileY >= centerY + 1
                && tileY <= centerY + (SurfaceSetup.viewPortSize / 1.5) + 1;
        }

        private bool TryFindBambooHutPlacementNear(
            SurfaceData[,] map,
            int targetX,
            int targetY,
            HashSet<(int x, int y)> used,
            out (int x, int y, int height) placement)
        {
            const int searchRadius = 8;
            placement = default;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            targetX = Math.Clamp(targetX, 2, sizeX - 3);
            targetY = Math.Clamp(targetY, 2, sizeY - 3);

            for (int radius = 0; radius <= searchRadius; radius++)
            {
                for (int y = targetY - radius; y <= targetY + radius; y++)
                {
                    for (int x = targetX - radius; x <= targetX + radius; x++)
                    {
                        if (radius > 0 && Math.Abs(x - targetX) != radius && Math.Abs(y - targetY) != radius)
                            continue;

                        if (!IsValidBambooHutTile(map, x, y, used))
                            continue;

                        placement = (x, y, map[y, x].mapDepth);
                        return true;
                    }
                }
            }

            return false;
        }

        private bool IsValidBambooHutTile(SurfaceData[,] map, int x, int y, HashSet<(int x, int y)> used)
        {
            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            if (x < 2 || y < 2 || x >= sizeX - 2 || y >= sizeY - 2)
                return false;

            if (used.Contains((x, y)))
                return false;

            var tile = map[y, x];
            if (tile.hasLandbasedObject || tile.isInfected || tile.isCratered)
                return false;

            int maxHeight = Surface.MaxHeight();
            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            int mountainCutoff = (int)(maxHeight * 0.70);
            if (tile.mapDepth <= coastCutoff || tile.mapDepth >= mountainCutoff)
                return false;

            for (int dy = 0; dy <= 1; dy++)
            {
                for (int dx = 0; dx <= 1; dx++)
                {
                    var quadTile = map[y + dy, x + dx];
                    if (quadTile.mapDepth <= coastCutoff
                        || quadTile.mapDepth >= mountainCutoff
                        || quadTile.hasLandbasedObject
                        || quadTile.isInfected
                        || quadTile.isCratered)
                    {
                        return false;
                    }
                }
            }

            int h = tile.mapDepth;
            return Math.Abs(h - map[y, x - 1].mapDepth) < 8
                && Math.Abs(h - map[y, x + 1].mapDepth) < 8
                && Math.Abs(h - map[y - 1, x].mapDepth) < 8
                && Math.Abs(h - map[y + 1, x].mapDepth) < 8;
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
