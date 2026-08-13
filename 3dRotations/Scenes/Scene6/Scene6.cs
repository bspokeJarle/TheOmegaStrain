using _3dRotations.World.Objects;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using GameAiAndControls.Controls.MotherShipMediumControls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;
using _3dRotations.Helpers;
using GameAiAndControls.Controls;
using GameAiAndControls.Controls.ZeppelinBomberControls;
using GameAiAndControls.Controls.KamikazeDroneControls;
using GameAiAndControls.Controls.JumpingFishControls;
using GameAiAndControls.Controls.SpaceSwanControls;
using System;

namespace _3dRotations.Scenes.Scene6
{
    public class Scene6 : IScene
    {
        Surface Surface = new();

        public string SceneMusic { get; } = "music_kanpai";
        public SceneTypes SceneType { get; } = SceneTypes.Game;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.Desert;
        public ISceneDirector Director { get; } = new Scene6Director();
        public GameModes GameMode { get; } = GameModes.Playback;

        public float InfectionThresholdPercent { get; } = 15.0f;
        public int InfectionSpreadRate { get; } = 7;
        public int SeederOffscreenSpeedFactor { get; } = 20;
        public float LocalInfectionSpreadDelaySec { get; } = 1.8f;
        public float LocalInfectionSpreadRadius { get; } = 5800f;
        public float MotherShipMediumAggression { get; } = 1.35f;
        private const int GuaranteedStartTentCount = 12;
        private const int DesertRockPlacementMax = 12000;
        private const int DesertCactusPlacementMax = 16000;
        private const int DesertTentPlacementMax = 30000;
        private const int DesertTentPlacementSpacingTiles = 20;
        private static readonly float[] BedouinTentRotationVariants = { -32f, -19f, -7f, 6f, 18f, 31f };

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            var ship = Ship.CreateShip(Surface);
            GameState.SurfaceState.SceneBiome = SceneBiome;
            Surface.Create2DMap(30000, 15000, GameMode, "Scene6SurfaceRecording_20260530_desert_lakes.retro");
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

            for (int b = 0; b < 6; b++)
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

            for (int i = 0; i < 14; i++)
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
                totalSeederCount: 21,
                regularSeed: 6061,
                nearSeederCount: 5,
                firstRingRadius: 6500f,
                ringRadiusStep: 9000f,
                firstKillPowerUpType: PowerUpType.TravelSpeedLevel1);

            var motherShipMedium = MotherShipMedium.CreateMotherShipMedium(Surface);
            motherShipMedium.Rotation = new Vector3 { };
            motherShipMedium.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 90000 * ws };
            motherShipMedium.ObjectOffsets = new Vector3 { x = 0, y = -1500, z = 400 };
            motherShipMedium.ObjectName = "MotherShipMedium";
            motherShipMedium.Movement = new MotherShipMediumControls();
            var motherShipMediumLazer = Lazer.CreateLazer(
                Surface,
                scaleMultiplier: 2.0f,
                crashBoxScaleMultiplier: WeaponSetup.EnemyLazerMediumCrashBoxScale);
            motherShipMediumLazer.CrashBoxDebugMode = false;
            var motherShipMediumWeapons = new List<I3dObject> { motherShipMediumLazer };
            motherShipMedium.WeaponSystems = new Weapons(motherShipMediumWeapons, motherShipMedium.Movement!, (_3dObject)motherShipMedium)
            {
                ShowAimAssist = false,
                FireAsEnemyWeapon = true,
                EnemyLazerName = "EnemyLazerMedium"
            };
            motherShipMedium.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShipMedium.ObjectName, MotherShipMediumAggression) };
            motherShipMedium.CrashBoxDebugMode = false;
            motherShipMedium.HasPowerUp = false;
            motherShipMedium.IsActive = false;
            world.WorldInhabitants.Add(motherShipMedium);
            GameState.SurfaceState.AiObjects.Add(motherShipMedium);

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
            world.WorldInhabitants.Add(SandEmitter.CreateSandEmitter(Surface));

            var towerPlacements = SurfaceGeneration.FindTowerPlacements(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight());
            AddGuaranteedStartTowerPlacements(towerPlacements);
            RemoveStartPlatformPlacements(towerPlacements);
            SurfaceGeneration.FlattenTerrainAroundTowers_ToHighlands(
                GameState.SurfaceState.Global2DMap,
                Surface.MaxHeight(),
                towerPlacements,
                writeDebugLogs: false
            );

            foreach (var towerPlacement in towerPlacements)
            {
                if (IsOnStartPlatform(towerPlacement.x, towerPlacement.y))
                    continue;

                var tower = DesertTower.CreateDesertTower(Surface);
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

            var oasisPlantPlacements = SurfaceGeneration.FindDesertOasisPlantPlacements(
                GameState.SurfaceState.Global2DMap,
                Surface.GlobalMapSize(),
                Surface.MaxHeight(),
                maxPlants: 90);
            RemoveStartPlatformPlacements(oasisPlantPlacements);
            var oasisIndex = 0;
            foreach (var oasisPlacement in oasisPlantPlacements)
            {
                if (IsOnStartPlatform(oasisPlacement.x, oasisPlacement.y))
                    continue;
                if (GameState.SurfaceState.Global2DMap[oasisPlacement.y, oasisPlacement.x].hasLandbasedObject)
                    continue;

                oasisIndex++;
                bool useLargePlant = oasisIndex % 3 == 0;
                var plant = useLargePlant
                    ? AlienPlant.CreateLargeAlienPlant(Surface)
                    : AlienPlant.CreateSmallAlienPlant(Surface);

                plant.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                plant.SurfaceBasedId = GameState.SurfaceState.Global2DMap[oasisPlacement.y, oasisPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[oasisPlacement.y, oasisPlacement.x].hasLandbasedObject = true;
                plant.ObjectOffsets = new Vector3
                {
                    x = 75 * ScreenSetup.ScreenScaleX,
                    y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled,
                    z = 400
                };
                plant.ObjectName = useLargePlant ? "LargeAlienPlant" : "SmallAlienPlant";
                plant.Movement = useLargePlant ? new LargePalmControls() : new SmallPalmControls();
                plant.ImpactStatus = new ImpactStatus { };
                plant.CrashBoxDebugMode = false;
                if (plant.SurfaceBasedId > 0) world.WorldInhabitants.Add(plant);
            }

            var rockPlacements = SurfaceGeneration.FindTreePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), DesertRockPlacementMax);
            AddGuaranteedStartRockPlacements(rockPlacements);
            RemoveStartPlatformPlacements(rockPlacements);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), rockPlacements, radius: 1);
            foreach (var rockPlacement in rockPlacements)
            {
                if (IsOnStartPlatform(rockPlacement.x, rockPlacement.y))
                    continue;
                if (GameState.SurfaceState.Global2DMap[rockPlacement.y, rockPlacement.x].hasLandbasedObject)
                    continue;

                var rocks = DesertRockFormation.CreateDesertRockFormation(Surface);
                rocks.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                rocks.SurfaceBasedId = GameState.SurfaceState.Global2DMap[rockPlacement.y, rockPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[rockPlacement.y, rockPlacement.x].hasLandbasedObject = true;
                rocks.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                rocks.ObjectName = "DesertRockFormation";
                rocks.Movement = new DesertRockControls();
                rocks.ImpactStatus = new ImpactStatus { };
                rocks.CrashBoxDebugMode = false;
                if (rocks.SurfaceBasedId > 0) world.WorldInhabitants.Add(rocks);
            }

            var reservedNaturePlacements = new List<(int x, int y, int height)>();
            reservedNaturePlacements.AddRange(oasisPlantPlacements);
            reservedNaturePlacements.AddRange(rockPlacements);
            var tentPlacements = SurfaceGeneration.FindHousePlacementAreas(
                GameState.SurfaceState.Global2DMap,
                Surface.GlobalMapSize(),
                Surface.MaxHeight(),
                reservedNaturePlacements,
                overrideMaxHouses: DesertTentPlacementMax,
                placementSpacing: DesertTentPlacementSpacingTiles);
            AddGuaranteedStartTentPlacements(tentPlacements);
            RemoveStartPlatformPlacements(tentPlacements);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), tentPlacements, radius: 1);
            int tentIndex = 0;
            foreach (var tentPlacement in tentPlacements)
            {
                if (IsOnStartPlatform(tentPlacement.x, tentPlacement.y))
                    continue;
                if (GameState.SurfaceState.Global2DMap[tentPlacement.y, tentPlacement.x].hasLandbasedObject)
                    continue;

                var tent = BedouinTent.CreateBedouinTent(Surface);
                tent.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                tent.SurfaceBasedId = GameState.SurfaceState.Global2DMap[tentPlacement.y, tentPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[tentPlacement.y, tentPlacement.x].hasLandbasedObject = true;
                tent.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                tent.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = GetBedouinTentRotationZ(tentIndex, tentPlacement.x, tentPlacement.y) };
                tent.ObjectName = "BedouinTent";
                tent.Movement = new BedouinTentControls();
                tent.ImpactStatus = new ImpactStatus { };
                tent.CrashBoxDebugMode = false;
                if (tent.SurfaceBasedId > 0)
                {
                    world.WorldInhabitants.Add(tent);
                    tentIndex++;
                }
            }

            var cactusPlacements = SurfaceGeneration.FindTreePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), DesertCactusPlacementMax);
            AddGuaranteedStartCactusPlacements(cactusPlacements);
            RemoveStartPlatformPlacements(cactusPlacements);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), cactusPlacements, radius: 1);
            foreach (var cactusPlacement in cactusPlacements)
            {
                if (IsOnStartPlatform(cactusPlacement.x, cactusPlacement.y))
                    continue;
                if (GameState.SurfaceState.Global2DMap[cactusPlacement.y, cactusPlacement.x].hasLandbasedObject)
                    continue;

                var cactus = Cactus.CreateCactus(Surface);
                cactus.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                cactus.SurfaceBasedId = GameState.SurfaceState.Global2DMap[cactusPlacement.y, cactusPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[cactusPlacement.y, cactusPlacement.x].hasLandbasedObject = true;
                cactus.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                cactus.ObjectName = "Cactus";
                cactus.Movement = new CactusControls();
                cactus.ImpactStatus = new ImpactStatus { };
                cactus.CrashBoxDebugMode = false;
                if (cactus.SurfaceBasedId > 0) world.WorldInhabitants.Add(cactus);
            }
        }

        private void AddGuaranteedStartTentPlacements(List<(int x, int y, int height)> tentPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            int startX = platformCenter.x;
            int startY = platformCenter.z;

            int maxHeight = Surface.MaxHeight();
            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            const int startTentTargetDistanceTiles = 100;
            var used = new HashSet<(int x, int y)>();
            foreach (var placement in tentPlacements)
                used.Add((placement.x, placement.y));

            int nearStartTentCount = 0;
            foreach (var placement in tentPlacements)
            {
                if (IsUsableNearStartTentPlacement(placement.x, placement.y))
                    nearStartTentCount++;
            }

            int[] targetDistances = { 90, 105, 120, 135, 150, 165, 180, 195, 210, 225 };
            for (int i = 0; nearStartTentCount < GuaranteedStartTentCount && i < targetDistances.Length; i++)
            {
                if (TryAddTentPlacementNearTargetDistance(targetDistances[i]))
                    nearStartTentCount++;
            }

            bool TryAddTentPlacementNearTargetDistance(int targetDistance)
            {
                bool TryAddTentPlacementOnRing(int radius)
                {
                    if (radius <= LandingPlatformHelpers.LandingPlatformSizeTiles)
                        return false;

                    for (int tolerance = 0; tolerance <= 4; tolerance++)
                    {
                        int searchRadius = radius + tolerance;
                        for (int oy = -searchRadius; oy <= searchRadius; oy++)
                        {
                            for (int ox = -searchRadius; ox <= searchRadius; ox++)
                            {
                                double distance = Math.Sqrt((ox * ox) + (oy * oy));
                                if (Math.Abs(distance - radius) > tolerance + 0.25)
                                    continue;

                                int x = (startX + ox + sizeX) % sizeX;
                                int y = (startY + oy + sizeY) % sizeY;
                                if (TryAddTentPlacement(x, y))
                                    return true;
                            }
                        }
                    }

                    return false;
                }

                for (int delta = 0; delta <= 35; delta++)
                {
                    if (TryAddTentPlacementOnRing(targetDistance - delta))
                        return true;
                    if (delta > 0 && TryAddTentPlacementOnRing(targetDistance + delta))
                        return true;
                }

                return false;
            }

            bool TryAddTentPlacement(int x, int y)
            {
                if (x <= 1 || y <= 1 || x >= sizeX - 2 || y >= sizeY - 2)
                    return false;
                if (IsOnStartPlatform(x, y))
                    return false;
                if (used.Contains((x, y)))
                    return false;
                if (HasPlacementNearby(x, y))
                    return false;

                var tile = map[y, x];
                if (tile.hasLandbasedObject)
                    return false;
                if (tile.mapDepth <= coastCutoff)
                    return false;

                used.Add((x, y));
                tentPlacements.Insert(0, (x, y, tile.mapDepth));
                return true;
            }

            bool IsUsableNearStartTentPlacement(int x, int y)
            {
                int dx = x - startX;
                int dy = y - startY;
                double distance = Math.Sqrt((dx * dx) + (dy * dy));
                if (Math.Abs(distance - startTentTargetDistanceTiles) > 35)
                    return false;
                if (IsOnStartPlatform(x, y))
                    return false;
                if (x < 0 || y < 0 || x >= sizeX || y >= sizeY)
                    return false;

                return !map[y, x].hasLandbasedObject;
            }

            bool HasPlacementNearby(int x, int y)
            {
                const int minSpacing = 5;
                foreach (var placement in used)
                {
                    if (Math.Abs(placement.x - x) <= minSpacing &&
                        Math.Abs(placement.y - y) <= minSpacing)
                        return true;
                }

                return false;
            }
        }

        private static float GetBedouinTentRotationZ(int index, int tileX, int tileY)
        {
            int variant = Math.Abs((tileX * 31 + tileY * 19 + index * 13) % BedouinTentRotationVariants.Length);
            return BedouinTentRotationVariants[variant];
        }

        private void SpawnJumpingFish(I3dWorld world)
        {
            var fishJumpAreas = GameState.SurfaceState.FishJumpAreas;
            var map = GameState.SurfaceState.Global2DMap;
            if (fishJumpAreas == null || fishJumpAreas.Count == 0 || map == null)
                return;

            int tileSize = Surface.TileSize();
            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            var jumpStyleRandom = new Random();

            foreach (var area in fishJumpAreas)
            {
                int fishInArea = GetDesertFishCount(area);
                for (int i = 0; i < fishInArea; i++)
                {
                    var segment = GetFishSegment(area, i, fishInArea);
                    var placement = GetFishPlacement(map, area, segment, i, fishInArea);

                    float jumpSpan = Math.Min(tileSize * 2f, Math.Max(tileSize, (placement.endX - placement.startX - 1) * tileSize));
                    float baseOffsetX = 75 * ScreenSetup.ScreenScaleX;
                    float minPathOffsetX = baseOffsetX + ((placement.startX - placement.centerX) * tileSize);
                    float maxPathOffsetX = baseOffsetX + ((placement.endX - placement.centerX) * tileSize);
                    int initialJumpDirection = JumpStyleVariants.PickAlternatingDirection(jumpStyleRandom, i);
                    var jumpStyle = JumpStyleVariants.PickRandom(jumpStyleRandom);
                    var jumpTiming = JumpStyleVariants.PickSpawnTiming(jumpStyleRandom);

                    var jumpingFish = JumpingFish.CreateJumpingFish(Surface);
                    jumpingFish.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                    jumpingFish.WorldPosition = new Vector3 { };
                    jumpingFish.SurfaceBasedId = map[placement.centerZ, placement.centerX].mapId;
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

            int GetDesertFishCount(FishJumpArea area)
            {
                int count = Math.Clamp(area.ComponentTileCount / 5000, 1, 10);
                double platformDistance = Math.Sqrt(
                    ((area.CenterTileX - platformCenter.x) * (area.CenterTileX - platformCenter.x)) +
                    ((area.CenterTileZ - platformCenter.z) * (area.CenterTileZ - platformCenter.z)));

                if (platformDistance <= 140)
                    count = Math.Max(count, 4);

                return count;
            }

            static (int centerX, int centerZ, int startX, int endX) GetFishPlacement(
                SurfaceData[,] map,
                FishJumpArea area,
                (int startX, int endX) segment,
                int index,
                int fishCount)
            {
                int centerX = segment.startX + ((segment.endX - segment.startX) / 2);
                int mapHeight = map.GetLength(0);

                foreach (int candidateZ in GetFishZCandidates(area.CenterTileZ, index, fishCount, mapHeight))
                {
                    if (!TryGetWaterSpan(map, centerX, candidateZ, out int rowStartX, out int rowEndX))
                        continue;

                    int startX = Math.Max(segment.startX, rowStartX);
                    int endX = Math.Min(segment.endX, rowEndX);
                    if (endX - startX < 3)
                        continue;

                    centerX = Math.Clamp(centerX, startX, endX);
                    return (centerX, candidateZ, startX, endX);
                }

                return (centerX, area.CenterTileZ, segment.startX, segment.endX);
            }

            static IEnumerable<int> GetFishZCandidates(int centerZ, int index, int fishCount, int mapHeight)
            {
                int preferredOffset = GetFishZOffset(index, fishCount);
                yield return Math.Clamp(centerZ + preferredOffset, 0, mapHeight - 1);

                if (preferredOffset != 0)
                    yield return Math.Clamp(centerZ, 0, mapHeight - 1);

                for (int distance = 4; distance <= 28; distance += 4)
                {
                    yield return Math.Clamp(centerZ - distance, 0, mapHeight - 1);
                    yield return Math.Clamp(centerZ + distance, 0, mapHeight - 1);
                }
            }

            static int GetFishZOffset(int index, int fishCount)
            {
                if (fishCount <= 1)
                    return 0;

                int[] offsets = { -10, 7, -3, 13, -16, 2, 18, -22, 24, -7 };
                return offsets[index % offsets.Length];
            }

            static bool TryGetWaterSpan(SurfaceData[,] map, int centerX, int centerZ, out int startX, out int endX)
            {
                startX = centerX;
                endX = centerX;
                int mapHeight = map.GetLength(0);
                int mapWidth = map.GetLength(1);
                if (centerZ < 0 || centerZ >= mapHeight || centerX < 0 || centerX >= mapWidth)
                    return false;
                if (!IsFishWaterTile(map[centerZ, centerX]))
                    return false;

                while (startX > 0 && IsFishWaterTile(map[centerZ, startX - 1]))
                    startX--;
                while (endX < mapWidth - 1 && IsFishWaterTile(map[centerZ, endX + 1]))
                    endX++;

                return true;
            }

            static bool IsFishWaterTile(SurfaceData tile)
            {
                if (tile.hasLandbasedObject || tile.isInfected || tile.isCratered)
                    return false;

                var terrainType = GamePlayHelpers.GetTerrainType(tile.mapDepth, MapSetup.maxHeight);
                return terrainType == GamePlayHelpers.TerrainType.DeepWater ||
                       terrainType == GamePlayHelpers.TerrainType.Coast;
            }

            static (int startX, int endX) GetFishSegment(FishJumpArea area, int index, int fishCount)
            {
                if (fishCount <= 1)
                    return (area.StartTileX, area.EndTileX);

                int width = Math.Max(1, area.EndTileX - area.StartTileX + 1);
                int startX = area.StartTileX + (int)MathF.Floor(index * width / (float)fishCount);
                int endX = area.StartTileX + (int)MathF.Floor((index + 1) * width / (float)fishCount) - 1;
                if (index == fishCount - 1)
                    endX = area.EndTileX;

                startX = Math.Clamp(startX, area.StartTileX, area.EndTileX);
                endX = Math.Clamp(Math.Max(endX, startX), area.StartTileX, area.EndTileX);
                return (startX, endX);
            }
        }

        private void RemoveStartPlatformPlacements(List<(int x, int y, int height)> placements)
        {
            placements.RemoveAll(p => IsOnStartPlatform(p.x, p.y));
        }

        private bool IsOnStartPlatform(int x, int y)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return false;

            return LandingPlatformHelpers.IsLandingPlatformTile(map, x, y);
        }

        private void AddGuaranteedStartRockPlacements(List<(int x, int y, int height)> rockPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            int startX = platformCenter.x;
            int startY = platformCenter.z;

            int maxHeight = Surface.MaxHeight();
            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            var used = new HashSet<(int x, int y)>();
            foreach (var placement in rockPlacements)
                used.Add((placement.x, placement.y));

            (int dx, int dy)[] nearStartOffsets =
            {
                (10, 14),
                (18, 15)
            };

            foreach (var (dx, dy) in nearStartOffsets)
            {
                int x = (startX + dx) % sizeX;
                int y = (startY + dy) % sizeY;
                TryAddRockPlacementNear(x, y);
            }

            void TryAddRockPlacementNear(int targetX, int targetY)
            {
                const int searchRadius = 4;
                for (int radius = 0; radius <= searchRadius; radius++)
                {
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            if (Math.Abs(ox) != radius && Math.Abs(oy) != radius)
                                continue;

                            int x = (targetX + ox + sizeX) % sizeX;
                            int y = (targetY + oy + sizeY) % sizeY;
                            if (TryAddRockPlacement(x, y))
                                return;
                        }
                    }
                }
            }

            bool TryAddRockPlacement(int x, int y)
            {
                if (x <= 1 || y <= 1 || x >= sizeX - 2 || y >= sizeY - 2)
                    return false;
                if (used.Contains((x, y)))
                    return false;

                var tile = map[y, x];
                if (tile.hasLandbasedObject)
                    return false;
                if (tile.mapDepth <= coastCutoff)
                    return false;

                used.Add((x, y));
                rockPlacements.Insert(0, (x, y, tile.mapDepth));
                return true;
            }
        }

        private void AddGuaranteedStartTowerPlacements(List<(int x, int y, int height)> towerPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            int startX = platformCenter.x;
            int startY = platformCenter.z;

            int maxHeight = Surface.MaxHeight();
            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            int highlandsMin = Math.Max(coastCutoff + 1, (int)(maxHeight * 0.40));
            var used = new HashSet<(int x, int y)>();
            foreach (var placement in towerPlacements)
                used.Add((placement.x, placement.y));

            (int dx, int dy)[] nearStartOffsets =
            {
                (12, 5)
            };

            foreach (var (dx, dy) in nearStartOffsets)
            {
                int x = (startX + dx) % sizeX;
                int y = (startY + dy) % sizeY;
                TryAddTowerPlacementNear(x, y);
            }

            void TryAddTowerPlacementNear(int targetX, int targetY)
            {
                const int searchRadius = 4;
                for (int radius = 0; radius <= searchRadius; radius++)
                {
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            if (Math.Abs(ox) != radius && Math.Abs(oy) != radius)
                                continue;

                            int x = (targetX + ox + sizeX) % sizeX;
                            int y = (targetY + oy + sizeY) % sizeY;
                            if (TryAddTowerPlacement(x, y))
                                return;
                        }
                    }
                }
            }

            bool TryAddTowerPlacement(int x, int y)
            {
                if (x <= 2 || y <= 2 || x >= sizeX - 3 || y >= sizeY - 3)
                    return false;
                if (used.Contains((x, y)))
                    return false;

                var tile = map[y, x];
                if (tile.hasLandbasedObject)
                    return false;
                if (tile.mapDepth <= coastCutoff)
                    return false;

                int height = Math.Max(tile.mapDepth, highlandsMin);
                used.Add((x, y));
                towerPlacements.Insert(0, (x, y, height));
                return true;
            }
        }

        private void AddGuaranteedStartCactusPlacements(List<(int x, int y, int height)> cactusPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            int sizeY = map.GetLength(0);
            int sizeX = map.GetLength(1);
            var platformCenter = LandingPlatformHelpers.GetLandingPlatformCenterTile(map);
            int startX = platformCenter.x;
            int startY = platformCenter.z;

            int maxHeight = Surface.MaxHeight();
            int coastCutoff = Math.Max(1, (int)Math.Ceiling(maxHeight * 0.15));
            var used = new HashSet<(int x, int y)>();
            foreach (var placement in cactusPlacements)
                used.Add((placement.x, placement.y));

            (int dx, int dy)[] nearStartOffsets =
            {
                (7, 8),
                (14, 10),
                (9, 15)
            };

            foreach (var (dx, dy) in nearStartOffsets)
            {
                int x = (startX + dx) % sizeX;
                int y = (startY + dy) % sizeY;
                TryAddCactusPlacementNear(x, y);
            }

            void TryAddCactusPlacementNear(int targetX, int targetY)
            {
                const int searchRadius = 3;
                for (int radius = 0; radius <= searchRadius; radius++)
                {
                    for (int oy = -radius; oy <= radius; oy++)
                    {
                        for (int ox = -radius; ox <= radius; ox++)
                        {
                            if (Math.Abs(ox) != radius && Math.Abs(oy) != radius)
                                continue;

                            int x = (targetX + ox + sizeX) % sizeX;
                            int y = (targetY + oy + sizeY) % sizeY;
                            if (TryAddCactusPlacement(x, y))
                                return;
                        }
                    }
                }
            }

            bool TryAddCactusPlacement(int x, int y)
            {
                if (x <= 1 || y <= 1 || x >= sizeX - 2 || y >= sizeY - 2)
                    return false;
                if (used.Contains((x, y)))
                    return false;

                var tile = map[y, x];
                if (tile.hasLandbasedObject)
                    return false;
                if (tile.mapDepth <= coastCutoff)
                    return false;

                used.Add((x, y));
                cactusPlacements.Insert(0, (x, y, tile.mapDepth));
                return true;
            }
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Top;
            o.Header = "RETROMESH // SECTOR BRIEFING";
            o.Title = "PLANET ARIDUS - PHASE VI";
            o.Body =
                "Descending on ARIDUS - arid desert world, surface temperature extreme.\n\n" +
                "Sand contamination identified as Omega Strain vector.\n" +
                "Twenty-one seeders detected across dune fields.\n" +
                "Kamikaze escort: FOURTEEN units. Bomber wing: SIX.\n" +
                "Spread delay: 1.8 seconds. Bio-tolerance: 15.0%.\n\n" +
                "DIRECTIVE:\n" +
                "Destroy all seeders before the desert biome collapses entirely.";
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
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", "Planet Aridus", "", "");
            GameState.ScreenOverlayState.ShowOverlay = false;
            GameState.ScreenOverlayState.ShowDebugOverlay = false;
        }

        public void SetupVideoOverlay(string fileName)
        {
            throw new NotImplementedException();
        }
    }
}
