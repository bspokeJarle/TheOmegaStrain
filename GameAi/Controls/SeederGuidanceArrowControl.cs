using CommonUtilities._3DHelpers;
using CommonUtilities.CommonGlobalState;
using Domain;
using CommonUtilities.CommonSetup;
using System;
using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace GameAiAndControls.Controls
{
    /// <summary>
    /// Controls the seeder guidance arrow.
    /// The arrow is always attached to the ship and rotates to point
    /// toward the closest living seeder. Default orientation: +X (right).
    /// </summary>
    public class SeederGuidanceArrowControl : IObjectMovement
    {
        public ITriangleMeshWithColor? StartCoordinates { get; set; }
        public ITriangleMeshWithColor? GuideCoordinates { get; set; }
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

        // Pacing safety: when the arrow first locks onto a target that is farther
        // than this many screens away, the target's WorldPosition is snapped to
        // exactly this distance in the same direction. Keeps "pure flying" time
        // between objectives bounded so players don't have to fly across the
        // entire map between actions. Snap happens once per target ObjectId.
        private const float MaxTargetDistanceInScreens = 4f;

        private readonly HashSet<int> _snappedTargetIds = new();

        private float? _preferredOffsetY;
        private DateTime _lastUpdate = DateTime.MinValue;

        public I3dObject MoveObject(I3dObject theObject, IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry)
        {
            ParentObject = theObject;

            // Arrow is a fixed on-screen object with no world position.
            // It only rotates to point toward the closest seeder.
            theObject.WorldPosition = new Vector3 { x = 0, y = 0, z = 0 };
            AnchorBelowGameOverlay(theObject);

            var now = DateTime.Now;
            if (_lastUpdate == DateTime.MinValue)
                _lastUpdate = now;

            double deltaSeconds = (now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;

            // Find closest alive seeder and compute rotation to point at it
            var closestSeederWorld = FindClosestSeederWorldPosition();
            if (closestSeederWorld != null)
            {
                var shipWorld = GetShipWorldPosition();
                var heading = Common3dObjectHelpers.GetHeadingToTarget(shipWorld, closestSeederWorld);
                TargetXrotation = heading.X;
                TargetYrotation = heading.Y;
                TargetZrotation = heading.Z;
            }
            // Smoothly rotate toward target
            float maxDelta = RotationDegreesPerSecond * (float)deltaSeconds;
            Xrotation = Common3dObjectHelpers.MoveAngleTowards(Xrotation, TargetXrotation, maxDelta);
            Yrotation = Common3dObjectHelpers.MoveAngleTowards(Yrotation, TargetYrotation, maxDelta);
            Zrotation = Common3dObjectHelpers.MoveAngleTowards(Zrotation, TargetZrotation, maxDelta);

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
        /// Finds the world position of the closest objective enemy to the ship.
        /// Priority: living seeders first; when no seeders remain, any living
        /// scene-completion enemy (mothership, active drones, zeppelin bombers).
        /// Returns null if no targets are alive.
        ///
        /// When a target is first locked onto and lies farther than
        /// MaxTargetDistanceInScreens away, its WorldPosition is snapped to that
        /// distance in the same direction so players don't have to fly across the
        /// entire map between actions. Snap happens at most once per target.
        /// </summary>
        private Vector3? FindClosestSeederWorldPosition()
        {
            var aiObjects = GameState.SurfaceState?.AiObjects;
            if (aiObjects == null || aiObjects.Count == 0)
                return null;

            var shipWorld = GetShipWorldPosition();
            float bestSeederDistSq = float.MaxValue;
            Vector3? bestSeederPos = null;
            I3dObject? bestSeederObj = null;
            float bestDroneDistSq = float.MaxValue;
            Vector3? bestDronePos = null;
            I3dObject? bestDroneObj = null;
            float bestBomberDistSq = float.MaxValue;
            Vector3? bestBomberPos = null;
            I3dObject? bestBomberObj = null;
            float bestMotherShipDistSq = float.MaxValue;
            Vector3? bestMotherShipPos = null;
            I3dObject? bestMotherShipObj = null;

            for (int i = 0; i < aiObjects.Count; i++)
            {
                var obj = aiObjects[i];
                if (!obj.IsActive) continue;
                if (obj.ImpactStatus?.HasExploded == true)
                    continue;

                bool isSeeder = obj.ObjectName == "Seeder";
                bool isDrone = obj.ObjectName == "KamikazeDrone";
                bool isBomber = obj.ObjectName == "ZeppelinBomber";
                bool isMotherShip = obj.ObjectName == "MotherShipSmall"
                    || obj.ObjectName == "MotherShipMedium"
                    || obj.ObjectName == "MotherShipLarge";
                if (!isSeeder && !isDrone && !isMotherShip && !isBomber)
                    continue;

                var pos = SurfacePositionSyncHelpers.GetGuidanceTargetWorldPosition(obj);
                if (pos == null)
                    continue;

                float dx = pos.x - shipWorld.x;
                float dz = pos.z - shipWorld.z;
                float distSq = dx * dx + dz * dz;

                if (isSeeder && distSq < bestSeederDistSq)
                {
                    bestSeederDistSq = distSq;
                    bestSeederPos = new Vector3 { x = pos.x, y = pos.y, z = pos.z };
                    bestSeederObj = obj;
                }
                else if (isMotherShip && distSq < bestMotherShipDistSq)
                {
                    bestMotherShipDistSq = distSq;
                    bestMotherShipPos = new Vector3 { x = pos.x, y = pos.y, z = pos.z };
                    bestMotherShipObj = obj;
                }
                else if (isBomber && distSq < bestBomberDistSq)
                {
                    bestBomberDistSq = distSq;
                    bestBomberPos = new Vector3 { x = pos.x, y = pos.y, z = pos.z };
                    bestBomberObj = obj;
                }
                else if (isDrone && distSq < bestDroneDistSq)
                {
                    bestDroneDistSq = distSq;
                    bestDronePos = new Vector3 { x = pos.x, y = pos.y, z = pos.z };
                    bestDroneObj = obj;
                }
            }

            // As long as seeders remain, always point to seeders.
            // After seeders are gone, point at any remaining objective enemy.
            I3dObject? winnerObj;
            Vector3? winnerPos;
            if (bestSeederObj != null)
            {
                winnerObj = bestSeederObj;
                winnerPos = bestSeederPos;
            }
            else if (bestMotherShipObj != null)
            {
                winnerObj = bestMotherShipObj;
                winnerPos = bestMotherShipPos;
            }
            else if (bestDroneObj != null)
            {
                winnerObj = bestDroneObj;
                winnerPos = bestDronePos;
            }
            else
            {
                winnerObj = bestBomberObj;
                winnerPos = bestBomberPos;
            }

            if (winnerObj != null && winnerPos != null)
            {
                winnerPos = SnapTargetIfTooFar(winnerObj, winnerPos, shipWorld);
            }

            return winnerPos;
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

        private static Vector3 GetShipWorldPosition()
        {
            if (GameState.ShipState?.ShipWorldPosition is Vector3 swp)
                return swp;

            // Fallback before ship controls have run
            var map = GameState.SurfaceState.GlobalMapPosition;
            return new Vector3 { x = map.x, y = map.y, z = map.z };
        }

        public void SetParticleGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetRearEngineGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void SetWeaponGuideCoordinates(ITriangleMeshWithColor StartCoord, ITriangleMeshWithColor GuideCoord) { }
        public void ConfigureAudio(IAudioPlayer? audioPlayer, ISoundRegistry? soundRegistry) { }
        public void ReleaseParticles(I3dObject theObject) { }
        public void Dispose() { }
    }
}
