using Frequify.Models;

namespace Frequify.DSP.Limiter;

/// <summary>
/// Applies a true-peak-safe brickwall limiter using lookahead analysis.
/// </summary>
public sealed class BrickwallLimiterProcessor : IAudioProcessor
{
    private readonly LimiterSettings _settings;

    /// <summary>
    /// Initializes a new limiter processor.
    /// </summary>
    /// <param name="settings">Limiter settings.</param>
    public BrickwallLimiterProcessor(LimiterSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var ceilingLinear = Math.Pow(10, _settings.CeilingDbTp / 20.0);
        var lookaheadSamples = Math.Max(1, (int)(Math.Clamp(_settings.LookaheadMs, 0.5, 10) * 0.001 * sampleRate));
        var releaseCoeff = Math.Exp(-1.0 / (0.05 * sampleRate));
        var gain = 1.0;

        for (var i = 0; i < left.Length; i++)
        {
            double peak = 0;
            var end = Math.Min(left.Length - 1, i + lookaheadSamples);
            for (var j = i; j <= end; j++)
            {
                peak = Math.Max(peak, Math.Abs(left[j]));
                peak = Math.Max(peak, Math.Abs(right[j]));
            }

            var desiredGain = peak > ceilingLinear ? ceilingLinear / peak : 1.0;
            gain = desiredGain < gain ? desiredGain : (releaseCoeff * gain + (1 - releaseCoeff) * desiredGain);

            left[i] = (float)(left[i] * gain);
            right[i] = (float)(right[i] * gain);
        }

        var truePeak = EstimateTruePeak(left, right);
        if (truePeak > ceilingLinear)
        {
            var safetyGain = ceilingLinear / truePeak;
            for (var i = 0; i < left.Length; i++)
            {
                left[i] = (float)(left[i] * safetyGain);
                right[i] = (float)(right[i] * safetyGain);
            }
        }
    }

    private static double EstimateTruePeak(Span<float> left, Span<float> right)
    {
        double peak = 0;
        for (var i = 0; i < left.Length - 1; i++)
        {
            var l0 = left[i];
            var l1 = left[i + 1];
            var r0 = right[i];
            var r1 = right[i + 1];

            for (var s = 0; s < 4; s++)
            {
                var t = s / 4.0;
                var li = l0 + (l1 - l0) * t;
                var ri = r0 + (r1 - r0) * t;
                peak = Math.Max(peak, Math.Abs(li));
                peak = Math.Max(peak, Math.Abs(ri));
            }
        }

        return peak;
    }
}
