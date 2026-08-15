using System.Collections.Generic;
using static Domain._3dSpecificsImplementations;

namespace Domain
{
    public interface IGameReplay
    {
        string ReplayFile { get; }
        string ReplayName { get; }
        ulong SurfaceHash { get; }
        int Fps { get; }
        List<IFrameState> ReplayFrames { get; }
    }

    public interface IFrameState
    {
        int FrameIndex { get; set; }
        int RecordedObjectCount { get; set; }
        Vector3 GlobalMapPosition { get; set; }
        List<IReplayObjectState> ObjectStates { get; set; }

        void Clear(int frameIndex)
        {
            FrameIndex = frameIndex;
            ObjectStates.Clear();
        }
    }

    public interface IReplayObjectState
    {
        int ObjectId { get; set; }
        string ObjectName { get; set; }
        Vector3 WorldPosition { get; set; }
        Vector3 ObjectOffset { get; set; }
        Vector3 Rotation { get; set; }
        bool TriggerExplode { get; set; }
    }
}
