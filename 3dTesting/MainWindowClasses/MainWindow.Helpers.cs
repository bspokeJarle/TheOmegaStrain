using CommonUtilities.OmegaEngineAdapters;
using CommonUtilities.CommonGlobalState;
using CommonUtilities.CommonGlobalState.States;
using CommonUtilities.CommonSetup;
using CommonUtilities.GamePlayHelpers;
using Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using static Domain._3dSpecificsImplementations;

namespace _3dTesting.Helpers
{
    public static class GameHelpers
    {
        private static OmegaMeshRotation Rotate3d = new OmegaMeshRotation();
        private static bool enableLogging = false;
        /// <summary>
        /// Applies the rotation offset to prevent flipping when rotating.
        /// </summary>
        public static void ApplyRotationOffset(ref ITriangleMeshWithColor triangle, int? offsetX, int? offsetY, int? offsetZ)
        {
            MeshGeometryOperations.ApplyPositiveRotationOffsetToTriangle(triangle, offsetX, offsetY, offsetZ);
        }

        public static Vector3 ComputeCentroid(_3dObject inhabitant)
        {
            return MeshGeometryOperations.GetObjectCentroid(
                inhabitant,
                static (x, y, z) => new Vector3(x, y, z));
        }

        /// <summary>
        /// Updates the minimap overlay with the correct cropped portion.
        /// </summary>
        public static void  UpdateMapOverlay(System.Windows.Controls.Image mapOverlay, BitmapSource surfaceMapBitmap, int mapX, int mapY)
        {
            if (mapX == 0 || mapY == 0) return; // Avoid division by zero or invalid map coordinates 
            if (surfaceMapBitmap != null && mapOverlay != null)
            {
                try
                {
                    mapOverlay.Source = new CroppedBitmap(surfaceMapBitmap, new Int32Rect((mapX - MapSetup.bitmapMapCenterOffsetX) / MapSetup.tileSize, (mapY - MapSetup.bitmapMapCenterOffsetY) / MapSetup.tileSize, MapSetup.bitmapSize * 2, MapSetup.bitmapSize));
                }
                catch (Exception ex)
                {
                    if (Logger.ShouldLog(enableLogging)) Logger.Log("UpdateMapOverlay: Exception while updating map overlay " + ex.Message, "Error");
                }
            }
        }

        public static void UpdateDirtyTilesInMap(SurfaceMapPixelBuffer? surfaceMapPixels)
        {
            var state = GameState.SurfaceState;
            if (state == null) return;

            // Scene reset: pixel buffer can be null or not ready.
            if (surfaceMapPixels == null)
            {
                state.DirtyTiles?.Clear();
                return;
            }

            if (state.DirtyTiles == null || state.DirtyTiles.Count == 0)
                return;

            try
            {
                int w = surfaceMapPixels.Width;
                int h = surfaceMapPixels.Height;

                // Thread-safe snapshot + clear to prevent corruption from background thread
                List<IVector3> dirtySnapshot;
                lock (state.DirtyTiles)
                {
                    dirtySnapshot = new List<IVector3>(state.DirtyTiles);
                    state.DirtyTiles.Clear();
                }

                lock (surfaceMapPixels.SyncRoot)
                {
                    foreach (var dirty in dirtySnapshot)
                    {
                        int x = (int)dirty.x;
                        int z = (int)dirty.z;

                        if (x < 0 || z < 0 || x >= w || z >= h)
                        {
                            if (Logger.ShouldLog(enableLogging)) Logger.Log($"UpdateDirtyTilesInMap: skip OOB ({x},{z})");
                            continue;
                        }

                        if (Logger.ShouldLog(enableLogging)) Logger.Log($"UpdateDirtyTilesInMap: Infect pixel ({x},{z})");
                        int offset = z * surfaceMapPixels.Stride + x * 4;
                        surfaceMapPixels.Pixels[offset] = 0;
                        surfaceMapPixels.Pixels[offset + 1] = 0;
                        surfaceMapPixels.Pixels[offset + 2] = 255;
                        surfaceMapPixels.Pixels[offset + 3] = 255;
                    }

                    surfaceMapPixels.Version++;
                }
            }
            catch (Exception ex)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log("UpdateDirtyTilesInMap: Exception while updating dirty tiles  " + ex.Message, "Error");
                return;
            }
        }

        // -----------------------------------------------------------------
        //  MINIMAP MARKERS  (overlay approach - never touches source pixels)
        // -----------------------------------------------------------------

        private static int _markerFrame;

        /// <summary>
        /// Creates a cropped copy of the source bitmap, draws markers on the copy,
        /// and sets it as the minimap image source. The source bitmap is never modified
        /// by markers, eliminating all save/restore issues.
        /// </summary>
        public static void UpdateMapOverlayWithMarkers(
            System.Windows.Controls.Image mapOverlay,
            SurfaceMapPixelBuffer? surfaceMapPixels,
            int mapX, int mapZ)
        {
            if (mapX == 0 || mapZ == 0) return;
            if (surfaceMapPixels == null || mapOverlay == null) return;

            try
            {
                // 1. Compute crop rect (same as old UpdateMapOverlay)
                int cropX = (mapX - MapSetup.bitmapMapCenterOffsetX) / MapSetup.tileSize;
                int cropZ = (mapZ - MapSetup.bitmapMapCenterOffsetY) / MapSetup.tileSize;
                int cropW = MapSetup.bitmapSize * 2;
                int cropH = MapSetup.bitmapSize;

                int srcW = surfaceMapPixels.Width;
                int srcH = surfaceMapPixels.Height;

                cropW = Math.Min(cropW, srcW);
                cropH = Math.Min(cropH, srcH);
                if (cropW <= 0 || cropH <= 0) return;

                // 2. Copy crop pixels into a writable bitmap, wrapping around the
                // planet map instead of clamping at bitmap edges.
                int stride = cropW * 4; // Bgra32 = 4 bytes per pixel
                byte[] pixels = CopyWrappedPixels(surfaceMapPixels, cropX, cropZ, cropW, cropH, stride);

                // 3. Draw markers onto the pixel buffer (positions relative to crop)
                DrawMarkersOnBuffer(pixels, cropW, cropH, stride, cropX, cropZ, srcW, srcH);

                // 4. Write into a WriteableBitmap and set as source
                var wb = new WriteableBitmap(cropW, cropH, 96, 96,
                    System.Windows.Media.PixelFormats.Bgra32, null);
                wb.WritePixels(new Int32Rect(0, 0, cropW, cropH), pixels, stride, 0);
                wb.Freeze(); // Allow cross-thread use and better perf
                mapOverlay.Source = wb;
            }
            catch (Exception ex)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log("UpdateMapOverlayWithMarkers: " + ex.Message, "Error");
            }
        }

        /// <summary>
        /// Draws marker pixels into a raw BGRA pixel buffer.
        /// Positions are converted from full-bitmap coords to crop-relative coords.
        /// </summary>
        private static void DrawMarkersOnBuffer(
            byte[] pixels, int cropW, int cropH, int stride,
            int cropOriginX, int cropOriginZ,
            int mapWidth, int mapHeight)
        {
            int tileSize = MapSetup.tileSize;

            _markerFrame++;
            bool aiPrimaryColor = (_markerFrame % 30) < 15;
            var biome = GameState.SurfaceState.SceneBiome;

            // BGRA format
            byte[] greyPx    = { 180, 180, 180, 255 }; // Ship
            byte[] blackPx   = { 0, 0, 0, 255 };       // Seeder
            byte[] bluePx    = { 255, 80, 0, 255 };    // Drone
            byte[] orangePx  = { 0, 140, 255, 255 };   // Decoy
            byte[] powerupPx = { 255, 140, 30, 255 };  // PowerUp (strong blue)
            byte[] swanPx    = { 240, 240, 240, 255 }; // SpaceSwan (white)
            byte[] zeppelinPx = { 0, 200, 200, 255 };   // ZeppelinBomber (yellow-green, BGRA)

            // Mothership - large marker flashing red/strong-red independently
            bool mothershipFlashRed = (_markerFrame % 20) < 10;
            byte[] mothershipPx = GetMinimapMarkerBlinkBgra(
                biome,
                new byte[] { 0, 0, 255, 255 }, // BGRA red
                mothershipFlashRed);

            bool powerupPrimaryColor = (_markerFrame % 8) < 5;

            // Ship marker - always visible at viewport center
            var mapPos = GameState.SurfaceState.GlobalMapPosition;
            if (mapPos != null)
            {
                int viewportCenterOffset = (SurfaceSetup.viewPortSize * tileSize) / 2;
                int shipMapX = MapCoordinateHelpers.WorldToTileIndex(mapPos.x + viewportCenterOffset, tileSize, mapWidth);
                int shipMapZ = MapCoordinateHelpers.WorldToTileIndex(mapPos.z + viewportCenterOffset, tileSize, mapHeight);
                int shipBx = MapCoordinateHelpers.GetWrappedRelativeIndex(shipMapX, cropOriginX, mapWidth);
                int shipBz = MapCoordinateHelpers.GetWrappedRelativeIndex(shipMapZ, cropOriginZ, mapHeight);
                StampMarker(pixels, cropW, cropH, stride, shipBx, shipBz, greyPx);
            }

            // Mothership marker - always drawn with its own red/black flash cycle
            {
                var aiObjects = GameState.SurfaceState?.AiObjects;
                _3dObject[]? msSnapshot = null;
                if (aiObjects != null)
                {
                    lock (aiObjects) { msSnapshot = [.. aiObjects]; }
                }
                if (msSnapshot != null)
                {
                    for (int i = 0; i < msSnapshot.Length; i++)
                    {
                        var obj = msSnapshot[i];
                        if (obj == null || (obj.ObjectName != "MotherShipSmall" && obj.ObjectName != "MotherShipMedium")) continue;
                        if (!obj.IsActive) continue;
                        if (obj.ImpactStatus?.HasExploded == true) continue;
                        if (obj.ObjectParts == null || obj.ObjectParts.Count == 0) continue;
                        if (obj.WorldPosition == null) continue;
                        if (obj.WorldPosition.x == 0 && obj.WorldPosition.z == 0) continue;

                        var markerWorld = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj);
                        if (markerWorld == null) continue;

                        int markerX = MapCoordinateHelpers.WorldToTileIndex(markerWorld.x, tileSize, mapWidth);
                        int markerZ = MapCoordinateHelpers.WorldToTileIndex(markerWorld.z, tileSize, mapHeight);
                        int mx = MapCoordinateHelpers.GetWrappedRelativeIndex(markerX, cropOriginX, mapWidth);
                        int mz = MapCoordinateHelpers.GetWrappedRelativeIndex(markerZ, cropOriginZ, mapHeight);
                        StampMarkerBoss(pixels, cropW, cropH, stride, mx, mz, mothershipPx);
                    }
                }
            }

            // AI objects - always visible, blinking between their own marker color
            // and a stronger variant of the same color family.
            {
                var aiObjects = GameState.SurfaceState?.AiObjects;
                _3dObject[]? snapshot = null;
                if (aiObjects != null)
                {
                    lock (aiObjects) { snapshot = [.. aiObjects]; }
                }
                if (snapshot != null)
                {
                    for (int i = 0; i < snapshot.Length; i++)
                    {
                        var obj = snapshot[i];
                        if (obj == null) continue;
                        if (!obj.IsActive) continue;
                        if (obj.ImpactStatus?.HasExploded == true)
                            continue;
                        // Also skip objects whose parts have been cleared (fully dead)
                        if (obj.ObjectParts == null || obj.ObjectParts.Count == 0)
                            continue;
                        if (obj.WorldPosition == null)
                            continue;
                        if (obj.WorldPosition.x == 0 && obj.WorldPosition.z == 0)
                            continue;

                        var markerWorld = SurfacePositionSyncHelpers.GetMinimapMarkerWorldPosition(obj);
                        if (markerWorld == null)
                            continue;

                        bool isPowerUp = obj.ObjectName == "PowerUp";

                        if (isPowerUp)
                        {
                            int markerX = MapCoordinateHelpers.WorldToTileIndex(markerWorld.x, tileSize, mapWidth);
                            int markerZ = MapCoordinateHelpers.WorldToTileIndex(markerWorld.z, tileSize, mapHeight);
                            int bx = MapCoordinateHelpers.GetWrappedRelativeIndex(markerX, cropOriginX, mapWidth);
                            int bz = MapCoordinateHelpers.GetWrappedRelativeIndex(markerZ, cropOriginZ, mapHeight);
                            StampMarkerLarge(
                                pixels,
                                cropW,
                                cropH,
                                stride,
                                bx,
                                bz,
                                GetMinimapMarkerBlinkBgra(biome, powerupPx, powerupPrimaryColor));
                            continue;
                        }

                        // Mothership drawn separately below with its own flash cycle
                        if (obj.ObjectName == "MotherShipSmall" || obj.ObjectName == "MotherShipMedium") continue;

                        byte[]? color = obj.ObjectName switch
                        {
                            "Seeder" => blackPx,
                            "KamikazeDrone" => bluePx,
                            "DroneDecoy" => orangePx,
                            "SpaceSwan" => swanPx,
                            "ZeppelinBomber" => null,
                            _ => null
                        };
                        if (color == null)
                        {
                            if (obj.ObjectName == "ZeppelinBomber")
                            {
                                int markerX = MapCoordinateHelpers.WorldToTileIndex(markerWorld.x, tileSize, mapWidth);
                                int markerZ = MapCoordinateHelpers.WorldToTileIndex(markerWorld.z, tileSize, mapHeight);
                                int bxZ = MapCoordinateHelpers.GetWrappedRelativeIndex(markerX, cropOriginX, mapWidth);
                                int bzZ = MapCoordinateHelpers.GetWrappedRelativeIndex(markerZ, cropOriginZ, mapHeight);
                                StampMarkerLarge(
                                    pixels,
                                    cropW,
                                    cropH,
                                    stride,
                                    bxZ,
                                    bzZ,
                                    GetMinimapMarkerBlinkBgra(biome, zeppelinPx, aiPrimaryColor));
                            }
                            continue;
                        }

                        int markerX2 = MapCoordinateHelpers.WorldToTileIndex(markerWorld.x, tileSize, mapWidth);
                        int markerZ2 = MapCoordinateHelpers.WorldToTileIndex(markerWorld.z, tileSize, mapHeight);
                        int bx2 = MapCoordinateHelpers.GetWrappedRelativeIndex(markerX2, cropOriginX, mapWidth);
                        int bz2 = MapCoordinateHelpers.GetWrappedRelativeIndex(markerZ2, cropOriginZ, mapHeight);
                        StampMarker(pixels, cropW, cropH, stride, bx2, bz2, GetMinimapMarkerBlinkBgra(biome, color, aiPrimaryColor));
                    }
                }
            }
        }

        public static byte[] GetMinimapMarkerBlinkBgra(SceneBiomeTypes biome, byte[] primaryBgra, bool usePrimaryColor)
        {
            if (usePrimaryColor)
            {
                return primaryBgra;
            }

            return GetMinimapMarkerHighlightBgra(primaryBgra);
        }

        public static byte[] GetMinimapMarkerHighlightBgra(byte[] primaryBgra)
        {
            if (primaryBgra.Length < 3)
            {
                return new byte[] { 255, 255, 255, 255 };
            }

            int b = primaryBgra[0];
            int g = primaryBgra[1];
            int r = primaryBgra[2];
            byte a = primaryBgra.Length > 3 ? primaryBgra[3] : (byte)255;
            int max = Math.Max(b, Math.Max(g, r));
            int min = Math.Min(b, Math.Min(g, r));

            if (max < 24)
            {
                return new byte[] { 90, 90, 90, a };
            }

            if (min > 230)
            {
                return new byte[] { 210, 255, 255, a };
            }

            return new byte[]
            {
                BoostMarkerChannel(b, max),
                BoostMarkerChannel(g, max),
                BoostMarkerChannel(r, max),
                a
            };
        }

        private static byte BoostMarkerChannel(int value, int max)
        {
            if (value >= max)
                return 255;

            if (value <= 0)
                return 0;

            return (byte)Math.Min(255, value + 70);
        }

        private static byte[] CopyWrappedPixels(
            SurfaceMapPixelBuffer source,
            int cropX,
            int cropZ,
            int cropW,
            int cropH,
            int stride)
        {
            int srcW = source.Width;
            int srcH = source.Height;
            byte[] pixels = new byte[stride * cropH];

            lock (source.SyncRoot)
            {
                if (cropX >= 0 && cropZ >= 0 && cropX + cropW <= srcW && cropZ + cropH <= srcH)
                {
                    for (int z = 0; z < cropH; z++)
                    {
                        Buffer.BlockCopy(
                            source.Pixels,
                            ((cropZ + z) * source.Stride) + (cropX * 4),
                            pixels,
                            z * stride,
                            stride);
                    }

                    return pixels;
                }

                for (int z = 0; z < cropH; z++)
                {
                    int srcZ = MapCoordinateHelpers.WrapIndex(cropZ + z, srcH);
                    int targetRow = z * stride;
                    int remaining = cropW;
                    int targetX = 0;
                    int srcX = MapCoordinateHelpers.WrapIndex(cropX, srcW);

                    while (remaining > 0)
                    {
                        int width = Math.Min(remaining, srcW - srcX);
                        Buffer.BlockCopy(
                            source.Pixels,
                            (srcZ * source.Stride) + (srcX * 4),
                            pixels,
                            targetRow + (targetX * 4),
                            width * 4);

                        targetX += width;
                        remaining -= width;
                        srcX = 0;
                    }
                }

                return pixels;
            }
        }

        /// <summary>
        /// Stamps a 3x3 marker into a raw pixel buffer at the given crop-relative position.
        /// </summary>
        private static void StampMarker(byte[] pixels, int w, int h, int stride,
            int cx, int cz, byte[] bgra)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    int px = cx + dx;
                    int pz = cz + dz;
                    if (px >= 0 && pz >= 0 && px < w && pz < h)
                    {
                        int offset = pz * stride + px * 4;
                        pixels[offset]     = bgra[0]; // B
                        pixels[offset + 1] = bgra[1]; // G
                        pixels[offset + 2] = bgra[2]; // R
                        pixels[offset + 3] = bgra[3]; // A
                    }
                }
            }
        }

        private static void StampMarkerBoss(byte[] pixels, int w, int h, int stride,
            int cx, int cz, byte[] bgra)
        {
            for (int dx = -3; dx <= 3; dx++)
            {
                for (int dz = -3; dz <= 3; dz++)
                {
                    int px = cx + dx;
                    int pz = cz + dz;
                    if (px >= 0 && pz >= 0 && px < w && pz < h)
                    {
                        int offset = pz * stride + px * 4;
                        pixels[offset]     = bgra[0]; // B
                        pixels[offset + 1] = bgra[1]; // G
                        pixels[offset + 2] = bgra[2]; // R
                        pixels[offset + 3] = bgra[3]; // A
                    }
                }
            }
        }

        private static void StampMarkerLarge(byte[] pixels, int w, int h, int stride,
            int cx, int cz, byte[] bgra)
        {
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    int px = cx + dx;
                    int pz = cz + dz;
                    if (px >= 0 && pz >= 0 && px < w && pz < h)
                    {
                        int offset = pz * stride + px * 4;
                        pixels[offset]     = bgra[0]; // B
                        pixels[offset + 1] = bgra[1]; // G
                        pixels[offset + 2] = bgra[2]; // R
                        pixels[offset + 3] = bgra[3]; // A
                    }
                }
            }
        }

        public static void UpdateShipStatistics(System.Windows.Shapes.Rectangle healthRectangle, _3dObject ship)
        {
            if (ship == null || ship.ImpactStatus == null) return;
            if (ship.ImpactStatus.ObjectHealth > 0)
            {
                healthRectangle.Width = ship.ImpactStatus.ObjectHealth.Value * 2;
            }
            else healthRectangle.Width = 1;
        }
    }
}
