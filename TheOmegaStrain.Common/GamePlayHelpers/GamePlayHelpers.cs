using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CommonUtilities.GamePlayHelpers
{
    public static class GamePlayHelpers
    {
        public enum TerrainType
        {
            DeepWater,
            Coast,
            Grassland,
            Highlands,
            Mountains,
            Unknown
        }
        public static TerrainType GetTerrainType(int height, int maxHeight)
        {
            if (height < maxHeight * 0.05)
                return TerrainType.DeepWater;
            else if (height < maxHeight * 0.15)
                return TerrainType.Coast;
            else if (height < maxHeight * 0.40)
                return TerrainType.Grassland;
            else if (height < maxHeight * 0.70)
                return TerrainType.Highlands;
            else if (height <= maxHeight)
                return TerrainType.Mountains;
            else
                return TerrainType.Unknown;
        }
    }
}
