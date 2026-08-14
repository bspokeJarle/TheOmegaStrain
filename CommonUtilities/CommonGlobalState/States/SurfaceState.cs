using CommonUtilities.CommonSetup;
using Domain;
using static Domain._3dSpecificsImplementations;

namespace CommonUtilities.CommonGlobalState.States
{
    // This class holds global state information for the ship, expand as needed
    // No deep copy for this class, it's intended to be a singleton-like static holder of state
    public class SurfaceState
    {
        //Meta information for surface ecology, for AI behavior etc
        public ScreenEcoMeta[,] ScreenEcoMetas {get;set;} = new ScreenEcoMeta[MapSetup.screensPrMap, MapSetup.screensPrMap];

        public SurfaceData[,]? Global2DMap { get; set; } = new SurfaceData[MapSetup.globalMapSize, MapSetup.globalMapSize];
        public SurfaceMapPixelBuffer? GlobalMapPixels { get; set; }
        public Vector3 GlobalMapPosition { get; set; } = new Vector3 { x = SurfaceSetup.DefaultMapPosition.x, y = SurfaceSetup.DefaultMapPosition.y, z = SurfaceSetup.DefaultMapPosition.z };
        public List<_3dObject> AiObjects { get; set; } = new List<_3dObject>();
        public List<IVector3> DirtyTiles { get; set; } = new List<IVector3>();
        public List<FishJumpArea> FishJumpAreas { get; set; } = new();
        public _3dObject? SurfaceViewportObject { get; set; }
        public ulong SurfaceHash { get; set; }
        public string? SurfaceFilePath { get; set; }
        public int RecordingFps { get; set; } = ScreenSetup.targetFps;
        public SceneBiomeTypes SceneBiome { get; set; } = SceneBiomeTypes.HillsWoods;

        /// <summary>
        /// Tiles infected by seeders that are pending local spread to their neighbors.
        /// After a delay each tile spreads infection to adjacent bio tiles, then is removed.
        /// </summary>
        public List<(int tileX, int tileZ, long infectedTick)> PendingLocalInfectionSpread { get; set; } = new();
    }
}
