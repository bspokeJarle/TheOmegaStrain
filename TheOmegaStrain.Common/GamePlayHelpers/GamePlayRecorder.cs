using CommonUtilities.CommonGlobalState; // Logger.Log(...) (adjust if needed)
using Domain;                            // Vector3, I3dObject, _3dObject etc. (adjust if needed)
using System;
using System.Collections.Generic;
using System.Linq;
using static Domain._3dSpecificsImplementations;

namespace GameplayHelpers.ReplayIO
{
    // ============================================================
    // Replay Recorder
    // ------------------------------------------------------------
    // Records only the objects that are part of the current render/active list.
    // If an object is not in the frame => it will not be recorded => not handled in playback.
    //
    // No generic events are recorded.
    // The only explicit trigger we store is "TriggerExplode", which will call ExplodeObject during playback.
    //
    // Typical usage (Record mode):
    //  - recorder.BeginRecording(surfaceHash, fps)
    //  - Each tick AFTER you have built the render list:
    //      recorder.RecordFrame(frameIndex, renderObjects)
    //  - When crash/explosion happens in Live/Record:
    //      recorder.TriggerExplodeForCurrentFrame(objectId)
    //  - At end:
    //      var replay = recorder.EndRecording(replayFile, replayName)
    //      ReplayIO.Save(file, replay)
    // ============================================================

    public sealed class ReplayRecorder
    {
        private readonly bool _enableLogging;
        private const int LogInterval = 60;
        private bool _isRecording;
        private int _currentFrameIndex = -1;

        private readonly List<IFrameState> _frames = new();
        private readonly Dictionary<int, ReplayObjectState> _currentFrameStatesById = new();
        private Vector3 _lastRecordedMap = new Vector3();
        private Vector3 _lastRecordedShipOffset = new Vector3();
        private int _unchangedFrameCount = 0;

        public ReplayRecorder(bool enableLogging = false)
        {
            _enableLogging = enableLogging;
        }

        public bool IsRecording => _isRecording;

        public void BeginRecording(ulong surfaceHash, int fps)
        {
            _isRecording = true;
            _currentFrameIndex = -1;

            _frames.Clear();
            _currentFrameStatesById.Clear();

            _lastRecordedMap = new Vector3();
            _lastRecordedShipOffset = new Vector3();
            _unchangedFrameCount = 0;

            SurfaceHash = surfaceHash;
            Fps = fps <= 0 ? 60 : fps;

            if (Logger.ShouldLog(_enableLogging))
                Logger.Log($"ReplayRecorder: BeginRecording surfaceHash={SurfaceHash} fps={Fps}", "Replay");
        }

        public void StopRecording()
        {
            if (Logger.ShouldLog(_enableLogging))
                Logger.Log("ReplayRecorder: StopRecording", "Replay");
            _isRecording = false;
        }

        public ulong SurfaceHash { get; private set; }
        public int Fps { get; private set; } = 90;

        /// <summary>
        /// Records the given list as the truth for this frame.
        /// Only objects in this list are recorded/handled in playback.
        /// </summary>
        public void RecordFrame(int frameIndex, IReadOnlyList<I3dObject> renderObjects)
        {
            if (!_isRecording) return;
            if (renderObjects == null) return;
            if (frameIndex < 0) return;

            _currentFrameIndex = frameIndex;
            _currentFrameStatesById.Clear();

            var mapPos = new Vector3(
                GameState.SurfaceState.GlobalMapPosition.x,
                GameState.SurfaceState.GlobalMapPosition.y,
                GameState.SurfaceState.GlobalMapPosition.z);

            var frame = new FrameState
            {
                FrameIndex = frameIndex,
                GlobalMapPosition = mapPos,
                ObjectStates = new List<IReplayObjectState>(renderObjects.Count)
            };

            // Snapshot states
            for (int i = 0; i < renderObjects.Count; i++)
            {
                if (renderObjects[i] is not _3dObject obj) continue;

                var state = new ReplayObjectState
                {
                    ObjectId = obj.ObjectId,
                    ObjectName = obj.ObjectName ?? "",
                    WorldPosition = (Vector3)obj.WorldPosition,
                    ObjectOffset = (Vector3)obj.ObjectOffsets,
                    Rotation = (Vector3)(obj.Rotation ?? new Vector3()),
                    TriggerExplode = false
                };

                frame.ObjectStates.Add(state);
                _currentFrameStatesById[state.ObjectId] = state;
            }

            _frames.Add(frame);
            frame.RecordedObjectCount = frame.ObjectStates.Count;

            var shipState = frame.ObjectStates.FirstOrDefault(s => string.Equals(s.ObjectName, "Ship", StringComparison.OrdinalIgnoreCase));
            var shipOffset = shipState != null ? new Vector3(shipState.ObjectOffset.x, shipState.ObjectOffset.y, shipState.ObjectOffset.z) : new Vector3();
            var mapSnapshot = new Vector3(mapPos.x, mapPos.y, mapPos.z);
            bool mapUnchanged = mapSnapshot.x == _lastRecordedMap.x && mapSnapshot.y == _lastRecordedMap.y && mapSnapshot.z == _lastRecordedMap.z;
            bool shipUnchanged = shipOffset.x == _lastRecordedShipOffset.x && shipOffset.y == _lastRecordedShipOffset.y && shipOffset.z == _lastRecordedShipOffset.z;

            if (mapUnchanged && shipUnchanged)
            {
                _unchangedFrameCount++;
                if (Logger.ShouldLog(_enableLogging) && _unchangedFrameCount % LogInterval == 0)
                {
                    Logger.Log($"ReplayRecorder: unchanged frames={_unchangedFrameCount} at frame {frameIndex}", "Replay");
                }
            }
            else
            {
                _unchangedFrameCount = 0;
                _lastRecordedMap = mapSnapshot;
                _lastRecordedShipOffset = shipOffset;
            }

            if (Logger.ShouldLog(_enableLogging) && frameIndex % LogInterval == 0)
            {
                Logger.Log($"ReplayRecorder: Recorded frame {frameIndex}, objects={frame.ObjectStates.Count} map=({mapPos.x:0.##};{mapPos.y:0.##};{mapPos.z:0.##}) shipOff=({shipOffset.x:0.##};{shipOffset.y:0.##};{shipOffset.z:0.##})", "Replay");
            }
        }

        /// <summary>
        /// Marks an explosion trigger for the given object in the CURRENT recorded frame.
        /// Call this from your explosion/crash logic in Live/Record mode.
        /// </summary>
        public void TriggerExplodeForCurrentFrame(int objectId)
        {
            if (!_isRecording) return;
            if (_currentFrameIndex < 0) return;

            // If the object was not in this frame list, we do nothing by design:
            // "If it's not in the frame, it is not handled."
            if (_currentFrameStatesById.TryGetValue(objectId, out var state))
            {
                state.TriggerExplode = true;
                if (Logger.ShouldLog(_enableLogging))
                    Logger.Log($"ReplayRecorder: TriggerExplode frame={_currentFrameIndex} objectId={objectId}", "Replay");
            }
            else
            {
                // Optional log - can be noisy
                if (Logger.ShouldLog(_enableLogging))
                    Logger.Log($"ReplayRecorder: TriggerExplode ignored (object not in frame). frame={_currentFrameIndex} objectId={objectId}", "Replay");
            }
        }

        public GameReplay EndRecording(string replayFile, string replayName)
        {
            _isRecording = false;

            // Return a concrete replay object ready for saving
            var replay = new GameReplay
            {
                ReplayFile = replayFile ?? "",
                ReplayName = replayName ?? "",
                SurfaceHash = SurfaceHash,
                Fps = Fps,
                ReplayFrames = _frames
            };

            if (Logger.ShouldLog(_enableLogging))
                Logger.Log($"ReplayRecorder: EndRecording frames={_frames.Count}", "Replay");
            return replay;
        }
    }

    // ------------------------------------------------------------
    // Concrete model (binary-friendly)
    // Keep these aligned with your chosen data structure.
    // ------------------------------------------------------------

    public sealed class GameReplay: IGameReplay
    {
        public string ReplayFile { get; set; } = "";
        public string ReplayName { get; set; } = "";
        public ulong SurfaceHash { get; set; }
        public int Fps { get; set; } = 60;
        public List<IFrameState> ReplayFrames { get; set; } = new();
    }

    public sealed class FrameState: IFrameState
    {
        public int FrameIndex { get; set; }
        public int RecordedObjectCount { get; set; }
        public Vector3 GlobalMapPosition { get; set; } = new Vector3();
        public List<IReplayObjectState> ObjectStates { get; set; } = new();

        public void Clear(int frameIndex)
        {
            FrameIndex = frameIndex;
            ObjectStates.Clear();
            RecordedObjectCount = 0;
        }
    }

    public sealed class ReplayObjectState:IReplayObjectState
    {
        public int ObjectId { get; set; }
        public string ObjectName { get; set; } = ""; // debug only
        public required Vector3 WorldPosition { get; set; }
        public required Vector3 ObjectOffset { get; set; }
        public required Vector3 Rotation { get; set; }
        public bool TriggerExplode { get; set; }
    }
}
