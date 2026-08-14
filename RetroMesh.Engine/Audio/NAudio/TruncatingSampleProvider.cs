using NAudio.Wave;
using System;


namespace RetroMesh.Engine;

/// <summary>
/// Wraps a sample provider and stops returning samples after a specified duration.
/// Used by OneShot mode to play only a specific segment of an audio file.
/// </summary>
internal sealed class TruncatingSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;
    private long _samplesRemaining;

    public TruncatingSampleProvider(ISampleProvider source, TimeSpan duration)
    {
        _source = source;
        _samplesRemaining = (long)(duration.TotalSeconds * source.WaveFormat.SampleRate * source.WaveFormat.Channels);
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_samplesRemaining <= 0) return 0;
        int toRead = (int)Math.Min(count, _samplesRemaining);
        int read = _source.Read(buffer, offset, toRead);
        _samplesRemaining -= read;
        return read;
    }
}
