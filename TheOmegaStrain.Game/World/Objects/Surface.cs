using TheOmegaStrain.Game.Helpers;
using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using static TheOmegaStrain.Domain._3dSpecificsImplementations;
using TheOmegaStrain.Common.CommonSetup;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Common.GamePlayHelpers;


namespace TheOmegaStrain.Game.World.Objects
{
    public class Surface : ISurface
    {
        private const float MainSurfaceLandingLeadY = 8f;

        public Vector3 GlobalMapRotation { get; set; } = new Vector3 { x = WorldViewSetup.SurfacePitchDegrees, y = 0, z = 0 };
        public List<ITriangleMeshWithColor> RotatedSurfaceTriangles  { get; set; }
        public Dictionary<long, ITriangleMeshWithColor> RotatedSurfaceTriangleByLandId { get; set; } = new();
        public HashSet<long?> LandBasedIds { get; set; } = new HashSet<long?>();
        private readonly List<ITriangleMeshWithColor> _surfaceTriangles = new();
        private readonly List<List<IVector3>> _viewPortCrashBoxes = new();
        private readonly List<string?> _viewPortCrashBoxNames = new();

        private const bool enableLogging = false;
        const bool debugSurfaceBasedObjects = false; // Set to true to debug surface based objects

        public int SurfaceWidth() {  return SurfaceSetup.surfaceWidth; }
        public int GlobalMapSize() { return MapSetup.globalMapSize; }
        public int ViewPortSize() { return SurfaceSetup.viewPortSize; }
        public int TileSize() { return SurfaceSetup.tileSize; }
        public int MaxHeight() { return MapSetup.maxHeight; }

        public I3dObject GetSurfaceViewPort()
        {
            var globalMapPosition = GameState.SurfaceState.GlobalMapPosition;
            var global2DMap = GameState.SurfaceState.Global2DMap!;
            int tileSize = TileSize();
            int viewPortSize = ViewPortSize();
            int maxHeight = MaxHeight();
            int rowLimit = (int)(viewPortSize / 1.5) + 2;
            int tileCount = (rowLimit - 1) * (viewPortSize - 1);
            int requiredTriangleCount = tileCount * 2;

            EnsureSurfaceTrianglePool(requiredTriangleCount);
            var surface = new _3dObject { ObjectId = GameState.ObjectIdCounter++ };
            _viewPortCrashBoxes.Clear();
            _viewPortCrashBoxNames.Clear();

            int mapSize = global2DMap.GetLength(0);
            int mapZIndex = ((int)globalMapPosition.z / tileSize) % mapSize;
            int mapXIndex = ((int)globalMapPosition.x / tileSize) % mapSize;
            if (mapZIndex < 0)
            {
                mapZIndex += mapSize;
            }

            if (mapXIndex < 0)
            {
                mapXIndex += mapSize;
            }

            var ZRemainer = globalMapPosition.z % tileSize;
            var XRemainer = globalMapPosition.x % tileSize;
            var YRemainer = globalMapPosition.y;

            var YPosition = -(tileSize * viewPortSize / 2);
            int triangleIndex = 0;
            for (int i = 1; i < rowLimit; i++)
            {
                int currentMapY = (mapZIndex + i) % mapSize;
                int nextMapY = (currentMapY + 1) % mapSize;
                YPosition += tileSize;
                var XPosition = -(tileSize * viewPortSize / 2) - tileSize;

                // Pre-compute fade factor for this row (only first 3 rows)
                float fadeFactor = i <= 3 ? i / 4f : 1f;

                for (int j = 0; j < viewPortSize - 1; j++)
                {
                    int currentMapX = (mapXIndex + j) % mapSize;
                    int nextMapX = (currentMapX + 1) % mapSize;
                    XPosition += tileSize;

                    var currentTile = global2DMap[currentMapY, currentMapX];
                    var surfaceId = currentTile.mapId;

                    // --- Build terrain from map
                    var ZPostition1 = currentTile.mapDepth;
                    var ZPostition2 = global2DMap[currentMapY, nextMapX].mapDepth;
                    var ZPostition3 = global2DMap[nextMapY, nextMapX].mapDepth;
                    var ZPostition4 = global2DMap[nextMapY, currentMapX].mapDepth;

                    // Work with RGB ints to avoid hex string parse/format roundtrips
                    TerrainPaletteHelpers.GetTerrainColorRgb(
                        (ZPostition1 + ZPostition2) / 2,
                        maxHeight,
                        GameState.SurfaceState.SceneBiome,
                        out int cr,
                        out int cg,
                        out int cb);

                    TerrainPaletteHelpers.ApplyTileColorOverrides(
                        currentTile,
                        currentMapX,
                        currentMapY,
                        maxHeight,
                        ref cr,
                        ref cg,
                        ref cb);

                    if (currentTile.hasLandbasedObject && debugSurfaceBasedObjects)
                    {
                        cr = 255; cg = 0; cb = 0;
                    }

                    // Create SurfaceCrashbox directly here if needed
                    if (currentTile.crashBox != null)
                    {
                        var box = currentTile.crashBox.Value;

                        var min = new Vector3
                        {
                            x = XPosition - XRemainer,
                            y = YPosition - ZRemainer,
                            z = -YRemainer // Sealevel synced with surface altitude
                        };

                        var max = new Vector3
                        {
                            x = XPosition + (box.width * tileSize) - XRemainer,
                            y = YPosition + (box.height * tileSize) - ZRemainer,
                            z = ResolveTerrainCrashBoxDepth(box, currentTile.mapDepth) - YRemainer
                        };

                        var crashBoxCorners = RotateTerrainCrashBox(_3dObjectHelpers.GenerateCrashBoxCorners(min, max));
                        _viewPortCrashBoxes.Add(crashBoxCorners);
                        _viewPortCrashBoxNames.Add("TerrainSurface");
                    }

                    // Fade-in: first 3 rows gradually brighten, full brightness from row 4
                    if (fadeFactor < 1f)
                    {
                        cr = (int)(cr * fadeFactor);
                        cg = (int)(cg * fadeFactor);
                        cb = (int)(cb * fadeFactor);
                    }

                    var color = $"{cr:X2}{cg:X2}{cb:X2}";

                    var triangle1 = (TriangleMeshWithColor)_surfaceTriangles[triangleIndex++];
                    triangle1.Color = color;
                    triangle1.landBasedPosition = surfaceId;
                    SetVector(triangle1.vert1, XPosition - XRemainer, YPosition - ZRemainer, ZPostition1 - YRemainer);
                    SetVector(triangle1.vert2, XPosition + tileSize - XRemainer, YPosition - ZRemainer, ZPostition2 - YRemainer);
                    SetVector(triangle1.vert3, XPosition + tileSize - XRemainer, YPosition + tileSize - ZRemainer, ZPostition3 - YRemainer);

                    var triangle2 = (TriangleMeshWithColor)_surfaceTriangles[triangleIndex++];
                    triangle2.Color = color;
                    triangle2.landBasedPosition = surfaceId;
                    SetVector(triangle2.vert1, XPosition - XRemainer, YPosition - ZRemainer, ZPostition1 - YRemainer);
                    SetVector(triangle2.vert2, XPosition + tileSize - XRemainer, YPosition + tileSize - ZRemainer, ZPostition3 - YRemainer);
                    SetVector(triangle2.vert3, XPosition - XRemainer, YPosition + tileSize - ZRemainer, ZPostition4 - YRemainer);
                }
            }

            surface.ObjectParts.Add(new _3dObjectPart { PartName = "Surface", Triangles = _surfaceTriangles, IsVisible = true });
            surface.CrashBoxes = _viewPortCrashBoxes;
            surface.CrashBoxNames = _viewPortCrashBoxNames;
            surface.CrashBoxes.AddRange(GetMainSurfaceCrashBox(YRemainer));
            surface.CrashBoxNames.Add("MainSurface");
            return surface;
        }

        private List<IVector3> RotateTerrainCrashBox(List<IVector3> crashBox)
        {
            if (GlobalMapRotation == null ||
                (GlobalMapRotation.x == 0 && GlobalMapRotation.y == 0 && GlobalMapRotation.z == 0))
                return crashBox;

            var rotatedCrashBox = new List<IVector3>(crashBox.Count);
            for (int i = 0; i < crashBox.Count; i++)
            {
                rotatedCrashBox.Add(RotateSurfacePoint((Vector3)crashBox[i], GlobalMapRotation));
            }

            return rotatedCrashBox;
        }

        private static IVector3 RotateSurfacePoint(Vector3 point, Vector3 rotation)
        {
            var rotatedPoint = RotatePoint(rotation.z, point, 'Z');
            rotatedPoint = RotatePoint(rotation.y, rotatedPoint, 'Y');
            return RotatePoint(rotation.x, rotatedPoint, 'X');
        }

        private static Vector3 RotatePoint(float angleInDegrees, Vector3 point, char axis)
        {
            double radians = Math.PI * angleInDegrees / 180.0;
            float cosRes = (float)Math.Cos(radians);
            float sinRes = (float)Math.Sin(radians);

            return axis switch
            {
                'X' => new Vector3
                {
                    x = point.x,
                    y = (point.y * cosRes) - (point.z * sinRes),
                    z = (point.z * cosRes) + (point.y * sinRes)
                },
                'Y' => new Vector3
                {
                    x = (point.x * cosRes) + (point.z * sinRes),
                    y = point.y,
                    z = (point.z * cosRes) - (point.x * sinRes)
                },
                'Z' => new Vector3
                {
                    x = (point.x * cosRes) - (point.y * sinRes),
                    y = (point.y * cosRes) + (point.x * sinRes),
                    z = point.z
                },
                _ => point
            };
        }

        private static int ResolveTerrainCrashBoxDepth(SurfaceData.CrashBoxData box, int anchorDepth)
        {
            if (box.boxDepth > 0)
                return box.boxDepth;

            return 40 + anchorDepth;
        }

        private void EnsureSurfaceTrianglePool(int requiredTriangleCount)
        {
            while (_surfaceTriangles.Count < requiredTriangleCount)
            {
                _surfaceTriangles.Add(new TriangleMeshWithColor
                {
                    vert1 = new Vector3(),
                    vert2 = new Vector3(),
                    vert3 = new Vector3()
                });
            }

            if (_surfaceTriangles.Count > requiredTriangleCount)
            {
                _surfaceTriangles.RemoveRange(requiredTriangleCount, _surfaceTriangles.Count - requiredTriangleCount);
            }
        }

        private static void SetVector(IVector3 vector, float x, float y, float z)
        {
            vector.x = x;
            vector.y = y;
            vector.z = z;
        }


        private List<List<IVector3>> GetMainSurfaceCrashBox(float surfaceYOffset)
        {
            //var min = new Vector3 { x = -1200, y = -600, z = -1000 };
            //var max = new Vector3 { x = 1200, y = 1500, z = 400 };

            var min = new Vector3 { x = -500, y = -100 + surfaceYOffset - MainSurfaceLandingLeadY, z = 1000 };
            var max = new Vector3 { x = 500, y = 1000 + surfaceYOffset, z = -350 };

            return new List<List<IVector3>>
            {
                _3dObjectHelpers.GenerateCrashBoxCorners(min, max)
            };
        }

        private static string GetTileColorGradient(int height, int maxHeight)
        {
            GetTileColorGradientRgb(height, maxHeight, out int r, out int g, out int b);
            return $"{r:X2}{g:X2}{b:X2}";
        }

        private static bool IsInfectableTerrain(int height, int maxHeight)
        {
            return TerrainPaletteHelpers.IsInfectableTerrain(height, maxHeight);
        }

        private static void GetTileColorGradientRgb(int height, int maxHeight, out int red, out int green, out int blue)
        {
            TerrainPaletteHelpers.GetTerrainColorRgb(
                height,
                maxHeight,
                GameState.SurfaceState.SceneBiome,
                out red,
                out green,
                out blue);
        }

        public void Create2DMap(int? maxTrees, int? maxHouses, GameModes gameMode,string? surfaceFile)
        {
            // All scene files live under the SceneFiles folder
            const string sceneFolder = "SceneFiles";
            var sceneFilePath = !string.IsNullOrWhiteSpace(surfaceFile)
                ? Path.Combine(sceneFolder, surfaceFile)
                : null;

            GameState.SurfaceState.SurfaceFilePath = sceneFilePath;
            // ------------------------------------------------------------
            // LIVE
            // ------------------------------------------------------------
            if (gameMode == GameModes.Live)
            {
                GameState.SurfaceState.Global2DMap =
                    SurfaceGeneration.ReturnPseudoRandomMap(
                        MapSetup.globalMapSize,
                        maxHeight: out MapSetup.maxHeight,
                        maxTrees,
                        maxHouses);

                GameState.SurfaceState.SurfaceHash = 0; // not replay eligible
            }

            // ------------------------------------------------------------
            // PLAYBACK
            // ------------------------------------------------------------
            else if (gameMode == GameModes.Playback)
            {
                if (GameplayHelpers.SurfaceIO.SurfaceIO.TryLoad(
                        sceneFilePath,
                        out var loadedMap,
                        out var hash))
                {
                    GameState.SurfaceState.Global2DMap = loadedMap;
                    GameState.SurfaceState.SurfaceHash = hash;
                    MapSetup.maxHeight = GetActualMaxHeight(loadedMap);
                }
                else
                {
                    // Fail-safe: generate instead and effectively disable replay
                    GameState.SurfaceState.Global2DMap =
                        SurfaceGeneration.ReturnPseudoRandomMap(
                            MapSetup.globalMapSize,
                            maxHeight: out MapSetup.maxHeight,
                            maxTrees,
                            maxHouses);

                    GameState.SurfaceState.SurfaceHash = 0;
                }
            }

            // ------------------------------------------------------------
            // RECORD — save with date_time stamp
            // ------------------------------------------------------------
            else if (gameMode == GameModes.Record)
            {
                 GameState.SurfaceState.Global2DMap =
                    SurfaceGeneration.ReturnPseudoRandomMap(
                        MapSetup.globalMapSize,
                        maxHeight: out MapSetup.maxHeight,
                        maxTrees,
                        maxHouses);

                // Build timestamped filename: e.g. SceneFiles\Scene1SurfaceRecording_20250615_143022.retro
                var baseName = Path.GetFileNameWithoutExtension(surfaceFile ?? "Recording");
                var ext = Path.GetExtension(surfaceFile ?? ".retro");
                if (string.IsNullOrEmpty(ext)) ext = ".retro";
                var timestamped = $"{baseName}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}";
                var recordPath = Path.Combine(sceneFolder, timestamped);

                // Ensure SceneFiles folder exists in output directory
                Directory.CreateDirectory(sceneFolder);

                var hash = GameplayHelpers.SurfaceIO.SurfaceIO.Save(
                    recordPath,
                    GameState.SurfaceState.Global2DMap);

                GameState.SurfaceState.SurfaceFilePath = recordPath;
                GameState.SurfaceState.SurfaceHash = hash;
            }
            // ------------------------------------------------------------
            // Always build bitmap from surface
            // ------------------------------------------------------------
            //We need to generate Ecomaps if they are not generated already, as they are used for AI
            if (GameState.SurfaceState.ScreenEcoMetas[0,0].BioTiles==null)
            {
                GameState.SurfaceState.ScreenEcoMetas = SurfaceGeneration.GenerateEcoMap(
                    GameState.SurfaceState.Global2DMap);
            }

            // Playback and saved-game restores can arrive with an old TotalBioTiles value.
            // Always derive the denominator from the currently loaded map.
            int totalBio = 0;
            var ecoMetas = GameState.SurfaceState.ScreenEcoMetas;
            for (int sy = 0; sy < ecoMetas.GetLength(0); sy++)
                for (int sx = 0; sx < ecoMetas.GetLength(1); sx++)
                    totalBio += ecoMetas[sy, sx].BioTileCount;
            GameState.GamePlayState.TotalBioTiles = totalBio;
            if (Logger.ShouldLog(enableLogging)) Logger.Log($"[Surface] TotalBioTiles computed from EcoMap: {totalBio} (mode={gameMode})", "Surface");

            int fishPriorityTileX = (int)(GameState.SurfaceState.GlobalMapPosition.x / TileSize());
            int fishPriorityTileZ = (int)(GameState.SurfaceState.GlobalMapPosition.z / TileSize());
            GameState.SurfaceState.FishJumpAreas = SurfaceGeneration.FindFishJumpAreas(
                GameState.SurfaceState.Global2DMap,
                MapSetup.maxHeight,
                minWidthTiles: 6,
                minHeightTiles: 2,
                maxAreas: 100,
                priorityTileX: fishPriorityTileX,
                priorityTileZ: fishPriorityTileZ);

            if (Logger.ShouldLog(enableLogging)) Logger.Log($"[Surface] Create2DMap complete: mode={gameMode} TotalBioTiles={GameState.GamePlayState.TotalBioTiles} maxHeight={MapSetup.maxHeight} InfectionCriticalMass={GameState.GamePlayState.InfectionCriticalMass}", "Surface");

            int mapSize = GameState.SurfaceState.Global2DMap.GetLength(0); // siden kartet er square
            MapHelpers.UpdateTerrainMapPixels(
                GameState.SurfaceState.Global2DMap,
                mapSize,
                MapSetup.maxHeight,
                enableLogging);
        }

        private static int GetActualMaxHeight(SurfaceData[,] map)
        {
            int sx = map.GetLength(0), sy = map.GetLength(1), m = 0;
            for (int i = 0; i < sx; i++)
                for (int j = 0; j < sy; j++)
                    if (map[i, j].mapDepth > m) m = map[i, j].mapDepth;
            return m;
        }

        private static string DarkenHexColor(string color, float factor)
        {
            if (string.IsNullOrWhiteSpace(color) || color.Length < 6)
            {
                return color;
            }

            factor = Math.Clamp(factor, 0f, 1f);
            color = color.TrimStart('#');

            int red = Convert.ToInt32(color.Substring(0, 2), 16);
            int green = Convert.ToInt32(color.Substring(2, 2), 16);
            int blue = Convert.ToInt32(color.Substring(4, 2), 16);

            red = (int)(red * factor);
            green = (int)(green * factor);
            blue = (int)(blue * factor);

            return $"{Math.Clamp(red, 0, 255):X2}{Math.Clamp(green, 0, 255):X2}{Math.Clamp(blue, 0, 255):X2}";
        }

        private static float GetDistanceSquared(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x;
            float dy = a.y - b.y;
            float dz = a.z - b.z;
            return (dx * dx) + (dy * dy) + (dz * dz);
        }

        private static Vector3 Midpoint(Vector3 a, Vector3 b)
        {
            return new Vector3
            {
                x = (a.x + b.x) * 0.5f,
                y = (a.y + b.y) * 0.5f,
                z = (a.z + b.z) * 0.5f
            };
        }

    }
}
