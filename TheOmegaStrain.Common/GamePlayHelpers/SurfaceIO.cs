// GameplayHelpers.SurfaceIO.SurfaceIO.cs  (OSSD v2)
// ------------------------------------------------------------
// Complete helper class to READ/WRITE precomputed SurfaceData in a compact binary format,
// with hashing + validation + fail-safe load-or-generate.
//
// FIXED: mapId is stored as int32 (NOT ushort) so you do not get 65535 clamping.
//
// File format: "OSSD" v2
// - Header (32 bytes)
// - Payload = BaseTileData + CrashStream
// - SurfaceHash64 = xxHash64(payload)
//
// BaseTileData (stride = 7 bytes per tile):
//   mapDepth  : ushort  (2 bytes)
//   mapId     : int32   (4 bytes)
//   flags     : byte    (1 byte)
//     bit0 hasLandbasedObject
//     bit1 isInfected
//     bit2 hasCrashBox
//
// CrashStream (sparse):
//   entry stride = 7 bytes:
//     tileIndex : uint32 (z*width + x)
//     width     : byte
//     height    : byte
//     boxDepth  : byte
//
// Bitmap data is NOT stored. Build bitmap from surface after load.
//
// ------------------------------------------------------------

using Domain;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace GameplayHelpers.SurfaceIO
{
    public static class SurfaceIO
    {
        private const bool enableLogging = false;
        // ===== File Format Constants (OSSD v2) =====
        private const string Magic = "OSSD";
        private const ushort Version = 2;

        private const byte BaseTileStride = 7;   // depth(2) + mapId int32(4) + flags(1)
        private const int CrashEntryStride = 7;  // tileIndex(4) + w(1) + h(1) + d(1)

        // flags byte
        private const byte FlagHasLandbasedObject = 1 << 0;
        private const byte FlagIsInfected = 1 << 1;
        private const byte FlagHasCrashBox = 1 << 2;

        // ============================================================
        // Public API
        // ============================================================

        public readonly struct SurfaceLoadResult
        {
            public SurfaceLoadResult(SurfaceData[,] surface, ulong surfaceHash, bool loadedFromFile)
            {
                Surface = surface;
                SurfaceHash = surfaceHash;
                LoadedFromFile = loadedFromFile;
            }

            public SurfaceData[,] Surface { get; }
            public ulong SurfaceHash { get; }       // 0 for generated (by design unless you choose otherwise)
            public bool LoadedFromFile { get; }
        }

        /// <summary>
        /// Writes OSSD v2 file. Returns SurfaceHash64 (xxHash64 of payload).
        /// </summary>
        public static ulong Save(string path, SurfaceData[,] surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));

            int height = surface.GetLength(0);
            int width = surface.GetLength(1);

            if (width <= 0 || height <= 0)
                throw new ArgumentException("Surface array must have positive dimensions.");

            checked
            {
                int tileCount = width * height;
                int baseLen = tileCount * BaseTileStride;

                // Base tile bytes
                var baseBytes = new byte[baseLen];

                // Crash stream bytes (sparse)
                var crashStream = new List<byte>(capacity: 1024);

                int o = 0;
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var t = surface[z, x];

                        ushort depth = (ushort)Math.Clamp(t.mapDepth, 0, ushort.MaxValue);
                        int id = t.mapId; // IMPORTANT: store full int32

                        bool hasCrash = t.crashBox.HasValue;

                        byte flags = 0;
                        if (t.hasLandbasedObject) flags |= FlagHasLandbasedObject;
                        if (t.isInfected) flags |= FlagIsInfected;
                        if (hasCrash) flags |= FlagHasCrashBox;

                        // Write base tile (7 bytes)
                        BinaryPrimitives.WriteUInt16LittleEndian(baseBytes.AsSpan(o, 2), depth);
                        BinaryPrimitives.WriteInt32LittleEndian(baseBytes.AsSpan(o + 2, 4), id);
                        baseBytes[o + 6] = flags;
                        o += BaseTileStride;

                        // Write crash entry (sparse)
                        if (hasCrash)
                        {
                            var cb = t.crashBox!.Value;

                            byte w = (byte)Math.Clamp(cb.width, 0, 255);
                            byte h = (byte)Math.Clamp(cb.height, 0, 255);
                            byte d = (byte)Math.Clamp(cb.boxDepth, 0, 255);

                            uint tileIndex = (uint)(z * width + x);

                            Span<byte> entry = stackalloc byte[CrashEntryStride];
                            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(0, 4), tileIndex);
                            entry[4] = w;
                            entry[5] = h;
                            entry[6] = d;

                            crashStream.Add(entry[0]);
                            crashStream.Add(entry[1]);
                            crashStream.Add(entry[2]);
                            crashStream.Add(entry[3]);
                            crashStream.Add(entry[4]);
                            crashStream.Add(entry[5]);
                            crashStream.Add(entry[6]);
                        }
                    }
                }

                uint crashLen = (uint)crashStream.Count;
                if (crashLen % CrashEntryStride != 0)
                    throw new InvalidOperationException("Internal error: crash stream length not divisible by 7.");

                // Build full payload for hashing + writing (BaseTileData + CrashStream)
                byte[] crashBytes = crashLen == 0 ? Array.Empty<byte>() : crashStream.ToArray();
                var payload = new byte[baseBytes.Length + crashBytes.Length];
                Buffer.BlockCopy(baseBytes, 0, payload, 0, baseBytes.Length);
                if (crashBytes.Length > 0)
                    Buffer.BlockCopy(crashBytes, 0, payload, baseBytes.Length, crashBytes.Length);

                ulong hash64 = XxHash64.Compute(payload);

                using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.SequentialScan);
                using var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false);

                // Header (32 bytes)
                bw.Write(Encoding.ASCII.GetBytes(Magic));      // 4
                bw.Write(Version);                             // 2
                bw.Write(width);                               // 4
                bw.Write(height);                              // 4
                bw.Write(BaseTileStride);                      // 1
                bw.Write((byte)0);                             // 1 reserved
                bw.Write(hash64);                              // 8
                bw.Write((uint)baseBytes.Length);              // 4
                bw.Write(crashLen);                            // 4

                // Payload
                bw.Write(payload);

                return hash64;
            }
        }

        /// <summary>
        /// Fail-safe read. Returns true ONLY if:
        /// - file exists
        /// - header is valid
        /// - lengths are valid
        /// - payload is fully read
        /// - SurfaceHash64 matches computed hash
        /// - crash entries are valid + match hasCrashBox flags
        /// </summary>
        public static bool TryLoad(string path, out SurfaceData[,] surface, out ulong surfaceHash64)
        {
            surface = new SurfaceData[0, 0];
            surfaceHash64 = 0;

            if (string.IsNullOrWhiteSpace(path))
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log("TryLoad SurfaceIO. File is null or whitespace.");
                return false;
            }

            if (!File.Exists(path))
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log("TryLoad SurfaceIO. File does not exist.");
                return false;
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
                using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

                // Read header
                var magicBytes = br.ReadBytes(4);
                if (magicBytes.Length != 4) return false;

                string magic = Encoding.ASCII.GetString(magicBytes);
                if (!string.Equals(magic, Magic, StringComparison.Ordinal)) return false;

                ushort version = br.ReadUInt16();
                if (version != Version) return false;

                int width = br.ReadInt32();
                int height = br.ReadInt32();
                if (width <= 0 || height <= 0) return false;

                byte baseStride = br.ReadByte();
                _ = br.ReadByte(); // reserved
                if (baseStride != BaseTileStride) return false;

                ulong expectedHash = br.ReadUInt64();
                uint baseLen = br.ReadUInt32();
                uint crashLen = br.ReadUInt32();

                checked
                {
                    int tileCount = width * height;
                    uint expectedBaseLen = (uint)(tileCount * baseStride);
                    if (baseLen != expectedBaseLen) return false;

                    if (crashLen % CrashEntryStride != 0) return false;

                    int payloadLen = (int)(baseLen + crashLen);
                    var payload = br.ReadBytes(payloadLen);
                    if (payload.Length != payloadLen) return false;

                    // Hash validation
                    ulong actualHash = XxHash64.Compute(payload);
                    if (actualHash != expectedHash) return false;

                    // Parse base tiles
                    var result = new SurfaceData[height, width];
                    var hasCrashFlag = new bool[tileCount];

                    int o = 0;
                    for (int z = 0; z < height; z++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            ushort depth = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(o, 2));
                            int id = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(o + 2, 4));
                            byte flags = payload[o + 6];
                            o += baseStride;

                            bool hasObj = (flags & FlagHasLandbasedObject) != 0;
                            bool infected = (flags & FlagIsInfected) != 0;
                            bool hasCrash = (flags & FlagHasCrashBox) != 0;

                            int tileIndex = (z * width) + x;
                            hasCrashFlag[tileIndex] = hasCrash;

                            result[z, x] = new SurfaceData
                            {
                                mapDepth = depth,
                                mapId = id,
                                hasLandbasedObject = hasObj,
                                isInfected = infected,
                                crashBox = null
                            };
                        }
                    }

                    // Parse crash stream
                    int crashOffset = (int)baseLen;
                    int crashEnd = crashOffset + (int)crashLen;

                    // Fail-safe: no duplicates
                    var seen = crashLen == 0 ? null : new HashSet<uint>();

                    while (crashOffset < crashEnd)
                    {
                        uint tileIndex = BinaryPrimitives.ReadUInt32LittleEndian(payload.AsSpan(crashOffset, 4));
                        byte w = payload[crashOffset + 4];
                        byte h = payload[crashOffset + 5];
                        byte d = payload[crashOffset + 6];
                        crashOffset += CrashEntryStride;

                        if (tileIndex >= (uint)tileCount) return false;
                        if (!hasCrashFlag[tileIndex]) return false;

                        if (seen != null && !seen.Add(tileIndex)) return false;

                        int ix = (int)tileIndex;
                        int z = ix / width;
                        int x = ix - (z * width);

                        result[z, x].crashBox = new SurfaceData.CrashBoxData
                        {
                            width = w,
                            height = h,
                            boxDepth = d
                        };
                    }

                    surface = result;
                    surfaceHash64 = actualHash;
                    return true;
                }
            }
            catch(Exception ex)
            {
                if (Logger.ShouldLog(enableLogging)) Logger.Log($"SurfaceIO, fail safe triggered, try load. Exception:{ex.Message}");
                // Fail-safe
                surface = new SurfaceData[0, 0];
                surfaceHash64 = 0;
                return false;
            }
        }

        /// <summary>
        /// Loads from file if valid; otherwise uses generate().
        /// Generated surfaces return SurfaceHash=0 by design (meaning "not replay-eligible").
        /// </summary>
        public static SurfaceLoadResult LoadOrGenerate(string path, Func<SurfaceData[,]> generate)
        {
            if (generate == null) throw new ArgumentNullException(nameof(generate));

            if (TryLoad(path, out var surface, out var hash))
                return new SurfaceLoadResult(surface, hash, loadedFromFile: true);

            var generated = generate();
            return new SurfaceLoadResult(generated, surfaceHash: 0, loadedFromFile: false);
        }

        /// <summary>
        /// Optional: compute the OSSD payload hash for an in-memory surface (same as file hash).
        /// Useful later if you allow replay for generated surfaces too.
        /// </summary>
        public static ulong ComputeSurfaceHash(SurfaceData[,] surface)
        {
            if (surface == null) throw new ArgumentNullException(nameof(surface));

            int height = surface.GetLength(0);
            int width = surface.GetLength(1);
            if (width <= 0 || height <= 0) return 0;

            checked
            {
                int tileCount = width * height;
                int baseLen = tileCount * BaseTileStride;

                var baseBytes = new byte[baseLen];
                var crashStream = new List<byte>(capacity: 1024);

                int o = 0;
                for (int z = 0; z < height; z++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var t = surface[z, x];

                        ushort depth = (ushort)Math.Clamp(t.mapDepth, 0, ushort.MaxValue);
                        int id = t.mapId;

                        bool hasCrash = t.crashBox.HasValue;

                        byte flags = 0;
                        if (t.hasLandbasedObject) flags |= FlagHasLandbasedObject;
                        if (t.isInfected) flags |= FlagIsInfected;
                        if (hasCrash) flags |= FlagHasCrashBox;

                        BinaryPrimitives.WriteUInt16LittleEndian(baseBytes.AsSpan(o, 2), depth);
                        BinaryPrimitives.WriteInt32LittleEndian(baseBytes.AsSpan(o + 2, 4), id);
                        baseBytes[o + 6] = flags;

                        o += BaseTileStride;

                        if (hasCrash)
                        {
                            var cb = t.crashBox!.Value;

                            byte w = (byte)Math.Clamp(cb.width, 0, 255);
                            byte h = (byte)Math.Clamp(cb.height, 0, 255);
                            byte d = (byte)Math.Clamp(cb.boxDepth, 0, 255);

                            uint tileIndex = (uint)(z * width + x);

                            Span<byte> entry = stackalloc byte[CrashEntryStride];
                            BinaryPrimitives.WriteUInt32LittleEndian(entry.Slice(0, 4), tileIndex);
                            entry[4] = w;
                            entry[5] = h;
                            entry[6] = d;

                            crashStream.Add(entry[0]);
                            crashStream.Add(entry[1]);
                            crashStream.Add(entry[2]);
                            crashStream.Add(entry[3]);
                            crashStream.Add(entry[4]);
                            crashStream.Add(entry[5]);
                            crashStream.Add(entry[6]);
                        }
                    }
                }

                byte[] crashBytes = crashStream.Count == 0 ? Array.Empty<byte>() : crashStream.ToArray();

                var payload = new byte[baseBytes.Length + crashBytes.Length];
                Buffer.BlockCopy(baseBytes, 0, payload, 0, baseBytes.Length);
                if (crashBytes.Length > 0)
                    Buffer.BlockCopy(crashBytes, 0, payload, baseBytes.Length, crashBytes.Length);

                return XxHash64.Compute(payload);
            }
        }

        // ============================================================
        // Internal xxHash64 implementation
        // ============================================================
        private static class XxHash64
        {
            private const ulong P1 = 11400714785074694791UL;
            private const ulong P2 = 14029467366897019727UL;
            private const ulong P3 = 1609587929392839161UL;
            private const ulong P4 = 9650029242287828579UL;
            private const ulong P5 = 2870177450012600261UL;

            public static ulong Compute(ReadOnlySpan<byte> data, ulong seed = 0)
            {
                int len = data.Length;
                int index = 0;
                ulong h64;

                if (len >= 32)
                {
                    ulong v1 = seed + P1 + P2;
                    ulong v2 = seed + P2;
                    ulong v3 = seed + 0;
                    ulong v4 = seed - P1;

                    int limit = len - 32;
                    while (index <= limit)
                    {
                        v1 = Round(v1, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8))); index += 8;
                        v2 = Round(v2, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8))); index += 8;
                        v3 = Round(v3, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8))); index += 8;
                        v4 = Round(v4, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8))); index += 8;
                    }

                    h64 = RotateLeft(v1, 1) + RotateLeft(v2, 7) + RotateLeft(v3, 12) + RotateLeft(v4, 18);
                    h64 = MergeRound(h64, v1);
                    h64 = MergeRound(h64, v2);
                    h64 = MergeRound(h64, v3);
                    h64 = MergeRound(h64, v4);
                }
                else
                {
                    h64 = seed + P5;
                }

                h64 += (ulong)len;

                while (index <= len - 8)
                {
                    ulong k1 = Round(0, BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(index, 8)));
                    h64 ^= k1;
                    h64 = RotateLeft(h64, 27) * P1 + P4;
                    index += 8;
                }

                if (index <= len - 4)
                {
                    h64 ^= (ulong)BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(index, 4)) * P1;
                    h64 = RotateLeft(h64, 23) * P2 + P3;
                    index += 4;
                }

                while (index < len)
                {
                    h64 ^= data[index] * P5;
                    h64 = RotateLeft(h64, 11) * P1;
                    index++;
                }

                // avalanche
                h64 ^= h64 >> 33;
                h64 *= P2;
                h64 ^= h64 >> 29;
                h64 *= P3;
                h64 ^= h64 >> 32;

                return h64;
            }

            private static ulong Round(ulong acc, ulong input)
            {
                acc += input * P2;
                acc = RotateLeft(acc, 31);
                acc *= P1;
                return acc;
            }

            private static ulong MergeRound(ulong acc, ulong val)
            {
                val = Round(0, val);
                acc ^= val;
                acc = acc * P1 + P4;
                return acc;
            }

            private static ulong RotateLeft(ulong x, int r) => (x << r) | (x >> (64 - r));
        }
    }
}
