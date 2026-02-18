using Frequify.DSP.Analysis;
using Frequify.Models;

namespace Frequify.DSP.Loudness;

/// <summary>
/// Applies integrated loudness normalization toward a target LUFS value.
/// </summary>
public sealed class LoudnessNormalizerProcessor : IAudioProcessor
{
    private readonly LoudnessNormalizationSettings _settings;

    /// <summary>
    /// Initializes a new loudness normalizer.
    /// </summary>
    /// <param name="settings">Loudness normalization settings.</param>
    public LoudnessNormalizerProcessor(LoudnessNormalizationSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var currentLufs = LoudnessMeter.CalculateIntegratedLufs(left, right, sampleRate);
        var gainDb = _settings.TargetLufs - currentLufs;
        var gain = Math.Pow(10, gainDb / 20.0);

        for (var i = 0; i < left.Length; i++)
        {
            left[i] = (float)(left[i] * gain);
            right[i] = (float)(right[i] * gain);
        }
    }
}
