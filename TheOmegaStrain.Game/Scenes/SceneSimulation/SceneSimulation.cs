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
using System.Globalization;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.Scenes.SceneSimulation
{
    public class SceneSimulation : IScene
    {
        Surface Surface = new();
        private const int TargetWinterPolarBearCount = 31;
        private const int LeafTreePlacementMax = 12000;
        private const int NearPlatformLeafTreeTarget = 14;
        private const int NearPlatformLeafTreeSearchRadius = 26;

        private static readonly SceneBiomeTypes[] BiomeCycle = new[]
        {
            SceneBiomeTypes.HillsWoods,
            SceneBiomeTypes.Winter,
            SceneBiomeTypes.Rainforrest,
            SceneBiomeTypes.Desert,
        };

        private readonly SceneBiomeTypes _biome;
        private readonly int _simulationRound;

        // Derived scaling values based on round
        private readonly int _seeders;
        private readonly int _drones;
        private readonly int _bombers;
        private readonly float _infectionThreshold;
        private readonly int _infectionSpreadRate;
        private readonly int _offscreenSpeedFactor;
        private readonly float _spreadDelaySec;
        private readonly float _spreadRadius;
        private readonly float _motherShipAggression;
        private readonly bool _useLargeMotherShip;

        public string SceneMusic { get; private set; } = "music_flight";
        public SceneTypes SceneType { get; } = SceneTypes.Simulation;
        public SceneBiomeTypes SceneBiome { get; }
        public GameModes GameMode { get; } = GameModes.Live;
        public ISceneDirector Director { get; }

        // IScene infection/difficulty properties driven by simulation round
        public float InfectionThresholdPercent { get; }
        public int InfectionSpreadRate { get; }
        public int SeederOffscreenSpeedFactor { get; }
        public float LocalInfectionSpreadDelaySec { get; }
        public float LocalInfectionSpreadRadius { get; }
        public float MotherShipSmallAggression { get; }
        public float MotherShipMediumAggression { get; }
        public float MotherShipLargeAggression { get; }

        public SceneSimulation()
        {
            _simulationRound = GameState.GamePlayState.SimulationRound;

            _biome = ResolveBiome(_simulationRound);
            SceneBiome = _biome;

            string[] musicCycle = { "music_flight", "music_battle", "music_kanpai", "music_dontstop" };
            SceneMusic = musicCycle[_simulationRound % musicCycle.Length];

            // Scale enemies with round: base values + round increment, capped at sane maxima
            int round = _simulationRound;
            int simulationSeederBase = Math.Min(8 + round * 3, 40);
            int simulationPowerUpBase = Math.Max(2, Math.Min(2 + round, 8));
            _seeders = simulationSeederBase + simulationPowerUpBase;
            _drones = Math.Min(12 + round * 2, 40);
            _bombers = Math.Min(5 + round, 15);

            // Use large mothership from round 2 onwards, toggle between medium/large
            _useLargeMotherShip = round >= 2;

            // Keep simulation aligned with the campaign balance: Seeders matter,
            // but the bio-limit must stay playable enough for the player to reach them.
            _infectionThreshold = Math.Max(10.0f, 14.0f - round * 0.5f);
            _infectionSpreadRate = Math.Min(4 + round, 10);
            _offscreenSpeedFactor = Math.Min(16 + round * 1, 30);
            _spreadDelaySec = Math.Max(1.2f, 2.0f - round * 0.10f);
            _spreadRadius = Math.Min(5500f + round * 150f, 6500f);
            _motherShipAggression = Math.Min(1.20f + round * 0.08f, 2.2f);

            // Assign to interface properties
            InfectionThresholdPercent = _infectionThreshold;
            InfectionSpreadRate = _infectionSpreadRate;
            SeederOffscreenSpeedFactor = _offscreenSpeedFactor;
            LocalInfectionSpreadDelaySec = _spreadDelaySec;
            LocalInfectionSpreadRadius = _spreadRadius;
            MotherShipSmallAggression = _motherShipAggression;
            MotherShipMediumAggression = _motherShipAggression;
            MotherShipLargeAggression = _motherShipAggression;

            Director = new SceneSimulationDirector();
        }

        private static SceneBiomeTypes ResolveBiome(int simulationRound)
        {
            int index = simulationRound % BiomeCycle.Length;
            if (index < 0)
                index += BiomeCycle.Length;

            return BiomeCycle[index];
        }

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            var ship = Ship.CreateShip(Surface);
            Surface.Create2DMap(30000, 15000, GameMode, null);
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

            if (_biome == SceneBiomeTypes.Winter)
                SpawnSeals(world);
            else
                SpawnJumpingFish(world);

            // Zeppelin Bombers
            for (int b = 0; b < _bombers; b++)
            {
                var rmd = new Random();
                var bomber = ZeppelinBomber.CreateZeppelinBomber(Surface);
                bomber.Rotation = new Vector3 { };
                bomber.WorldPosition = new Vector3 { x = (95700 + rmd.Next(-40000, 40000)) * ws, y = 0, z = (92000 + rmd.Next(-40000, 40000)) * ws };
                bomber.ObjectOffsets = new Vector3 { x = 0, y = -50, z = 400 };
                bomber.ObjectName = "ZeppelinBomber";
                bomber.Movement = new ZeppelinBomberControls();
                bomber.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.ZeppelinBomberHealth };
                bomber.CrashBoxDebugMode = false;
                bomber.IsActive = true;
                world.WorldInhabitants.Add(bomber);
                GameState.SurfaceState.AiObjects.Add(bomber);
            }

            // Kamikaze Drones — inactive until Decoy unlocked
            for (int i = 0; i < _drones; i++)
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
                totalSeederCount: _seeders,
                regularSeed: 9001 + (_simulationRound * 17),
                nearSeederCount: Math.Max(1, _seeders / 2));

            // MotherShip — Large from round 2+, Medium for rounds 0-1
            if (_useLargeMotherShip)
            {
                var motherShipLarge = MotherShipLarge.CreateMotherShipLarge(Surface);
                motherShipLarge.Rotation = new Vector3 { };
                motherShipLarge.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 88000 * ws };
                motherShipLarge.ObjectOffsets = new Vector3 { x = 0, y = -1500, z = 400 };
                motherShipLarge.ObjectName = "MotherShipLarge";
                motherShipLarge.Movement = new MotherShipLargeControls();
                var lazer = Lazer.CreateLazer(Surface, scaleMultiplier: 2.5f);
                lazer.CrashBoxDebugMode = false;
                motherShipLarge.WeaponSystems = new Weapons(new List<I3dObject> { lazer }, motherShipLarge.Movement!, (_3dObject)motherShipLarge)
                {
                    ShowAimAssist = false,
                    FireAsEnemyWeapon = true,
                    EnemyLazerName = "EnemyLazerLarge"
                };
                motherShipLarge.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShipLarge.ObjectName, _motherShipAggression) };
                motherShipLarge.CrashBoxDebugMode = false;
                motherShipLarge.HasPowerUp = false;
                motherShipLarge.IsActive = false;
                world.WorldInhabitants.Add(motherShipLarge);
                GameState.SurfaceState.AiObjects.Add(motherShipLarge);
            }
            else
            {
                var motherShipMedium = MotherShipMedium.CreateMotherShipMedium(Surface);
                motherShipMedium.Rotation = new Vector3 { };
                motherShipMedium.WorldPosition = new Vector3 { x = 95700 * ws, y = 0, z = 90000 * ws };
                motherShipMedium.ObjectOffsets = new Vector3 { x = 0, y = -1500, z = 400 };
                motherShipMedium.ObjectName = "MotherShipMedium";
                motherShipMedium.Movement = new MotherShipMediumControls();
                var lazer = Lazer.CreateLazer(
                    Surface,
                    scaleMultiplier: 2.0f,
                    crashBoxScaleMultiplier: WeaponSetup.EnemyLazerMediumCrashBoxScale);
                lazer.CrashBoxDebugMode = false;
                motherShipMedium.WeaponSystems = new Weapons(new List<I3dObject> { lazer }, motherShipMedium.Movement!, (_3dObject)motherShipMedium)
                {
                    ShowAimAssist = false,
                    FireAsEnemyWeapon = true,
                    EnemyLazerName = "EnemyLazerMedium"
                };
                motherShipMedium.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShipMedium.ObjectName, _motherShipAggression) };
                motherShipMedium.CrashBoxDebugMode = false;
                motherShipMedium.HasPowerUp = false;
                motherShipMedium.IsActive = false;
                world.WorldInhabitants.Add(motherShipMedium);
                GameState.SurfaceState.AiObjects.Add(motherShipMedium);
            }

            // SpaceSwans — passive wildlife
            for (int s = 0; s < 50; s++)
            {
                var rmd = new Random();
                var spaceSwan = SpaceSwan.CreateSpaceSwan(Surface);
                spaceSwan.Rotation = new Vector3 { };
                spaceSwan.WorldPosition = new Vector3 { x = (95700 + rmd.Next(-40000, 40000)) * ws, y = 0, z = (92000 + rmd.Next(-40000, 40000)) * ws };
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

            AddBiomeWeather(world);
            AddBiomeLandmarks(world);

            if (_biome == SceneBiomeTypes.Winter)
                SpawnPolarBears(world);
        }

        private void AddBiomeWeather(I3dWorld world)
        {
            if (_biome == SceneBiomeTypes.Winter)
            {
                world.WorldInhabitants.Add(SnowEmitter.CreateSnowEmitter(Surface));
                return;
            }

            if (_biome == SceneBiomeTypes.Rainforrest)
            {
                world.WorldInhabitants.Add(RainEmitter.CreateRainEmitter(Surface));
                world.WorldInhabitants.Add(LightningEmitter.CreateLightningEmitter(Surface));
            }
        }

        private void AddBiomeLandmarks(I3dWorld world)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            var towerPlacements = SurfaceGeneration.FindTowerPlacements(map, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight());
            SurfaceGeneration.FlattenTerrainAroundTowers_ToHighlands(
                map,
                Surface.MaxHeight(),
                towerPlacements,
                writeDebugLogs: false);

            AddBiomeTowers(world, towerPlacements);

            if (_biome == SceneBiomeTypes.Rainforrest)
            {
                AddRainforestLandmarks(world);
                return;
            }

            if (_biome == SceneBiomeTypes.Winter)
            {
                AddWinterLandmarks(world);
                return;
            }

            world.WorldInhabitants.Add(LeafEmitter.CreateLeafEmitter(Surface));
            AddTreeAndHouseLandmarks(world, towerPlacements);
        }

        private void AddBiomeTowers(I3dWorld world, List<(int x, int y, int height)> towerPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            bool useSnowTower = _biome == SceneBiomeTypes.Winter;
            foreach (var towerPlacement in towerPlacements)
            {
                var tower = useSnowTower
                    ? SnowTower.CreateSnowTower(Surface)
                    : Tower.CreateTower(Surface);

                tower.Rotation = new Vector3 { };
                tower.WorldPosition = new Vector3 { };
                tower.SurfaceBasedId = map[towerPlacement.y, towerPlacement.x].mapId;
                map[towerPlacement.y, towerPlacement.x].hasLandbasedObject = true;
                tower.ObjectOffsets = new Vector3
                {
                    x = 75 * ScreenSetup.ScreenScaleX,
                    y = useSnowTower
                        ? LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled
                        : LandBasedObjectSetup.NudgedSurfaceFootprintOffsetYScaled,
                    z = 400
                };
                tower.ObjectName = useSnowTower ? "SnowTower" : "Tower";
                tower.Movement = new TowerControls();
                tower.CrashBoxDebugMode = false;
                tower.ImpactStatus = new ImpactStatus { };
                world.WorldInhabitants.Add(tower);
            }
        }

        private void AddTreeAndHouseLandmarks(I3dWorld world, List<(int x, int y, int height)> towerPlacements)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            var treePlacements = SurfaceGeneration.FindTreePlacementAreas(map, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), treePlacements, radius: 1);
            foreach (var treePlacement in treePlacements)
            {
                var tree = Tree.CreateTree(Surface);
                tree.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                tree.SurfaceBasedId = map[treePlacement.y, treePlacement.x].mapId;
                map[treePlacement.y, treePlacement.x].hasLandbasedObject = true;
                tree.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                tree.ObjectName = "Tree";
                tree.Movement = new TreeControls();
                tree.ImpactStatus = new ImpactStatus { };
                tree.CrashBoxDebugMode = false;
                if (tree.SurfaceBasedId > 0) world.WorldInhabitants.Add(tree);
            }

            var housePlacements = SurfaceGeneration.FindHousePlacementAreas(map, Surface.GlobalMapSize(), Surface.MaxHeight(), treePlacements, 15000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), housePlacements, radius: 1);
            foreach (var housePlacement in housePlacements)
            {
                var house = House.CreateHouse(Surface);
                house.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                house.SurfaceBasedId = map[housePlacement.y, housePlacement.x].mapId;
                map[housePlacement.y, housePlacement.x].hasLandbasedObject = true;
                house.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                house.ObjectName = "House";
                house.Movement = new HouseControls();
                house.ImpactStatus = new ImpactStatus { };
                house.CrashBoxDebugMode = false;
                if (house.SurfaceBasedId > 0) world.WorldInhabitants.Add(house);
            }

            LeafTreePlacementHelpers.AddLeafTrees(
                world,
                Surface,
                map,
                Surface.GlobalMapSize(),
                Surface.TileSize(),
                Surface.MaxHeight(),
                LeafTreePlacementMax,
                NearPlatformLeafTreeTarget,
                NearPlatformLeafTreeSearchRadius,
                treeOffsetX: 75 * ScreenSetup.ScreenScaleX,
                treeOffsetY: LandBasedObjectSetup.SurfaceFootprintOffsetYScaled,
                towerPlacements,
                treePlacements,
                housePlacements);
        }

        private void AddRainforestLandmarks(I3dWorld world)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            var palmPlacements = SurfaceGeneration.FindTreePlacementAreas(map, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), palmPlacements, radius: 1);
            int palmIndex = 0;
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
                plant.SurfaceBasedId = map[palmPlacement.y, palmPlacement.x].mapId;
                map[palmPlacement.y, palmPlacement.x].hasLandbasedObject = true;
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

            var hutPlacements = SurfaceGeneration.FindHousePlacementAreas(map, Surface.GlobalMapSize(), Surface.MaxHeight(), palmPlacements, 15000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), hutPlacements, radius: 1);
            foreach (var hutPlacement in hutPlacements)
            {
                if (map[hutPlacement.y, hutPlacement.x].hasLandbasedObject)
                    continue;

                var hut = BambooHut.CreateBambooHut(Surface);
                hut.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                hut.SurfaceBasedId = map[hutPlacement.y, hutPlacement.x].mapId;
                map[hutPlacement.y, hutPlacement.x].hasLandbasedObject = true;
                hut.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                hut.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 0 };
                hut.ObjectName = "BambooHut";
                hut.Movement = new BambooHutControls();
                hut.ImpactStatus = new ImpactStatus { };
                hut.CrashBoxDebugMode = false;
                if (hut.SurfaceBasedId > 0) world.WorldInhabitants.Add(hut);
            }
        }

        private void AddWinterLandmarks(I3dWorld world)
        {
            var map = GameState.SurfaceState.Global2DMap;
            if (map == null)
                return;

            var treePlacements = SurfaceGeneration.FindTreePlacementAreas(map, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight(), 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), treePlacements, radius: 1);
            foreach (var treePlacement in treePlacements)
            {
                var tree = Tree.CreateTree(Surface);
                _3dObjectHelpers.ApplyScaleToObject(tree, LandBasedObjectSetup.WinterSurfaceObjectScale);
                tree.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                tree.SurfaceBasedId = map[treePlacement.y, treePlacement.x].mapId;
                map[treePlacement.y, treePlacement.x].hasLandbasedObject = true;
                tree.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
                tree.ObjectName = "Tree";
                tree.Movement = new TreeControls();
                tree.ImpactStatus = new ImpactStatus { };
                tree.CrashBoxDebugMode = false;
                if (tree.SurfaceBasedId > 0) world.WorldInhabitants.Add(tree);
            }

            var iglooPlacements = SurfaceGeneration.FindHousePlacementAreas(map, Surface.GlobalMapSize(), Surface.MaxHeight(), treePlacements, 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(map, Surface.MaxHeight(), iglooPlacements, radius: 1);
            var iglooRotationVariants = new float[] { -30f, -18f, -8f, 0f, 12f, 24f, 36f };
            int iglooIndex = 0;
            foreach (var iglooPlacement in iglooPlacements)
            {
                if (map[iglooPlacement.y, iglooPlacement.x].hasLandbasedObject)
                    continue;

                bool useLargeIgloo = iglooIndex % 4 == 0;
                var igloo = useLargeIgloo
                    ? Igloo.CreateLargeIgloo(Surface)
                    : Igloo.CreateSmallIgloo(Surface);

                float rotationZ = iglooRotationVariants[iglooIndex % iglooRotationVariants.Length];
                igloo.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                igloo.Rotation = new Vector3 { x = 0, y = 0, z = rotationZ };
                igloo.SurfaceBasedId = map[iglooPlacement.y, iglooPlacement.x].mapId;
                map[iglooPlacement.y, iglooPlacement.x].hasLandbasedObject = true;
                igloo.ObjectOffsets = new Vector3 { x = 75 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.WinterSurfaceFootprintOffsetYScaled, z = 400 };
                igloo.ImpactStatus = new ImpactStatus { };
                igloo.CrashBoxDebugMode = false;
                if (igloo.SurfaceBasedId > 0) world.WorldInhabitants.Add(igloo);

                iglooIndex++;
            }
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Top;

            int round = _simulationRound;
            string roundLabel = round == 0 ? "INITIAL" : $"ROUND {round + 1}";
            int simulationPowerUps = SeederPlacementHelpers.GetPowerUpCountForSeeders(_seeders);
            int totalEnemies = _seeders + _drones + _bombers + 1;
            string motherShipClass = _useLargeMotherShip ? "LARGE-CLASS WAR CARRIER" : "MEDIUM-CLASS CARRIER";
            string infectionThresholdText = _infectionThreshold.ToString("0.0", CultureInfo.InvariantCulture);
            string spreadDelayText = _spreadDelaySec.ToString("0.0", CultureInfo.InvariantCulture);
            string biomeName = _biome switch
            {
                SceneBiomeTypes.Rainforrest => "JUNGLE WORLD",
                SceneBiomeTypes.Desert      => "DESERT WORLD",
                SceneBiomeTypes.Winter      => "FROZEN WORLD",
                _                           => "TEMPERATE WORLD"
            };

            o.Header = "RETROMESH // SIMULATION";
            o.Title = $"COMBAT SIMULATOR - {roundLabel}";

            o.Body =
                $"The galaxy has been cleared - but the war is not over.\n\n" +
                $"A new infection wave is imminent. Train now.\n" +
                $"Simulation type: {biomeName}\n\n" +
                $"ENEMY COUNT: {totalEnemies} units total\n" +
                $"  Seeders:          {_seeders} (incl. {simulationPowerUps} power-up carriers)\n" +
                $"  Kamikaze Drones:  {_drones}\n" +
                $"  Zeppelin Bombers: {_bombers}\n" +
                $"  MotherShip:       1 x {motherShipClass}\n\n" +
                $"Infection tolerance: {infectionThresholdText}%  |  Spread delay: {spreadDelayText}s\n" +
                "Kill Seeders first; every Seeder destroyed slows the infection cascade.\n\n" +
                "DIRECTIVE:\n" +
                "Survive. Score high. Defend your rank on the leaderboard.";

            o.Footer = "PRESS ANY KEY TO ENTER THE SIMULATION";

            o.ShowOverlay = true;
            o.AutoHide = false;
            o.AutoHideSeconds = 0f;

            o.DimStrength = 0.65f;
            o.PanelWidthRatio = 0.80f;
            o.PanelHeightRatio = 0.40f;

            o.ShowDebugOverlay = false;
        }

        public void SetupGameOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
            int round = _simulationRound;
            string roundLabel = round == 0 ? "Simulation" : $"Simulation R{round + 1}";
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", roundLabel, "", "");
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

            int sizeZ = map.GetLength(0);
            int sizeX = map.GetLength(1);
            int maxHeight = Surface.MaxHeight();
            int tileSize = Surface.TileSize();
            float baseOffsetX = 75 * ScreenSetup.ScreenScaleX;
            int mapCenterX = sizeX / 2;
            int mapCenterZ = sizeZ / 2;
            int landingAreaSize = 8;
            int landingBufferTiles = 6;
            int landingTopLeftX = Math.Max(0, mapCenterX - (landingAreaSize / 2));
            int landingTopLeftZ = Math.Max(0, mapCenterZ - (landingAreaSize / 2));

            var patrolTiles = new List<(int tileX, int tileZ, int startX, int endX)>();
            int patrolWidthTiles = 8;
            int patrolHeightTiles = 2;

            for (int z = 1; z < sizeZ - patrolHeightTiles - 1; z += patrolHeightTiles)
            {
                for (int x = 1; x < sizeX - patrolWidthTiles - 1; x += patrolWidthTiles)
                {
                    int centerX = x + (patrolWidthTiles / 2);
                    int centerZ = z + (patrolHeightTiles / 2);
                    if (IsInsideLandingPlatformOrBuffer(centerX, centerZ, landingTopLeftX, landingTopLeftZ, landingAreaSize, landingBufferTiles))
                        continue;

                    if (!TryFindDryLandTileInPatrolArea(
                            map,
                            maxHeight,
                            centerX,
                            centerZ,
                            x,
                            x + patrolWidthTiles - 1,
                            patrolHeightTiles,
                            out int tileX,
                            out int tileZ))
                        continue;

                    patrolTiles.Add((tileX, tileZ, x, x + patrolWidthTiles - 1));
                }
            }

            patrolTiles.Sort((a, b) =>
            {
                int adx = a.tileX - mapCenterX;
                int adz = a.tileZ - mapCenterZ;
                int bdx = b.tileX - mapCenterX;
                int bdz = b.tileZ - mapCenterZ;
                return ((adx * adx) + (adz * adz)).CompareTo((bdx * bdx) + (bdz * bdz));
            });

            var placedBearTiles = new List<(int tileX, int tileZ)>();
            int bearsPlaced = 0;
            for (int i = 0; i < patrolTiles.Count && bearsPlaced < TargetWinterPolarBearCount; i++)
            {
                var placement = patrolTiles[i];
                int tileX = placement.tileX;
                int tileZ = placement.tileZ;

                if (map[tileZ, tileX].hasLandbasedObject)
                    continue;

                int mapId = map[tileZ, tileX].mapId;
                if (mapId <= 0)
                    continue;

                if (!PolarBearPlacementHelpers.IsAtLeastOneScreenFromExisting(tileX, tileZ, tileSize, placedBearTiles))
                    continue;

                int centerX = (placement.startX + placement.endX) / 2;
                int tileDeltaX = tileX - centerX;
                float centerOffsetX = baseOffsetX + (tileDeltaX * tileSize);
                float halfSpan = (placement.endX - placement.startX) * 0.5f * tileSize;
                float minPathOffsetX = centerOffsetX - halfSpan;
                float maxPathOffsetX = centerOffsetX + halfSpan;

                var polarBear = PolarBear.CreatePolarBear(Surface);
                polarBear.WorldPosition = new Vector3 { };
                polarBear.SurfaceBasedId = mapId;
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
                bearsPlaced++;
            }
        }

        private static bool IsInsideLandingPlatformOrBuffer(int x, int z, int landingTopLeftX, int landingTopLeftZ, int landingSize, int buffer)
        {
            int minX = landingTopLeftX - buffer;
            int minZ = landingTopLeftZ - buffer;
            int maxX = landingTopLeftX + landingSize - 1 + buffer;
            int maxZ = landingTopLeftZ + landingSize - 1 + buffer;
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        private static bool TryFindDryLandTileInPatrolArea(
            SurfaceData[,] map,
            int maxHeight,
            int centerX,
            int centerZ,
            int startX,
            int endX,
            int patrolHeightTiles,
            out int bestTileX,
            out int bestTileZ)
        {
            int sizeZ = map.GetLength(0);
            int sizeX = map.GetLength(1);

            bestTileX = Math.Clamp(centerX, 0, sizeX - 1);
            bestTileZ = Math.Clamp(centerZ, 0, sizeZ - 1);

            int bestDistance = int.MaxValue;
            bool found = false;
            int minX = Math.Clamp(startX, 1, sizeX - 2);
            int maxX = Math.Clamp(endX, 1, sizeX - 2);
            int zSearchRadius = Math.Max(1, patrolHeightTiles);
            int minZ = Math.Clamp(centerZ - zSearchRadius, 1, sizeZ - 2);
            int maxZ = Math.Clamp(centerZ + zSearchRadius, 1, sizeZ - 2);

            for (int z = minZ; z <= maxZ; z++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    if (map[z, x].hasLandbasedObject)
                        continue;

                    if (!IsDryLandTerrain(map[z, x], maxHeight))
                        continue;

                    int dx = x - centerX;
                    int dz = z - centerZ;
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

        private static bool IsDryLandTerrain(SurfaceData tile, int maxHeight)
        {
            var terrain = GamePlayHelpers.GetTerrainType(tile.mapDepth, maxHeight);
            return terrain == GamePlayHelpers.TerrainType.Grassland ||
                   terrain == GamePlayHelpers.TerrainType.Highlands;
        }
    }
}
