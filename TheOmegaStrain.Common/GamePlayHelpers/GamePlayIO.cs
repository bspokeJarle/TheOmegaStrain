using Domain;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using static Domain._3dSpecificsImplementations;
using System.Text.Json;

namespace GameplayHelpers.ReplayIO
{
    public static class ReplayIO
    {
        private const string Magic = "OSRP";
        private const ushort Version = 2;

        private const byte FlagTriggerExplode = 1 << 0;

        // ------------------------------------------------------------
        // SAVE
        // ------------------------------------------------------------
        public static ulong Save(string path, IGameReplay replay)
        {
            if (replay == null) throw new ArgumentNullException(nameof(replay));
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Path is required.", nameof(path));

            // Build payload in a MemoryStream (simple + safe). If you later want streaming-hash, we can do that too.
            byte[] payload;
            using (var ms = new MemoryStream(capacity: 1 << 20))
            using (var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true))
            {
                foreach (var f in replay.ReplayFrames)
                {
                    bw.Write(f.FrameIndex);
                    WriteVector3(bw, f.GlobalMapPosition);

                    int objectCount = f.RecordedObjectCount > 0
                        ? f.RecordedObjectCount
                        : f.ObjectStates?.Count ?? 0;

                    ushort count = (ushort)Math.Min(objectCount, ushort.MaxValue);
                    bw.Write(count);

                    for (int i = 0; i < count; i++)
                    {
                        var o = f.ObjectStates[i];

                        bw.Write(o.ObjectId);

                        byte flags = 0;
                        if (o.TriggerExplode) flags |= FlagTriggerExplode;
                        bw.Write(flags);

                        WriteVector3(bw, o.WorldPosition);
                        WriteVector3(bw, o.ObjectOffset);
                        WriteVector3(bw, o.Rotation);

                        WriteString(bw, o.ObjectName ?? string.Empty);
                    }
                }

                payload = ms.ToArray();
            }

            ulong payloadHash = XxHash64.Compute(payload);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 20, FileOptions.SequentialScan))
            using (var bwFile = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false))
            {
                // Header
                bwFile.Write(Encoding.ASCII.GetBytes(Magic));     // 4
                bwFile.Write(Version);                            // 2
                bwFile.Write((ushort)replay.Fps);                 // 2
                bwFile.Write((uint)0);                            // 4 reserved
                bwFile.Write(replay.SurfaceHash);                 // 8
                bwFile.Write(replay.ReplayFrames.Count);          // 4
                bwFile.Write(payloadHash);                        // 8

                // Payload
                bwFile.Write(payload);
            }

            return payloadHash;
        }

        // ------------------------------------------------------------
        // LOAD (fail-safe)
        // ------------------------------------------------------------
        public static bool TryLoad(
            string path,
            out ulong surfaceHash,
            out int fps,
            out List<FrameState> frames)
        {
            surfaceHash = 0;
            fps = 0;
            frames = new List<FrameState>();

            if (string.IsNullOrWhiteSpace(path)) return false;
            if (!File.Exists(path)) return false;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20, FileOptions.SequentialScan);
                using var br = new BinaryReader(fs, Encoding.UTF8, leaveOpen: false);

                var magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if (magic != Magic) { surfaceHash = 0; fps = 0; frames = new List<FrameState>(); return false; }

                ushort ver = br.ReadUInt16();
                if (ver != 1 && ver != Version) { surfaceHash = 0; fps = 0; frames = new List<FrameState>(); return false; }

                fps = br.ReadUInt16();
                _ = br.ReadUInt32(); // reserved

                surfaceHash = br.ReadUInt64();
                int frameCount = br.ReadInt32();
                ulong expectedPayloadHash = br.ReadUInt64();

                if (frameCount < 0) { surfaceHash = 0; fps = 0; frames = new List<FrameState>(); return false; }

                // Read remaining as payload
                byte[] payload = br.ReadBytes((int)(fs.Length - fs.Position));
                if (payload.Length == 0 && frameCount > 0) { surfaceHash = 0; fps = 0; frames = new List<FrameState>(); return false; }

                ulong actualPayloadHash = XxHash64.Compute(payload);
                if (actualPayloadHash != expectedPayloadHash) { surfaceHash = 0; fps = 0; frames = new List<FrameState>(); return false; }

                // Parse payload
                using var ms = new MemoryStream(payload, writable: false);
                using var pr = new BinaryReader(ms, Encoding.UTF8, leaveOpen: false);

                frames = new List<FrameState>(frameCount);

                for (int f = 0; f < frameCount; f++)
                {
                    int frameIndex = pr.ReadInt32();
                    Vector3 globalMapPosition = ver >= 2 ? ReadVector3(pr) : new Vector3();
                    ushort objectCount = pr.ReadUInt16();

                    var frame = new FrameState
                    {
                        FrameIndex = frameIndex,
                        RecordedObjectCount = objectCount,
                        GlobalMapPosition = globalMapPosition,
                        ObjectStates = new List<ReplayObjectState>(objectCount)
                    };

                    for (int i = 0; i < objectCount; i++)
                    {
                        int objectId = pr.ReadInt32();
                        byte flags = pr.ReadByte();

                        var obj = new ReplayObjectState
                        {
                            ObjectId = objectId,
                            TriggerExplode = (flags & FlagTriggerExplode) != 0,
                            WorldPosition = ReadVector3(pr),
                            ObjectOffset = ReadVector3(pr),
                            Rotation = ReadVector3(pr),
                            ObjectName = ver >= 2 ? ReadString(pr) : string.Empty
                        };

                        frame.ObjectStates.Add(obj);
                    }

                    frames.Add(frame);
                }

                return true;
            }
            catch
            {
                surfaceHash = 0;
                fps = 0;
                frames = new List<FrameState>();
                return false;
            }
        }

        private static void WriteString(BinaryWriter bw, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            ushort length = (ushort)Math.Min(bytes.Length, ushort.MaxValue);
            bw.Write(length);
            bw.Write(bytes, 0, length);
        }

        private static string ReadString(BinaryReader br)
        {
            ushort length = br.ReadUInt16();
            if (length == 0) return string.Empty;
            var bytes = br.ReadBytes(length);
            return Encoding.UTF8.GetString(bytes);
        }

        // ------------------------------------------------------------
        // Vector3 IO (uses YOUR Vector3)
        // ------------------------------------------------------------
        private static void WriteVector3(BinaryWriter bw, Vector3 v)
        {
            bw.Write(v.x);
            bw.Write(v.y);
            bw.Write(v.z);
        }

        private static Vector3 ReadVector3(BinaryReader br)
        {
            return new Vector3
            {
                x = br.ReadSingle(),
                y = br.ReadSingle(),
                z = br.ReadSingle()
            };
        }

        // ------------------------------------------------------------
        // Concrete minimal classes (binary-friendly)
        // You can swap these for your own implementations easily.
        // ------------------------------------------------------------
        public sealed class FrameState:IFrameState
        {
            public int FrameIndex { get; set; }
            public int RecordedObjectCount { get; set; }
            public Vector3 GlobalMapPosition { get; set; } = new Vector3();
            public List<ReplayObjectState> ObjectStates { get; set; } = new();
            List<IReplayObjectState> IFrameState.ObjectStates
            {
                get => ObjectStates.Cast<IReplayObjectState>().ToList();
                set => ObjectStates = value.Cast<ReplayObjectState>().ToList();
            }
        }

        public sealed class ReplayObjectState: IReplayObjectState
        {
            public int ObjectId { get; set; }
            public string ObjectName { get; set; } = "";
            public required Vector3 WorldPosition { get; set; }
            public required Vector3 ObjectOffset { get; set; }
            public required Vector3 Rotation { get; set; }
            public bool TriggerExplode { get; set; }
        }

        // ------------------------------------------------------------
        // xxHash64: reuse the exact same implementation as SurfaceIO
        // ------------------------------------------------------------
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

        public static void Save(string path, GameReplay replay)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new InvalidDataException("Replay path is required.");

            if (replay == null)
                throw new InvalidDataException("Replay data is required.");

            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            var json = JsonSerializer.Serialize(replay, options);
            File.WriteAllText(path, json);
        }
    }
}