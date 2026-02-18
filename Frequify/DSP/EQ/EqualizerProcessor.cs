using Frequify.DSP.Filters;
using Frequify.Models;

namespace Frequify.DSP.EQ;

/// <summary>
/// Applies a three-band minimum-phase equalizer.
/// </summary>
public sealed class EqualizerProcessor : IAudioProcessor
{
    private readonly EqualizerSettings _settings;

    /// <summary>
    /// Initializes a new equalizer processor.
    /// </summary>
    /// <param name="settings">Equalizer settings.</param>
    public EqualizerProcessor(EqualizerSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var leftLow = BiquadFilter.LowShelf(sampleRate, _settings.LowShelf.FrequencyHz, _settings.LowShelf.GainDb, Math.Clamp(_settings.LowShelf.Q, 0.3, 3));
        var leftMid = BiquadFilter.Peaking(sampleRate, _settings.MidBell.FrequencyHz, _settings.MidBell.GainDb, Math.Clamp(_settings.MidBell.Q, 0.3, 6));
        var leftHigh = BiquadFilter.HighShelf(sampleRate, _settings.HighShelf.FrequencyHz, _settings.HighShelf.GainDb, Math.Clamp(_settings.HighShelf.Q, 0.3, 3));

        var rightLow = BiquadFilter.LowShelf(sampleRate, _settings.LowShelf.FrequencyHz, _settings.LowShelf.GainDb, Math.Clamp(_settings.LowShelf.Q, 0.3, 3));
        var rightMid = BiquadFilter.Peaking(sampleRate, _settings.MidBell.FrequencyHz, _settings.MidBell.GainDb, Math.Clamp(_settings.MidBell.Q, 0.3, 6));
        var rightHigh = BiquadFilter.HighShelf(sampleRate, _settings.HighShelf.FrequencyHz, _settings.HighShelf.GainDb, Math.Clamp(_settings.HighShelf.Q, 0.3, 3));

        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            l = leftLow.Process(l);
            l = leftMid.Process(l);
            l = leftHigh.Process(l);
            left[i] = l;

            var r = right[i];
            r = rightLow.Process(r);
            r = rightMid.Process(r);
            r = rightHigh.Process(r);
            right[i] = r;
        }
    }
}
