using Frequify.DSP.Filters;
using Frequify.Models;

namespace Frequify.DSP.Rebalance;

/// <summary>
/// Applies pseudo stem rebalance using broad-band tonal weighting for vocals, drums, and instruments.
/// </summary>
public sealed class PseudoRebalanceProcessor : IAudioProcessor
{
    private readonly RebalanceSettings _settings;

    public PseudoRebalanceProcessor(RebalanceSettings settings)
    {
        _settings = settings;
    }

    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var vocalGain = Math.Clamp(_settings.VocalGainDb, -6.0, 6.0);
        var drumGain = Math.Clamp(_settings.DrumGainDb, -6.0, 6.0);
        var instrumentGain = Math.Clamp(_settings.InstrumentGainDb, -6.0, 6.0);

        if (Math.Abs(vocalGain) < 0.01 && Math.Abs(drumGain) < 0.01 && Math.Abs(instrumentGain) < 0.01)
        {
            return;
        }

        var leftVocalPresence = BiquadFilter.Peaking(sampleRate, 2800, vocalGain * 0.70, 1.0);
        var leftVocalBody = BiquadFilter.Peaking(sampleRate, 1200, vocalGain * 0.35, 0.9);

        var rightVocalPresence = BiquadFilter.Peaking(sampleRate, 2800, vocalGain * 0.70, 1.0);
        var rightVocalBody = BiquadFilter.Peaking(sampleRate, 1200, vocalGain * 0.35, 0.9);

        var leftDrumWeight = BiquadFilter.Peaking(sampleRate, 95, drumGain * 0.70, 0.8);
        var leftDrumAttack = BiquadFilter.Peaking(sampleRate, 4200, drumGain * 0.35, 1.2);

        var rightDrumWeight = BiquadFilter.Peaking(sampleRate, 95, drumGain * 0.70, 0.8);
        var rightDrumAttack = BiquadFilter.Peaking(sampleRate, 4200, drumGain * 0.35, 1.2);

        var leftInstrumentBody = BiquadFilter.Peaking(sampleRate, 650, instrumentGain * 0.60, 0.9);
        var leftInstrumentAir = BiquadFilter.Peaking(sampleRate, 5200, instrumentGain * 0.30, 0.8);

        var rightInstrumentBody = BiquadFilter.Peaking(sampleRate, 650, instrumentGain * 0.60, 0.9);
        var rightInstrumentAir = BiquadFilter.Peaking(sampleRate, 5200, instrumentGain * 0.30, 0.8);

        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            l = leftVocalPresence.Process(l);
            l = leftVocalBody.Process(l);
            l = leftDrumWeight.Process(l);
            l = leftDrumAttack.Process(l);
            l = leftInstrumentBody.Process(l);
            l = leftInstrumentAir.Process(l);
            left[i] = l;

            var r = right[i];
            r = rightVocalPresence.Process(r);
            r = rightVocalBody.Process(r);
            r = rightDrumWeight.Process(r);
            r = rightDrumAttack.Process(r);
            r = rightInstrumentBody.Process(r);
            r = rightInstrumentAir.Process(r);
            right[i] = r;
        }
    }
}
