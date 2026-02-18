using Frequify.Models;

namespace Frequify.DSP.Compression;

/// <summary>
/// Applies deterministic per-band dynamic range compression.
/// </summary>
public sealed class BandCompressor
{
    private readonly CompressionBandSettings _settings;
    private double _envelope;
    private double _gain = 1;

    /// <summary>
    /// Initializes a new band compressor.
    /// </summary>
    /// <param name="settings">Compressor settings.</param>
    public BandCompressor(CompressionBandSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets the most recent gain reduction in decibels.
    /// </summary>
    public double GainReductionDb { get; private set; }

    /// <summary>
    /// Processes a sample value.
    /// </summary>
    /// <param name="sample">Input sample.</param>
    /// <param name="sampleRate">Sample rate in hertz.</param>
    /// <returns>Compressed output sample.</returns>
    public float Process(float sample, int sampleRate)
    {
        var attackCoeff = Math.Exp(-1.0 / (Math.Max(_settings.AttackMs, 0.1) * 0.001 * sampleRate));
        var releaseCoeff = Math.Exp(-1.0 / (Math.Max(_settings.ReleaseMs, 1) * 0.001 * sampleRate));

        var inputAbs = Math.Abs(sample);
        _envelope = inputAbs > _envelope
            ? attackCoeff * _envelope + (1 - attackCoeff) * inputAbs
            : releaseCoeff * _envelope + (1 - releaseCoeff) * inputAbs;

        var inputDb = 20.0 * Math.Log10(Math.Max(_envelope, 1e-9));
        var threshold = _settings.ThresholdDb;
        var ratio = Math.Max(_settings.Ratio, 1.0);

        var outputDb = inputDb <= threshold ? inputDb : threshold + ((inputDb - threshold) / ratio);
        var requiredGainDb = outputDb - inputDb;
        var targetGain = Math.Pow(10.0, requiredGainDb / 20.0);

        _gain = targetGain < _gain
            ? attackCoeff * _gain + (1 - attackCoeff) * targetGain
            : releaseCoeff * _gain + (1 - releaseCoeff) * targetGain;

        GainReductionDb = -20.0 * Math.Log10(Math.Max(_gain, 1e-9));
        return (float)(sample * _gain);
    }
}
