using Frequify.DSP.Compression;
using Frequify.DSP.EQ;
using Frequify.DSP.Filters;
using Frequify.DSP.Limiter;
using Frequify.DSP.Loudness;
using Frequify.DSP.Rebalance;
using Frequify.DSP.Saturation;
using Frequify.DSP.Stereo;
using Frequify.Models;

namespace Frequify.DSP;

/// <summary>
/// Applies the complete mastering chain in deterministic signal-flow order.
/// </summary>
public sealed class MasteringChain
{
    public readonly record struct StageUpdate(double Percent, string Message);

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
    /// <param name="progress">Optional progress callback for stage updates.</param>
    /// <returns>Processed copy of the audio buffer.</returns>
    public AudioFileData Process(AudioFileData audio, IProgress<StageUpdate>? progress = null)
    {
        progress?.Report(new StageUpdate(34, "Preparing signal buffers..."));
        var output = audio.Clone();
        var left = output.Left.AsSpan();
        var right = output.Right.AsSpan();

        if (_settings.HighPass.IsEnabled)
        {
            progress?.Report(new StageUpdate(40, "Applying high-pass cleanup..."));
            new HighPassFilterProcessor(_settings.HighPass).Process(left, right, output.SampleRate);
        }

        if (_settings.Equalizer.IsEnabled)
        {
            progress?.Report(new StageUpdate(46, "Applying tonal EQ shaping..."));
            new EqualizerProcessor(_settings.Equalizer).Process(left, right, output.SampleRate);
        }

        if (_settings.Rebalance.IsEnabled)
        {
            progress?.Report(new StageUpdate(52, "Applying pseudo rebalance weighting..."));
            new PseudoRebalanceProcessor(_settings.Rebalance).Process(left, right, output.SampleRate);
        }

        if (_settings.Compression.IsEnabled)
        {
            progress?.Report(new StageUpdate(58, "Applying multiband compression..."));
            var compressor = new MultibandCompressorProcessor(_settings.Compression);
            compressor.Process(left, right, output.SampleRate);
            LowBandGainReductionDb = compressor.LowGainReductionDb;
            MidBandGainReductionDb = compressor.MidGainReductionDb;
            HighBandGainReductionDb = compressor.HighGainReductionDb;
        }

        if (_settings.Saturation.IsEnabled)
        {
            progress?.Report(new StageUpdate(64, "Applying saturation color..."));
            new SoftClipSaturationProcessor(_settings.Saturation).Process(left, right, output.SampleRate);
        }

        if (_settings.Stereo.IsEnabled)
        {
            progress?.Report(new StageUpdate(68, "Applying stereo image stage..."));
            new StereoImagerProcessor(_settings.Stereo).Process(left, right, output.SampleRate);
        }

        if (_settings.Limiter.IsEnabled)
        {
            progress?.Report(new StageUpdate(72, "Applying limiter protection..."));
            new BrickwallLimiterProcessor(_settings.Limiter).Process(left, right, output.SampleRate);
        }

        if (_settings.Loudness.IsEnabled)
        {
            progress?.Report(new StageUpdate(76, "Applying loudness normalization..."));
            new LoudnessNormalizerProcessor(_settings.Loudness).Process(left, right, output.SampleRate);
            if (_settings.Limiter.IsEnabled)
            {
                progress?.Report(new StageUpdate(80, "Running final limiter safety pass..."));
                new BrickwallLimiterProcessor(_settings.Limiter).Process(left, right, output.SampleRate);
            }
        }

        progress?.Report(new StageUpdate(84, "Mastering chain complete. Finalizing output..."));

        return output;
    }
}
