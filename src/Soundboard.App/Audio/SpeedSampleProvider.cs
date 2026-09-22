using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Soundboard.Audio;

/// <summary>
/// Relabels a source as if it were recorded at a different sample rate, then
/// resamples back to the original rate. Result: playback speed changes and
/// pitch follows the rate (chipmunk / slowdown). No extra SoundTouch package.
/// </summary>
internal sealed class RelabeledRateSampleProvider : ISampleProvider
{
    private readonly ISampleProvider _inner;

    public RelabeledRateSampleProvider(ISampleProvider inner, int claimedSampleRate)
    {
        _inner = inner;
        WaveFormat = WaveFormat.CreateIeeeFloatWaveFormat(
            claimedSampleRate, inner.WaveFormat.Channels);
    }

    public WaveFormat WaveFormat { get; }

    public int Read(float[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
}

internal static class PlaybackSpeed
{
    public static ISampleProvider Apply(ISampleProvider source, float speed)
    {
        speed = Math.Clamp(speed, Core.SpeedRamp.MinSpeed, Core.SpeedRamp.AbsoluteMax);
        if (Math.Abs(speed - 1f) < 0.001f)
        {
            return source;
        }

        var originalRate = source.WaveFormat.SampleRate;
        var claimedRate = Math.Max(8000, (int)Math.Round(originalRate * speed));
        if (claimedRate == originalRate)
        {
            return source;
        }

        var relabeled = new RelabeledRateSampleProvider(source, claimedRate);
        return new WdlResamplingSampleProvider(relabeled, originalRate);
    }
}
