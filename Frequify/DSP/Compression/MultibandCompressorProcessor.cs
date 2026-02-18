using Frequify.Models;

namespace Frequify.DSP.Compression;

/// <summary>
/// Applies deterministic three-band compression with conservative defaults.
/// </summary>
public sealed class MultibandCompressorProcessor : IAudioProcessor
{
    private readonly MultibandCompressionSettings _settings;
    private readonly BandCompressor _lowBand;
    private readonly BandCompressor _midBand;
    private readonly BandCompressor _highBand;

    /// <summary>
    /// Initializes a new multiband compressor.
    /// </summary>
    /// <param name="settings">Multiband settings.</param>
    public MultibandCompressorProcessor(MultibandCompressionSettings settings)
    {
        _settings = settings;
        _lowBand = new BandCompressor(settings.Low);
        _midBand = new BandCompressor(settings.Mid);
        _highBand = new BandCompressor(settings.High);
    }

    /// <summary>
    /// Gets low-band gain reduction in dB.
    /// </summary>
    public double LowGainReductionDb => _lowBand.GainReductionDb;

    /// <summary>
    /// Gets mid-band gain reduction in dB.
    /// </summary>
    public double MidGainReductionDb => _midBand.GainReductionDb;

    /// <summary>
    /// Gets high-band gain reduction in dB.
    /// </summary>
    public double HighGainReductionDb => _highBand.GainReductionDb;

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var lowCut = Math.Clamp(_settings.LowCrossoverHz, 80, 400);
        var highCut = Math.Clamp(_settings.HighCrossoverHz, 1500, 8000);

        var lowL = new OnePoleLowPass(sampleRate, lowCut);
        var lowR = new OnePoleLowPass(sampleRate, lowCut);

        var highLpL = new OnePoleLowPass(sampleRate, highCut);
        var highLpR = new OnePoleLowPass(sampleRate, highCut);

        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            var r = right[i];

            var lowLVal = lowL.Process(l);
            var lowRVal = lowR.Process(r);

            var highLVal = l - highLpL.Process(l);
            var highRVal = r - highLpR.Process(r);

            var midLVal = l - lowLVal - highLVal;
            var midRVal = r - lowRVal - highRVal;

            lowLVal = _lowBand.Process(lowLVal, sampleRate);
            lowRVal = _lowBand.Process(lowRVal, sampleRate);

            midLVal = _midBand.Process(midLVal, sampleRate);
            midRVal = _midBand.Process(midRVal, sampleRate);

            highLVal = _highBand.Process(highLVal, sampleRate);
            highRVal = _highBand.Process(highRVal, sampleRate);

            left[i] = lowLVal + midLVal + highLVal;
            right[i] = lowRVal + midRVal + highRVal;
        }
    }

    private sealed class OnePoleLowPass
    {
        private readonly double _a;
        private double _z;

        public OnePoleLowPass(int sampleRate, double cutoffHz)
        {
            var x = Math.Exp(-2.0 * Math.PI * cutoffHz / sampleRate);
            _a = 1 - x;
        }

        public float Process(float sample)
        {
            _z += _a * (sample - _z);
            return (float)_z;
        }
    }
}
