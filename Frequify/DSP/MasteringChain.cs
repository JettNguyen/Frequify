using Frequify.DSP.Compression;
using Frequify.DSP.EQ;
using Frequify.DSP.Filters;
using Frequify.DSP.Limiter;
using Frequify.DSP.Loudness;
using Frequify.DSP.Saturation;
using Frequify.DSP.Stereo;
using Frequify.Models;

namespace Frequify.DSP;

/// <summary>
/// Applies the complete mastering chain in deterministic signal-flow order.
/// </summary>
public sealed class MasteringChain
{
    private readonly MasteringSettings _settings;

    /// <summary>
    /// Initializes a new mastering chain.
    /// </summary>
    /// <param name="settings">Mastering settings.</param>
    public MasteringChain(MasteringSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Gets latest low-band compression reduction in dB.
    /// </summary>
    public double LowBandGainReductionDb { get; private set; }

    /// <summary>
    /// Gets latest mid-band compression reduction in dB.
    /// </summary>
    public double MidBandGainReductionDb { get; private set; }

    /// <summary>
    /// Gets latest high-band compression reduction in dB.
    /// </summary>
    public double HighBandGainReductionDb { get; private set; }

    /// <summary>
    /// Processes an audio buffer according to enabled mastering stages.
    /// </summary>
    /// <param name="audio">Audio buffer to process.</param>
    /// <returns>Processed copy of the audio buffer.</returns>
    public AudioFileData Process(AudioFileData audio)
    {
        var output = audio.Clone();
        var left = output.Left.AsSpan();
        var right = output.Right.AsSpan();

        if (_settings.HighPass.IsEnabled)
        {
            new HighPassFilterProcessor(_settings.HighPass).Process(left, right, output.SampleRate);
        }

        if (_settings.Equalizer.IsEnabled)
        {
            new EqualizerProcessor(_settings.Equalizer).Process(left, right, output.SampleRate);
        }

        if (_settings.Compression.IsEnabled)
        {
            var compressor = new MultibandCompressorProcessor(_settings.Compression);
            compressor.Process(left, right, output.SampleRate);
            LowBandGainReductionDb = compressor.LowGainReductionDb;
            MidBandGainReductionDb = compressor.MidGainReductionDb;
            HighBandGainReductionDb = compressor.HighGainReductionDb;
        }

        if (_settings.Saturation.IsEnabled)
        {
            new SoftClipSaturationProcessor(_settings.Saturation).Process(left, right, output.SampleRate);
        }

        if (_settings.Stereo.IsEnabled)
        {
            new StereoImagerProcessor(_settings.Stereo).Process(left, right, output.SampleRate);
        }

        if (_settings.Limiter.IsEnabled)
        {
            new BrickwallLimiterProcessor(_settings.Limiter).Process(left, right, output.SampleRate);
        }

        if (_settings.Loudness.IsEnabled)
        {
            new LoudnessNormalizerProcessor(_settings.Loudness).Process(left, right, output.SampleRate);
            if (_settings.Limiter.IsEnabled)
            {
                new BrickwallLimiterProcessor(_settings.Limiter).Process(left, right, output.SampleRate);
            }
        }

        return output;
    }
}
