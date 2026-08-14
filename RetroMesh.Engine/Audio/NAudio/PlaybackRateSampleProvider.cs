using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;


namespace RetroMesh.Engine;

internal sealed class PlaybackRateSampleProvider : ISampleProvider
{
    private const float MinPlaybackRate = 0.25f;
    private const float MaxPlaybackRate = 4.0f;

    private readonly ISampleProvider _source;

    public PlaybackRateSampleProvider(ISampleProvider source, float playbackRate)
    {
        _source = source;
        PlaybackRate = Math.Clamp(playbackRate, MinPlaybackRate, MaxPlaybackRate);

        int sampleRate = Math.Clamp(
            (int)MathF.Round(source.WaveFormat.SampleRate * PlaybackRate),
            8000,
            192000);

        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(sampleRate, source.WaveFormat.Channels);
    }

    public float PlaybackRate { get; }
    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count) =>
        _source.Read(buffer, offset, count);
}
