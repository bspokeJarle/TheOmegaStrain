using TheOmegaStrain.Common.OmegaEngineAdapters;
using TheOmegaStrain.Common.CommonGlobalState;
using TheOmegaStrain.Domain;
using TheOmegaStrain.Common.CommonSetup;
using System;
using System.Collections.Generic;

namespace TheOmegaStrain.Gameplay.Controls
{
    /// <summary>
    /// Controls the powerup guidance arrow.
    /// The arrow is always attached to the ship and rotates to point
    /// toward the closest live powerup. Default orientation: +X (right).
    /// </summary>
    public class PowerUpGuidanceArrowControl : IObjectMovement
    {
        public ITriangleMeshWithColorAndTexture? StartCoordinates { get; set; }
        public ITriangleMeshWithColorAndTexture? GuideCoordinates { get; set; }
        public I3dObject ParentObject { get; set; }
        public IPhysics Physics { get; set; } = new Physics.Physics();

        // The arrow's default forward is +X. Base rotation aligns it with the camera view.
        private float Xrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float Yrotation = 0f;
        private float Zrotation = 90f;

        private float TargetXrotation = WorldViewSetup.SurfaceFacingObjectPitchDegrees;
        private float TargetYrotation = 0f;
        private float TargetZrotation = 90f;

        private const float RotationDegreesPerSecond = 1800f;

        // See SeederGuidanceArrowControl: bounds how far away a freshly-locked
        // target can appear to be, so players don't have to cross the whole map.
        private const float MaxTargetDistanceInScreens = 4f;

        private readonly HashSet<int> _snappedTargetIds = new();

        private float? _preferredOffsetY;
        private DateTime _lastUpdate = DateTime.MinValue;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            // Arrow is a fixed on-screen object with no world position.
            // It only rotates to point toward the closest powerup.
            theObject.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            AnchorBelowGameOverlay(theObject);

            var now = DateTime.Now;
            if (_lastUpdate == DateTime.MinValue)
                _lastUpdate = now;

            double deltaSeconds = (now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;

            // Find closest live powerup and compute rotation to point at it
            var closestPowerUpWorld = FindClosestPowerUpWorldPosition();
            SetObjectVisibility(theObject, closestPowerUpWorld != null);
            if (closestPowerUpWorld != null)
            {
                var shipWorld = GetShipWorldPosition();
                var heading = OmegaObjectHelpers.GetHeadingToTarget(shipWorld, closestPowerUpWorld);
                TargetXrotation = heading.X;
                TargetYrotation = heading.Y;
                TargetZrotation = heading.Z;
            }
            // Smoothly rotate toward target
            float maxDelta = RotationDegreesPerSecond * (float)deltaSeconds;
            Xrotation = OmegaObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
            Yrotation = OmegaObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
            Zrotation = OmegaObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);

            if (theObject.Rotation != null)
            {
                theObject.Rotation.x = Xrotation;
                theObject.Rotation.y = Yrotation;
                theObject.Rotation.z = Zrotation;
            }

            return theObject;
        }

        private void AnchorBelowGameOverlay(I3dObject theObject)
        {
            var offsets = theObject.ObjectOffsets;
            if (offsets == null)
            {
                offsets = new Vector3 { x = 0, y = 0, z = 0 };
                theObject.ObjectOffsets = offsets;
            }

            _preferredOffsetY ??= offsets.y;

            float preferredScreenY = ScreenSetup.screenSizeY / 2f + _preferredOffsetY.Value;
            float anchoredScreenY = GameOverlaySetup.AnchorScreenYBelowHud(preferredScreenY);
            offsets.y = anchoredScreenY - ScreenSetup.screenSizeY / 2f;
        }

        /// <summary>
        /// Finds the world position of the closest active, non-exploded PowerUp.
        /// Returns null if none are alive.
        ///
        /// When a target is first locked onto and lies farther than
        /// MaxTargetDistanceInScreens away, its WorldPosition is snapped to that
        /// distance in the same direction, matching SeederGuidanceArrowControl.
        /// </summary>
        private Vector3? FindClosestPowerUpWorldPosition()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
                return null;

            var shipWorld = GetShipWorldPosition();
            float bestDistSq = float.MaxValue;
            Vector3? bestPos = null;
            I3dObject? bestObj = null;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (!obj.IsActive) continue;
                if (obj.ImpactStatus?.HasExploded == true) continue;
                if (obj.ObjectName != "PowerUp") continue;

                var pos = SurfacePositionSyncHelpers.GetGuidanceTargetWorldPosition(obj);
                if (pos == null)
                    continue;

                float dx = pos.x - shipWorld.x;
                float dz = pos.z - shipWorld.z;
                float distSq = dx * dx + dz * dz;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestPos = new Vector3 { x = pos.x, y = pos.y, z = pos.z };
                    bestObj = obj;
                }
            }

            if (bestObj != null && bestPos != null)
                bestPos = SnapTargetIfTooFar(bestObj, bestPos, shipWorld);

            return bestPos;
        }

        /// <summary>
        /// If <paramref name="winnerObj"/> has not yet been snapped and lies
        /// farther than MaxTargetDistanceInScreens screens from the ship along
        /// the XZ plane, mutate its WorldPosition so the guidance target ends up
        /// at exactly that distance in the same direction. Returns the (possibly
        /// updated) guidance position. Records the ObjectId only when an actual
        /// snap happens, so a target first seen nearby can still be corrected if
        /// it later drifts too far away while the arrow is pointing at it.
        /// </summary>
        private Vector3 SnapTargetIfTooFar(I3dObject winnerObj, Vector3 winnerGuidancePos, Vector3 shipWorld)
        {
            if (_snappedTargetIds.Contains(winnerObj.ObjectId))
                return winnerGuidancePos;

            var worldPosition = winnerObj.WorldPosition;
            if (worldPosition == null)
                return winnerGuidancePos;

            float maxDist = MaxTargetDistanceInScreens * ScreenSetup.screenSizeX;
            float dx = winnerGuidancePos.x - shipWorld.x;
            float dz = winnerGuidancePos.z - shipWorld.z;
            float distSq = dx * dx + dz * dz;
            float maxDistSq = maxDist * maxDist;

            if (distSq <= maxDistSq)
                return winnerGuidancePos;

            float dist = MathF.Sqrt(distSq);
            if (dist <= 0f)
                return winnerGuidancePos;

            float scale = maxDist / dist;
            float newGuidanceX = shipWorld.x + dx * scale;
            float newGuidanceZ = shipWorld.z + dz * scale;

            // GetGuidanceTargetWorldPosition adds (screenSizeX/2 + ObjectOffsets.x)
            // to WorldPosition.x and uses WorldPosition.z directly, so the same
            // delta applied to WorldPosition will land the guidance position at
            // the desired XZ. y is intentionally left untouched.
            float worldDeltaX = newGuidanceX - winnerGuidancePos.x;
            float worldDeltaZ = newGuidanceZ - winnerGuidancePos.z;
            worldPosition.x += worldDeltaX;
            worldPosition.z += worldDeltaZ;
            _snappedTargetIds.Add(winnerObj.ObjectId);

            return new Vector3 { x = newGuidanceX, y = winnerGuidancePos.y, z = newGuidanceZ };
        }

        private static void SetObjectVisibility(I3dObject theObject, bool visible)
        {
            for (int i = 0; i < theObject.ObjectParts.Count; i++)
            {
                theObject.ObjectParts[i].IsVisible = visible;
            }
        }

        private static Vector3 GetShipWorldPosition()
        {
            if (GameState.ShipState?.ShipWorldPosition is Vector3 swp)
                return swp;

            // Fallback before ship controls have run
            var map = GameState.SurfaceState.GlobalMapPosition;
            return new Vector3 { x = map.x, y = map.y, z = map.z };
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColorAndTexture StartCoord, ITriangleMeshWithColorAndTexture GuideCoord) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void Dispose() { }
    }
}
