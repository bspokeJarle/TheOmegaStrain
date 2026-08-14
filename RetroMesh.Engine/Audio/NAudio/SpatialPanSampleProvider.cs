using System;
using NAudio.Wave;


namespace RetroMesh.Engine;

/// <summary>
/// Applies a simple stereo balance to an already-stereo sample stream.
/// NAudio's built-in PanningSampleProvider expects mono input, while this project
/// normalizes audio to stereo before panning, so a custom provider is required.
/// </summary>
internal sealed class SpatialPanSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _source;

    public SpatialPanSampleProvider(ISampleProvider source)
    {
        if (source.WaveFormat.Channels != 2)
        {
            throw new ArgumentException("Source sample provider must be stereo", nameof(source));
        }

        _source = source;
    }

    public float Pan { get; set; }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        int read = _source.Read(buffer, offset, count);

        // Clamp pan so invalid provider values never reach the audio pipeline.
        float pan = float.IsFinite(Pan) ? Math.Clamp(Pan, -1f, 1f) : 0f;

        // Positive pan reduces the left channel, negative pan reduces the right channel.
        float leftScale = pan > 0f ? 1f - pan : 1f;
        float rightScale = pan < 0f ? 1f + pan : 1f;

        int end = offset + read;
        for (int i = offset; i + 1 < end; i += 2)
        {
            buffer[i] *= leftScale;
            buffer[i + 1] *= rightScale;
        }

        return read;
    }
}
