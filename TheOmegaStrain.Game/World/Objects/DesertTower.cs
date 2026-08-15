using Domain;
using GameAiAndControls.Controls;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace _3dRotations.World.Objects
{
    public static class DesertTower
    {
        public static _3dObject CreateDesertTower(ISurface parentSurface)
        {
            var tower = Tower.CreateTower(parentSurface);
            RecolorTower(tower);
            tower.Movement = new TowerControls();
            return tower;
        }

        private static void RecolorTower(_3dObject tower)
        {
            foreach (var part in tower.ObjectParts)
            {
                if (part.Triangles == null)
                    continue;

                string? color = part.PartName switch
                {
                    "TowerBase" => "8C6A3E",
                    "TowerBaseDecals" => "3A2A1A",
                    "TowerShaft" => "9D7444",
                    "TowerHeadFrame" => "B8874C",
                    "TowerHeadGlass" => "E2B45F",
                    "TowerRoof" => "5F4328",
                    "TowerRadar" => "C0A16A",
                    _ => null
                };

                if (color == null)
                    continue;

                SetColor(part.Triangles, color);
            }
        }

        private static void SetColor(List<ITriangleMeshWithColor> triangles, string color)
        {
            foreach (var triangle in triangles)
                triangle.Color = color;
        }
    }
}
