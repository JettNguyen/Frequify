using Frequify.Models;

namespace Frequify.DSP.Saturation;

/// <summary>
/// Applies soft-clipping saturation with controllable drive.
/// </summary>
public sealed class SoftClipSaturationProcessor : IAudioProcessor
{
    private readonly SaturationSettings _settings;

    /// <summary>
    /// Initializes a new saturation processor.
    /// </summary>
    /// <param name="settings">Saturation settings.</param>
    public SoftClipSaturationProcessor(SaturationSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var drive = 1.0 + Math.Clamp(_settings.Drive, 0, 1) * 6.0;

        for (var i = 0; i < left.Length; i++)
        {
            left[i] = SoftClip(left[i], drive);
            right[i] = SoftClip(right[i], drive);
        }
    }

    private static float SoftClip(float value, double drive)
    {
        var shaped = Math.Tanh(value * drive);
        var normalized = shaped / Math.Tanh(drive);
        return (float)normalized;
    }
}
