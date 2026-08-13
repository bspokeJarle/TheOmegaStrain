using _3dRotations.Helpers;
using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using GameAiAndControls.Controls;
using GameAiAndControls.Controls.KamikazeDroneControls;
using GameAiAndControls.Controls.MotherShipMediumControls;
using GameAiAndControls.Controls.SpaceSwanControls;
using GameAiAndControls.Controls.ZeppelinBomberControls;
using GameAiAndControls.Controls.JumpingFishControls;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.Scene7
{
    public class Scene7 : IScene
    {
        Surface Surface = new();
        public const int TargetPatrolPolarBearCount = 30;
        private readonly List<PolarBearPlacementInfo> _polarBearPlacements = new();
        public IReadOnlyList<PolarBearPlacementInfo> PolarBearPlacements => _polarBearPlacements;

        public string SceneMusic { get; } = "music_battle";
        public SceneTypes SceneType { get; } = SceneTypes.Game;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.Winter;
        public ISceneDirector Director { get; } = new Scene7Director();
        public GameModes GameMode { get; } = GameModes.Playback;

        public float InfectionThresholdPercent { get; } = 14.0f;
        public int InfectionSpreadRate { get; } = 8;
        public int SeederOffscreenSpeedFactor { get; } = 21;
        public float LocalInfectionSpreadDelaySec { get; } = 1.5f;
        public float LocalInfectionSpreadRadius { get; } = 6000f;
        public float MotherShipLargeAggression { get; } = 1.20f;

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            var ship = Ship.CreateShip(Surface);
            Surface.Create2DMap(30000, 15000, GameMode, "Scene7SurfaceRecording_20260526_222053.retro");
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

            SpawnSeals(world);

            for (int b = 0; b < 7; b++)
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

            for (int i = 0; i < 16; i++)
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
                totalSeederCount: 23,
                regularSeed: 7071,
                nearSeederCount: 10,
                firstKillPowerUpType: PowerUpType.TravelSpeedLevel2);

            var motherShipLarge = MotherShipLarge.CreateMotherShipLarge(Surface);
            motherShipLarge.Rotation = new Vector3 { };
            motherShipLarge.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 88000 * ws };
            motherShipLarge.ObjectOffsets = new Vector3 { x = 0, y = -1500, z = 400 };
            motherShipLarge.ObjectName = "MotherShipLarge";
            motherShipLarge.Movement = new MotherShipLargeControls();

            var motherShipLargeLazer = Lazer.CreateLazer(Surface, scaleMultiplier: 2.5f);
            motherShipLargeLazer.CrashBoxDebugMode = false;
            var motherShipLargeWeapons = new List<I3dObject> { motherShipLargeLazer };
            motherShipLarge.WeaponSystems = new Weapons(motherShipLargeWeapons, motherShipLarge.Movement!, (_3dObject)motherShipLarge)
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

            if (SceneBiome == SceneBiomeTypes.Winter)
                world.WorldInhabitants.Add(SnowEmitter.CreateSnowEmitter(Surface));

            var towerPlacements = SurfaceGeneration.FindTowerPlacements(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight());
            SurfaceGeneration.FlattenTerrainAroundTowers_ToHighlands(
                GameState.SurfaceState.Global2DMap,
                Surface.MaxHeight(),
                towerPlacements,
                writeDebugLogs: false
            );

            foreach (var towerPlacement in towerPlacements)
            {
                var tower = SnowTower.CreateSnowTower(Surface);
                tower.Rotation = new Vector3 { };
                tower.WorldPosition = new Vector3 { };
                tower.SurfaceBasedId = GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].hasLandbasedObject = true;
                tower.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
                tower.ObjectName = "SnowTower";
                tower.Movement = new TowerControls();
                tower.CrashBoxDebugMode = false;
                tower.ImpactStatus = new ImpactStatus { };
                world.WorldInhabitants.Add(tower);
            }

            var treePlacements = SurfaceGeneration.FindTreePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), 30000);
            var housePlacements = SurfaceGeneration.FindHousePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.MaxHeight(), treePlacements, 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), treePlacements, radius: 1);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), housePlacements, radius: 1);
            var iglooPlacementAreas = new List<(int x, int y)>();
            for (int i = 0; i < treePlacements.Count; i++)
                iglooPlacementAreas.Add((treePlacements[i].x, treePlacements[i].y));
            for (int i = 0; i < housePlacements.Count; i++)
                iglooPlacementAreas.Add((housePlacements[i].x, housePlacements[i].y));

            var iglooRotationVariants = new float[] { -30f, -18f, -8f, 0f, 12f, 24f, 36f };
            var iglooIndex = 0;
            foreach (var iglooPlacement in iglooPlacementAreas)
            {
                bool useLargeIgloo = iglooIndex % 4 == 0;
                var igloo = useLargeIgloo
                    ? Igloo.CreateLargeIgloo(Surface)
                    : Igloo.CreateSmallIgloo(Surface);

                float rotationZ = iglooRotationVariants[iglooIndex % iglooRotationVariants.Length];
                igloo.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                igloo.Rotation = new Vector3 { x = 0, y = 0, z = rotationZ };
                igloo.SurfaceBasedId = GameState.SurfaceState.Global2DMap[iglooPlacement.y, iglooPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[iglooPlacement.y, iglooPlacement.x].hasLandbasedObject = true;
                igloo.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
                igloo.ImpactStatus = new ImpactStatus { };
                igloo.CrashBoxDebugMode = false;
                if (igloo.SurfaceBasedId > 0) world.WorldInhabitants.Add(igloo);

                iglooIndex++;
            }

            SpawnPolarBears(world);
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Top;
            o.Header = "RETROMESH // SECTOR BRIEFING";
            o.Title = "PLANET GLACIUS - PHASE VII";
            o.Body =
                "Entering orbit of GLACIUS - frozen world, permanent ice storm coverage.\n\n" +
                "Omega Strain adapts rapidly under sub-zero conditions.\n" +
                "Twenty-three seeders embedded in glacial terrain.\n" +
                "Kamikaze escort: SIXTEEN units. Bomber wing: SEVEN.\n" +
                "Spread delay: 1.5 seconds. Bio-tolerance: 14.0%.\n" +
                "Large-class war carrier incoming.\n\n" +
                "DIRECTIVE:\n" +
                "Clear all hostiles - the ice planet will not survive another hour.";
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
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", "Planet Glacius", "", "");
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

        private void SpawnSeals(I3dWorld world)
        {
            var fishJumpAreas = GameState.SurfaceState.FishJumpAreas;
            if (fishJumpAreas == null || fishJumpAreas.Count == 0)
                return;

            int sealCount = Math.Min(80, fishJumpAreas.Count);
            int tileSize = Surface.TileSize();
            var jumpStyleRandom = new Random();
            for (int i = 0; i < sealCount; i++)
            {
                int areaIndex = (int)MathF.Floor(i * fishJumpAreas.Count / (float)sealCount);
                var area = fishJumpAreas[areaIndex];
                float jumpSpan = Math.Min(tileSize * 2f, Math.Max(tileSize, (area.EndTileX - area.StartTileX - 1) * tileSize));
                float baseOffsetX = 75 * ScreenSetup.ScreenScaleX;
                float minPathOffsetX = baseOffsetX + ((area.StartTileX - area.CenterTileX) * tileSize);
                float maxPathOffsetX = baseOffsetX + ((area.EndTileX - area.CenterTileX) * tileSize);
                var jumpStyle = JumpStyleVariants.PickRandom(jumpStyleRandom);
                int initialJumpDirection = JumpStyleVariants.PickAlternatingDirection(jumpStyleRandom, i);
                var jumpTiming = JumpStyleVariants.PickSpawnTiming(jumpStyleRandom);

                var seal = Seal.CreateSeal(Surface);
                seal.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                seal.WorldPosition = new Vector3 { };
                seal.SurfaceBasedId = GameState.SurfaceState.Global2DMap[area.CenterTileZ, area.CenterTileX].mapId;
                seal.ObjectOffsets = new Vector3
                {
                    x = baseOffsetX,
                    y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled,
                    z = 400
                };
                seal.ObjectName = "Seal";
                seal.Movement = new JumpingFishControls(jumpSpan, minPathOffsetX, maxPathOffsetX, initialJumpDirection, jumpStyle, jumpTiming);
                seal.ImpactStatus = new ImpactStatus { };
                seal.CrashBoxDebugMode = false;
                seal.CrashBoxes = new List<List<IVector3>>();
                seal.IsActive = true;
                world.WorldInhabitants.Add(seal);
            }
        }

        private void SpawnPolarBears(I3dWorld world)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            _polarBearPlacements.Clear();

            int sizeZ = map.GetLength(0);
            int sizeX = map.GetLength(1);
            int patrolWidthTiles = 8;
            int patrolHeightTiles = 2;
            int maxHeightDelta = 10;
            int landingAreaSize = 8;
            int landingBufferTiles = 6;

            int mapCenterX = sizeX / 2;
            int mapCenterZ = sizeZ / 2;
            int landingTopLeftX = Math.Max(0, mapCenterX - (landingAreaSize / 2));
            int landingTopLeftZ = Math.Max(0, mapCenterZ - (landingAreaSize / 2));
            int guaranteedCenterX = mapCenterX;
            int guaranteedCenterZ = Math.Clamp(mapCenterZ + landingAreaSize + landingBufferTiles + 6, 1, sizeZ - 2);

            int maxHeight = 0;
            for (int z = 0; z < sizeZ; z++)
                for (int x = 0; x < sizeX; x++)
                    if (map[z, x].mapDepth > maxHeight) maxHeight = map[z, x].mapDepth;

            var patrolAreas = new List<(int centerX, int centerZ, int startX, int endX)>();
            var patrolAreaKeys = new HashSet<int>();

            void AddPatrolAreas(int areaMaxHeightDelta, bool requireFlatArea)
            {
                for (int z = 1; z < sizeZ - patrolHeightTiles - 1; z += patrolHeightTiles)
                {
                    for (int x = 1; x < sizeX - patrolWidthTiles - 1; x += patrolWidthTiles)
                    {
                        int key = (z * sizeX) + x;
                        if (patrolAreaKeys.Contains(key))
                            continue;

                        int areaCenterX = x + (patrolWidthTiles / 2);
                        int areaCenterZ = z + (patrolHeightTiles / 2);
                        if (IsInsideLandingPlatformOrBuffer(areaCenterX, areaCenterZ, landingTopLeftX, landingTopLeftZ, landingAreaSize, landingBufferTiles))
                            continue;

                        bool isValidArea = requireFlatArea
                            ? IsLandFlatArea(map, x, z, patrolWidthTiles, patrolHeightTiles, areaMaxHeightDelta, maxHeight)
                            : TryFindNearestDryLandTile(map, maxHeight, areaCenterX, areaCenterZ, out _, out _);

                        if (!isValidArea)
                            continue;

                        patrolAreaKeys.Add(key);
                        patrolAreas.Add((areaCenterX, areaCenterZ, x, x + patrolWidthTiles - 1));
                    }
                }
            }

            AddPatrolAreas(maxHeightDelta, requireFlatArea: true);
            if (patrolAreas.Count < TargetPatrolPolarBearCount)
                AddPatrolAreas(areaMaxHeightDelta: 20, requireFlatArea: true);
            if (patrolAreas.Count < TargetPatrolPolarBearCount)
                AddPatrolAreas(areaMaxHeightDelta: 32, requireFlatArea: true);
            if (patrolAreas.Count < TargetPatrolPolarBearCount)
                AddPatrolAreas(areaMaxHeightDelta: 0, requireFlatArea: false);

            patrolAreas.Sort((a, b) =>
            {
                int adx = a.centerX - mapCenterX;
                int adz = a.centerZ - mapCenterZ;
                int bdx = b.centerX - mapCenterX;
                int bdz = b.centerZ - mapCenterZ;
                int ad = (adx * adx) + (adz * adz);
                int bd = (bdx * bdx) + (bdz * bdz);
                return ad.CompareTo(bd);
            });

            if (patrolAreas.Count == 0)
            {
                (int centerX, int centerZ, int startX, int endX)? nearestFallback = null;
                int bestDistance = int.MaxValue;
                for (int z = 1; z < sizeZ - patrolHeightTiles - 1; z += patrolHeightTiles)
                {
                    for (int x = 1; x < sizeX - patrolWidthTiles - 1; x += patrolWidthTiles)
                    {
                        int areaCenterX = x + (patrolWidthTiles / 2);
                        int areaCenterZ = z + (patrolHeightTiles / 2);
                        if (IsInsideLandingPlatformOrBuffer(areaCenterX, areaCenterZ, landingTopLeftX, landingTopLeftZ, landingAreaSize, landingBufferTiles))
                            continue;

                        if (!IsLandFlatArea(map, x, z, patrolWidthTiles, patrolHeightTiles, maxHeightDelta: 16, maxHeight))
                            continue;

                        int dx = areaCenterX - mapCenterX;
                        int dz = areaCenterZ - mapCenterZ;
                        int distance = (dx * dx) + (dz * dz);
                        if (distance < bestDistance)
                        {
                            bestDistance = distance;
                            nearestFallback = (areaCenterX, areaCenterZ, x, x + patrolWidthTiles - 1);
                        }
                    }
                }

                if (nearestFallback.HasValue)
                    patrolAreas.Add(nearestFallback.Value);
            }

            int tileSize = Surface.TileSize();
            float baseOffsetX = 75 * ScreenSetup.ScreenScaleX;
            var placedBearTiles = new List<(int tileX, int tileZ)>();

            SpawnGuaranteedBear(world, map, sizeX, sizeZ, mapCenterX, mapCenterZ, guaranteedCenterX, guaranteedCenterZ, baseOffsetX, tileSize, patrolWidthTiles, maxHeight, placedBearTiles);

            int patrolBearsPlaced = 0;
            for (int i = 0; i < patrolAreas.Count && patrolBearsPlaced < TargetPatrolPolarBearCount; i++)
            {
                var area = patrolAreas[i];
                var centerTile = map[area.centerZ, area.centerX];
                var centerTerrain = GamePlayHelpers.GetTerrainType(centerTile.mapDepth, maxHeight);
                if (centerTerrain == GamePlayHelpers.TerrainType.DeepWater || centerTerrain == GamePlayHelpers.TerrainType.Coast)
                    continue;

                int tileZ = Math.Clamp(area.centerZ, 0, sizeZ - 1);
                int tileX = Math.Clamp(area.centerX, 0, sizeX - 1);
                if (map[tileZ, tileX].hasLandbasedObject)
                    continue;

                if (!TryFindNearestDryLandTile(map, maxHeight, tileX, tileZ, out int resolvedTileX, out int resolvedTileZ))
                    continue;

                tileX = resolvedTileX;
                tileZ = resolvedTileZ;

                if (!PolarBearPlacementHelpers.IsAtLeastOneScreenFromExisting(tileX, tileZ, tileSize, placedBearTiles))
                    continue;

                int tileDeltaX = tileX - area.centerX;
                float centerOffsetX = baseOffsetX + (tileDeltaX * tileSize);
                float halfSpan = (area.endX - area.startX) * 0.5f * tileSize;
                float minPathOffsetX = centerOffsetX - halfSpan;
                float maxPathOffsetX = centerOffsetX + halfSpan;

                int fallbackMapId = map[tileZ, tileX].mapId;
                if (fallbackMapId <= 0)
                    fallbackMapId = map[mapCenterZ, mapCenterX].mapId;

                var polarBear = PolarBear.CreatePolarBear(Surface);
                polarBear.WorldPosition = new Vector3 { };
                polarBear.SurfaceBasedId = fallbackMapId;
                polarBear.ObjectOffsets = new Vector3 { x = baseOffsetX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
                polarBear.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                polarBear.ObjectName = "PolarBear";
                polarBear.Movement = new PolarBearControls(minPathOffsetX, maxPathOffsetX);
                polarBear.ImpactStatus = new ImpactStatus { };
                polarBear.CrashBoxDebugMode = false;
                polarBear.IsActive = true;

                world.WorldInhabitants.Add(polarBear);
                GameState.SurfaceState.AiObjects.Add(polarBear);
                map[tileZ, tileX].hasLandbasedObject = true;
                placedBearTiles.Add((tileX, tileZ));
                patrolBearsPlaced++;
                _polarBearPlacements.Add(new PolarBearPlacementInfo(
                    "Patrol", tileX, tileZ, fallbackMapId, minPathOffsetX, maxPathOffsetX));
            }
        }

        private void SpawnGuaranteedBear(
            I3dWorld world,
            SurfaceData[,] map,
            int sizeX,
            int sizeZ,
            int mapCenterX,
            int mapCenterZ,
            int guaranteedCenterX,
            int guaranteedCenterZ,
            float baseOffsetX,
            int tileSize,
            int patrolWidthTiles,
            int maxHeight,
            List<(int tileX, int tileZ)> placedBearTiles)
        {
            int startX = Math.Max(1, guaranteedCenterX - (patrolWidthTiles / 2));
            int endX = Math.Min(sizeX - 2, startX + patrolWidthTiles - 1);

            int tileZ = Math.Clamp(guaranteedCenterZ, 0, sizeZ - 1);
            int tileX = Math.Clamp(guaranteedCenterX, 0, sizeX - 1);
            if (!TryFindNearestDryLandTile(map, maxHeight, tileX, tileZ, out tileX, out tileZ))
                return;

            int tileDeltaX = tileX - guaranteedCenterX;
            float centerOffsetX = baseOffsetX + (tileDeltaX * tileSize);
            float halfSpan = (endX - startX) * 0.5f * tileSize;
            float minPathOffsetX = centerOffsetX - halfSpan;
            float maxPathOffsetX = centerOffsetX + halfSpan;

            int mapId = map[tileZ, tileX].mapId;
            if (mapId <= 0)
                mapId = map[mapCenterZ, mapCenterX].mapId;

            var guaranteedBear = PolarBear.CreatePolarBear(Surface);
            guaranteedBear.WorldPosition = new Vector3 { };
            guaranteedBear.SurfaceBasedId = mapId;
            guaranteedBear.ObjectOffsets = new Vector3 { x = baseOffsetX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
            guaranteedBear.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
            guaranteedBear.ObjectName = "PolarBear";
            guaranteedBear.Movement = new PolarBearControls(minPathOffsetX, maxPathOffsetX);
            guaranteedBear.ImpactStatus = new ImpactStatus { };
            guaranteedBear.CrashBoxDebugMode = false;
            guaranteedBear.IsActive = true;

            world.WorldInhabitants.Add(guaranteedBear);
            GameState.SurfaceState.AiObjects.Add(guaranteedBear);
            map[tileZ, tileX].hasLandbasedObject = true;
            placedBearTiles.Add((tileX, tileZ));
            _polarBearPlacements.Add(new PolarBearPlacementInfo(
                "Guaranteed", tileX, tileZ, mapId, minPathOffsetX, maxPathOffsetX));
        }

        private static bool IsInsideLandingPlatformOrBuffer(int x, int z, int landingTopLeftX, int landingTopLeftZ, int landingSize, int buffer)
        {
            int minX = landingTopLeftX - buffer;
            int minZ = landingTopLeftZ - buffer;
            int maxX = landingTopLeftX + landingSize - 1 + buffer;
            int maxZ = landingTopLeftZ + landingSize - 1 + buffer;
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        private static bool IsLandFlatArea(SurfaceData[,] map, int startX, int startZ, int width, int height, int maxHeightDelta, int maxHeight)
        {
            int minDepth = int.MaxValue;
            int maxDepth = int.MinValue;

            for (int z = startZ; z < startZ + height; z++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    var tile = map[z, x];
                    if (!IsDryLandTerrain(tile, maxHeight))
                        return false;

                    if (tile.hasLandbasedObject)
                        return false;

                    minDepth = Math.Min(minDepth, tile.mapDepth);
                    maxDepth = Math.Max(maxDepth, tile.mapDepth);
                    if ((maxDepth - minDepth) > maxHeightDelta)
                        return false;
                }
            }

            return true;
        }

        private static bool IsDryLandTerrain(SurfaceData tile, int maxHeight)
        {
            var terrain = GamePlayHelpers.GetTerrainType(tile.mapDepth, maxHeight);
            return terrain == GamePlayHelpers.TerrainType.Grassland ||
                   terrain == GamePlayHelpers.TerrainType.Highlands;
        }

        private static bool TryFindNearestDryLandTile(SurfaceData[,] map, int maxHeight, int startX, int startZ, out int bestTileX, out int bestTileZ)
        {
            int sizeZ = map.GetLength(0);
            int sizeX = map.GetLength(1);

            bestTileX = Math.Clamp(startX, 0, sizeX - 1);
            bestTileZ = Math.Clamp(startZ, 0, sizeZ - 1);

            if (!map[bestTileZ, bestTileX].hasLandbasedObject && IsDryLandTerrain(map[bestTileZ, bestTileX], maxHeight))
                return true;

            int bestDistance = int.MaxValue;
            bool found = false;

            for (int z = 1; z < sizeZ - 1; z++)
            {
                for (int x = 1; x < sizeX - 1; x++)
                {
                    if (map[z, x].hasLandbasedObject)
                        continue;

                    if (!IsDryLandTerrain(map[z, x], maxHeight))
                        continue;

                    int dx = x - startX;
                    int dz = z - startZ;
                    int distance = (dx * dx) + (dz * dz);
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        bestTileX = x;
                        bestTileZ = z;
                        found = true;
                    }
                }
            }

            return found;
        }
    }

    public readonly record struct PolarBearPlacementInfo(
        string Source,
        int TileX,
        int TileZ,
        int MapId,
        float MinPathOffsetX,
        float MaxPathOffsetX);
}
