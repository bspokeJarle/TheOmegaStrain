using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Domain
{
    public interface ISurface : ISurfaceGeometryCache
    {
        Vector3 GlobalMapRotation { get; set; }
        int SurfaceWidth();
        int GlobalMapSize();
        int ViewPortSize();
        int TileSize();
        int MaxHeight();
        I3dObject GetSurfaceViewPort();
        void Create2DMap(int? maxTrees, int? maxHouses, GameModes gameMode, string? recordedSurface);
    }
}
