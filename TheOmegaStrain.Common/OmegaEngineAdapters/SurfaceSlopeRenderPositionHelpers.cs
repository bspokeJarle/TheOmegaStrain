using System;
using RetroMesh.Engine;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;

namespace TheOmegaStrain.Common.OmegaEngineAdapters
{
    /// <summary>
    /// Applies Omega's Surface pitch to an airborne object's render anchor.
    /// This is presentation-only: world position, object offsets, physics and
    /// geometry remain authoritative and are never changed here.
    /// </summary>
    public static class SurfaceSlopeRenderPositionHelpers
    {
        public static bool ShouldConformToSurfaceSlope(OmegaObject3D obj)
        {
            if (obj == null || obj.ParentSurface == null || obj.SurfaceBasedId > 0)
                return false;

            // Particles are separate render objects. Classify them by emitter so
            // they inherit the actor's visual anchor without moving their stored
            // offsets, collision state or simulation trajectory.
            string? actorName = obj.ObjectName == "Particle"
                ? obj.ImpactStatus?.ObjectName
                : obj.ObjectName;
            if (!IsAirborneActorName(actorName))
                return false;

            var worldPosition = obj.WorldPosition;
            return worldPosition != null
                && (worldPosition.x != 0f || worldPosition.y != 0f || worldPosition.z != 0f);
        }

        private static bool IsAirborneActorName(string? objectName)
        {
            return objectName is
                "AttackShip" or
                "Cloud" or
                "KamikazeDrone" or
                "MotherShipSmall" or
                "MotherShipMedium" or
                "MotherShipLarge" or
                "PowerUp" or
                "Seeder" or
                "SpaceSwan" or
                "ZeppelinBomber";
        }

        public static RenderPosition Apply(OmegaObject3D obj, RenderPosition position)
        {
            double correctionY = GetCorrectionY(obj, obj.WorldPosition);
            return new RenderPosition(position.X, position.Y + correctionY, position.Z);
        }

        /// <summary>
        /// Returns the emitter's render-only correction at a supplied world anchor.
        /// Particles use their own emission anchor so they stay attached without
        /// changing particle simulation or applying correction repeatedly.
        /// </summary>
        public static double GetCorrectionY(OmegaObject3D source, IVector3? worldAnchor)
        {
            if (!ShouldConformToSurfaceSlope(source) || worldAnchor == null)
                return 0d;

            float longitudinalDistance = worldAnchor.z - GameState.SurfaceState.GlobalMapPosition.z;
            return CalculateCorrectionY(
                longitudinalDistance,
                WorldViewSetup.SurfacePitchDegrees,
                OmegaWorldViewSetup.OriginalWorldPitchDegrees);
        }

        /// <summary>
        /// Returns only the pitch difference from Omega's original 70-degree
        /// presentation. That preserves the shipped calibration at High while
        /// making Normal follow the same tilted ground plane in both directions.
        /// </summary>
        public static double CalculateCorrectionY(
            float longitudinalDistance,
            float pitchDegrees,
            float calibratedPitchDegrees)
        {
            if (!float.IsFinite(longitudinalDistance)
                || !float.IsFinite(pitchDegrees)
                || !float.IsFinite(calibratedPitchDegrees))
                return 0d;

            double radians = pitchDegrees * Math.PI / 180d;
            double calibratedRadians = calibratedPitchDegrees * Math.PI / 180d;
            return longitudinalDistance * (Math.Cos(radians) - Math.Cos(calibratedRadians));
        }
    }
}
