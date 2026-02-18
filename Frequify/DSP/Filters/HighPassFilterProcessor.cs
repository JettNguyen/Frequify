using Frequify.Models;

namespace Frequify.DSP.Filters;

/// <summary>
/// Applies a high-pass cleanup filter to reduce inaudible sub-bass build-up.
/// </summary>
public sealed class HighPassFilterProcessor : IAudioProcessor
{
    private readonly HighPassSettings _settings;

    /// <summary>
    /// Initializes a new high-pass processor.
    /// </summary>
    /// <param name="settings">High-pass settings.</param>
    public HighPassFilterProcessor(HighPassSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var cutoff = Math.Clamp(_settings.CutoffHz, 20, 120);
        var leftFilter = BiquadFilter.HighPass(sampleRate, cutoff, 0.707);
        var rightFilter = BiquadFilter.HighPass(sampleRate, cutoff, 0.707);

        for (var i = 0; i < left.Length; i++)
        {
            left[i] = leftFilter.Process(left[i]);
            right[i] = rightFilter.Process(right[i]);
        }
    }
}
