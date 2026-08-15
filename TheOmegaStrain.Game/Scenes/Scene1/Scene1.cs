using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Game.World.Objects;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.CommonGlobalState.States;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;
using TheOmegaStrain.Gameplay.Controls.KamikazeDroneControls;
using TheOmegaStrain.Gameplay.Controls.MotherShipSmallControls;
using TheOmegaStrain.Gameplay.Controls.SpaceSwanControls;
using TheOmegaStrain.Gameplay.Controls.JumpingFishControls;
using System;
using System.Collections.Generic;
using System.Runtime.Intrinsics.Arm;

namespace TheOmegaStrain.Game.Scenes.Scene1
{
    public class Scene1 : IScene
    {
        Surface Surface = new();
        private const int LeafTreePlacementMax = 12000;
        private const int NearPlatformLeafTreeTarget = 14;
        private const int NearPlatformLeafTreeSearchRadius = 26;

        public string SceneMusic { get; } = "music_flight";
        public SceneTypes SceneType { get; } = SceneTypes.Game;
        public SceneBiomeTypes SceneBiome { get; } = SceneBiomeTypes.HillsWoods;
        public ISceneDirector Director { get; } = new Scene1Director();

        public GameModes GameMode { get; } = GameModes.Playback;
        //How much of the surface needs to be infected for the player to lose, as a percentage of total bio tiles
        public float InfectionThresholdPercent { get; } = 14.0f;
        //How many new tiles does each infected tile infect per second, on average? This is used to calculate the local spread delay and the infection progress bar fill rate
        public int InfectionSpreadRate { get; } = 4;
        //When seeders are offscreen, they will move at this speed factor (multiplier to normal speed) to catch up to the player faster. This is used to keep the gameplay engaging and prevent players from kiting seeders indefinitely by staying at the edge of the screen
        public int SeederOffscreenSpeedFactor { get; } = 10;
        //When a tile is infected, it will spread the infection to its neighbors after this delay (in seconds). The delay is calculated based on the InfectionSpreadRate, and determines how quickly the infection spreads across the surface. A lower value means faster spread, while a higher value means slower spread.
        public float LocalInfectionSpreadDelaySec { get; } = 8.0f;
        //Killing a seeder will stop the cascade of infections from spreading to its neighbors. If there is a seeder within this radius the infection will go on until it is killed
        public float LocalInfectionSpreadRadius { get; } = 4000f;
        public float MotherShipSmallAggression { get; } = 0.90f;

        public void SetupScene(I3dWorld world)
        {
            var ws = SurfaceSetup.WorldScale;

            //Add ship as first inhabitant
            var ship = Ship.CreateShip(Surface);
            //Generate 2D map for the surface, maxtrees and maxhouses set
            Surface.Create2DMap(30000,15000, GameMode, "Scene1SurfaceRecording.retro");
            var weapons = new List<I3dObject> { Lazer.CreateLazer(Surface), Bullet.CreateBullet(Surface) };
            ship.Rotation = new Vector3 { };
            ship.WorldPosition = new Vector3 { };
            ship.ObjectName = "Ship";
            ship.ImpactStatus = new ImpactStatus { ObjectHealth = ShipSetup.DefaultShipHealth };
            ship.CrashBoxDebugMode = false;
            ship.WeaponSystems = new Weapons(weapons, ship.Movement!, ship);
            world.WorldInhabitants.Add(ship);

            // Guidance arrow — on-screen indicator pointing toward closest seeder
            var guidanceArrow = SeederGuidanceArrow.CreateSeederGuidanceArrow(Surface);
            guidanceArrow.ObjectOffsets = new Vector3 { x = 0, y = -200, z = 200 };
            guidanceArrow.Rotation = new Vector3 { x = WorldViewSetup.SurfaceFacingObjectPitchDegrees, y = 0, z = 90 };
            guidanceArrow.WorldPosition = new Vector3 { };
            guidanceArrow.ObjectName = "SeederGuidanceArrow";
            guidanceArrow.ImpactStatus = new ImpactStatus { };
            guidanceArrow.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(guidanceArrow);

            SpawnJumpingFish(world);

            //Add drones that will be waiting until the player has a Decoy powerup
            for (int i = 0; i < 4; i++)
            {
                var rmd = new Random();

                //Add Kamikaze drones
                var kamikaze = KamikazeDrone.CreateKamikazeDrone(Surface);
                kamikaze.WorldPosition = new Vector3 { x = (95700 + rmd.Next(-25000, 25000)) * ws, y = 0, z = (92000 + rmd.Next(-25000, 25000)) * ws };
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
                totalSeederCount: 7,
                regularSeed: 1011,
                nearSeederCount: 4);

            //Mothership for this Scene — spawns inactive, enters when all seeders are destroyed
            var motherShip = MotherShipSmall.CreateMotherShipSmall(Surface);
            motherShip.Rotation = new Vector3 { };
            motherShip.WorldPosition = new Vector3 { x = 95100 * ws, y = 0, z = 93700 * ws };
            motherShip.ObjectOffsets = new Vector3 { x = 0, y = -2500, z = 400 };
            motherShip.ObjectName = "MotherShipSmall";
            motherShip.Movement = new MotherShipSmallControls();
            motherShip.CrashBoxDebugMode = false;
            motherShip.ImpactStatus = new ImpactStatus { ObjectHealth = EnemySetup.GetMotherShipHealth(motherShip.ObjectName, MotherShipSmallAggression) };
            motherShip.HasPowerUp = false;
            motherShip.IsActive = false;
            motherShip.CrashBoxDebugMode = false;
            world.WorldInhabitants.Add(motherShip);
            GameState.SurfaceState.AiObjects.Add(motherShip);

            // SpaceSwans — passive wildlife, 50 points each, don't block scene progression
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

            //Get the surface viewport based on the global Map Position
            //Important: In a Scene, Surface should be amongst the first objects added to the world
            var surfaceObject = (OmegaObject3D)Surface.GetSurfaceViewPort();
            surfaceObject.ObjectName = "Surface";
            //This position and rotation is for the onscreen object, not the map position
            surfaceObject.ObjectOffsets = new Vector3 { x = 70 * ScreenSetup.ScreenScaleX, y = 500 * ScreenSetup.ScreenScaleY, z = 400 };
            surfaceObject.Rotation = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 };
            surfaceObject.WorldPosition = new Vector3 { };
            //Crashboxes are added n the GetSurfaceViewPort method
            surfaceObject.Movement = new GroundControls();
            surfaceObject.ParentSurface = Surface;
            surfaceObject.ImpactStatus = new ImpactStatus { };
            surfaceObject.CrashBoxDebugMode = false;
            surfaceObject.CrashBoxesFollowRotation = false;
            world.WorldInhabitants.Add(surfaceObject);
            GameState.SurfaceState.SurfaceViewportObject = surfaceObject;
            world.WorldInhabitants.Add(LeafEmitter.CreateLeafEmitter(Surface));

            var towerPlacements = SurfaceGeneration.FindTowerPlacements(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.TileSize(), Surface.MaxHeight());

            //For a more natural look, flatten the area around the towers
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
                //Initialize the seeder rotation
                tower.Rotation = new Vector3 { };
                tower.WorldPosition = new Vector3 { };
                tower.SurfaceBasedId = GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[towerPlacement.y, towerPlacement.x].hasLandbasedObject = true;

                //The offsets of landbased objects need to similar to that of the surface, apart from some fine tuning
                tower.ObjectOffsets = new Vector3 { x = 40 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.NudgedSurfaceFootprintOffsetYScaled, z = 400 };
                tower.ObjectName = "Tower";
                tower.Movement = new TowerControls();
                tower.CrashBoxDebugMode = false;
                tower.ImpactStatus = new ImpactStatus { };
                world.WorldInhabitants.Add(tower);
            }


            var treePlacements = SurfaceGeneration.FindTreePlacementAreas(GameState.SurfaceState.Global2DMap,Surface.GlobalMapSize(),Surface.TileSize(),Surface.MaxHeight(), 30000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), treePlacements, radius: 1);
            var treeIndex = 0;
            foreach (var treePlacement in treePlacements)
            {
                treeIndex++;
                //Debug.WriteLine($"Tree placement: {treePlacement.x} {treePlacement.y}");
                //TODO: We need design more trees
                var tree = Tree.CreateTree(Surface);
                //The tree don't need a world position, it's a surface based object
                tree.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                tree.SurfaceBasedId = GameState.SurfaceState.Global2DMap[treePlacement.y, treePlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[treePlacement.y, treePlacement.x].hasLandbasedObject = true;

                //The offsets of landbased objects need to similar to that of the surface, apart from some fine tuning
                tree.ObjectOffsets = new Vector3 { x = 40 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                tree.ObjectName = "Tree";
                tree.Movement = new TreeControls();
                tree.ImpactStatus = new ImpactStatus { };
                tree.CrashBoxDebugMode = false;
                if (tree.SurfaceBasedId>0) world.WorldInhabitants.Add(tree);
            }

            var housePlacements = SurfaceGeneration.FindHousePlacementAreas(GameState.SurfaceState.Global2DMap, Surface.GlobalMapSize(), Surface.MaxHeight(), treePlacements, 15000);
            SurfaceGeneration.FlattenTerrainAroundPlacements(GameState.SurfaceState.Global2DMap, Surface.MaxHeight(), housePlacements, radius: 1);
            foreach (var housePlacement in housePlacements)
            {
                //Debug.WriteLine($"House placement: {housePlacement.x} {housePlacement.y}");

                var house = House.CreateHouse(Surface);
                //Initialize the house rotation
                //The tree don't need a world position, it's a surface based object
                house.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
                //Find the surface based id for the tree
                house.SurfaceBasedId = GameState.SurfaceState.Global2DMap[housePlacement.y, housePlacement.x].mapId;
                GameState.SurfaceState.Global2DMap[housePlacement.y, housePlacement.x].hasLandbasedObject = true;

                house.ObjectOffsets = new Vector3 { x = 40 * ScreenSetup.ScreenScaleX, y = LandBasedObjectSetup.SurfaceFootprintOffsetYScaled, z = 400 };
                house.ObjectName = "House";
                house.Movement = new HouseControls();
                house.ImpactStatus = new ImpactStatus { };
                house.CrashBoxDebugMode = false;
                if (house.SurfaceBasedId>0) world.WorldInhabitants.Add(house);
            }

            LeafTreePlacementHelpers.AddLeafTrees(
                world,
                Surface,
                GameState.SurfaceState.Global2DMap,
                Surface.GlobalMapSize(),
                Surface.TileSize(),
                Surface.MaxHeight(),
                LeafTreePlacementMax,
                NearPlatformLeafTreeTarget,
                NearPlatformLeafTreeSearchRadius,
                treeOffsetX: 40 * ScreenSetup.ScreenScaleX,
                treeOffsetY: LandBasedObjectSetup.SurfaceFootprintOffsetYScaled,
                towerPlacements,
                treePlacements,
                housePlacements);
        }

        public void SetupGameOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            GameState.ScreenOverlayState.Type = ScreenOverlayType.Game;
            GameState.ScreenOverlayState.SetGameOverlayPreset("Header", "The Omega Strain", "", "");
            GameState.ScreenOverlayState.ShowOverlay = false;
            GameState.ScreenOverlayState.ShowDebugOverlay = false;
        }

        public void SetupSceneOverlay()
        {
            GameState.ScreenOverlayState.ResetToDefaults();
            var o = GameState.ScreenOverlayState;

            o.Type = ScreenOverlayType.Intro;
            o.Anchor = ScreenOverlayAnchor.Top;

            o.Header = "RETROMESH // BOOT SEQUENCE";
            o.Title = "PLANET NEREID - PHASE I";

            o.Body =
                "Year 2147.\n\n" +
                "Signal received from the NEREID perimeter colonies.\n" +
                "Biological anomaly confirmed. Designation: OMEGA STRAIN.\n\n" +
                "Seeder activity detected. Seven units across grassland sectors.\n" +
                "Infection is advancing fast - tolerance threshold: 14.0%.\n" +
                "Spread delay: 8 seconds. Act before the bio-layer is lost.\n\n" +
                "PRIMARY DIRECTIVE:\n" +
                "Eliminate Seeders before Critical Mass. Good luck, pilot.";

            o.Footer = "PRESS ANY KEY TO INITIATE PROTOCOL";

            // Scene intro overlay should be visible until player input
            o.ShowOverlay = true;
            o.AutoHide = false;
            o.AutoHideSeconds = 0f;

            // Cinematic / readability
            o.DimStrength = 0.60f;
            o.PanelWidthRatio = 0.74f;
            o.PanelHeightRatio = 0.34f;

            // Hide debug overlay during intro
            o.ShowDebugOverlay = false;
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
