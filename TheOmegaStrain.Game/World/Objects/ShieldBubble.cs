using System.Collections.Generic;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Gameplay.Controls;

namespace TheOmegaStrain.Game.World.Objects
{
    // Opaque "energy shield" bubble around the ship: a sparse faceted sphere whose
    // panels ripple outward in a traveling wave, standing in for true translucency
    // (the renderer has no alpha blending). Visible only while ShieldPoints > 0.
    // Geometry lives in ShieldBubbleControl since it is rebuilt every frame there.
    public static class ShieldBubble
    {
        public static OmegaObject3D CreateShieldBubble(ISurface? parentSurface)
        {
            var bubble = new OmegaObject3D
            {
                ObjectId = GameState.ObjectIdCounter++,
                ObjectName = "ShieldBubble",
                ObjectOffsets = new Vector3 { x = 0, y = 0, z = 0 },
                Rotation = new Vector3 { x = 0, y = 0, z = 0 },
                WorldPosition = new Vector3 { x = 0, y = 0, z = 0 },
                ParentSurface = parentSurface,
                CrashBoxes = new List<List<IVector3>>(),
                ImpactStatus = new ImpactStatus { },
                CrashBoxDebugMode = false,
                HasShadow = false,
                Movement = new ShieldBubbleControl()
            };

            bubble.ObjectParts.Add(new OmegaObjectPart3D
            {
                PartName = "ShieldBubblePanels",
                Triangles = ShieldBubbleControl.BuildPanels(0f),
                IsVisible = false
            });

            return bubble;
        }
    }
}
