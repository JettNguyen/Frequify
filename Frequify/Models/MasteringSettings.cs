using Frequify.Infrastructure;

namespace Frequify.Models;

/// <summary>
/// Holds full mastering chain configuration with stage-level controls.
/// </summary>
public sealed class MasteringSettings : ObservableObject
{
    /// <summary>
    /// Initializes default conservative mastering settings.
    /// </summary>
    public MasteringSettings()
    {
        HighPass = new HighPassSettings();
        Equalizer = new EqualizerSettings();
        Compression = new MultibandCompressionSettings();
        Rebalance = new RebalanceSettings();
        Saturation = new SaturationSettings();
        Stereo = new StereoImagerSettings();
        Limiter = new LimiterSettings();
        Loudness = new LoudnessNormalizationSettings();
    }

    /// <summary>
    /// Gets high-pass filter settings.
    /// </summary>
    public HighPassSettings HighPass { get; }

    /// <summary>
    /// Gets equalizer settings.
    /// </summary>
    public EqualizerSettings Equalizer { get; }

    /// <summary>
    /// Gets multiband compression settings.
    /// </summary>
    public MultibandCompressionSettings Compression { get; }

    /// <summary>
    /// Gets pseudo rebalance settings.
    /// </summary>
    public RebalanceSettings Rebalance { get; }

    /// <summary>
    /// Gets saturation settings.
    /// </summary>
    public SaturationSettings Saturation { get; }

    /// <summary>
    /// Gets stereo imaging settings.
    /// </summary>
    public StereoImagerSettings Stereo { get; }

    /// <summary>
    /// Gets limiter settings.
    /// </summary>
    public LimiterSettings Limiter { get; }

    /// <summary>
    /// Gets loudness normalization settings.
    /// </summary>
    public LoudnessNormalizationSettings Loudness { get; }
}

public sealed class HighPassSettings : ObservableObject
{
    private bool _isEnabled = true;
    private double _cutoffHz = 30;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public double CutoffHz { get => _cutoffHz; set => SetProperty(ref _cutoffHz, value); }
}

public sealed class EqualizerSettings : ObservableObject
{
    private bool _isEnabled = true;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public EqBandSettings LowShelf { get; } = new(120, 0, 0.7);

    public EqBandSettings MidBell { get; } = new(1000, 0, 1.2);

    public EqBandSettings HighShelf { get; } = new(8000, 0, 0.7);
}

public sealed class EqBandSettings : ObservableObject
{
    private double _frequencyHz;
    private double _gainDb;
    private double _q;

    public EqBandSettings(double frequencyHz, double gainDb, double q)
    {
        _frequencyHz = frequencyHz;
        _gainDb = gainDb;
        _q = q;
    }

    public double FrequencyHz { get => _frequencyHz; set => SetProperty(ref _frequencyHz, value); }

    public double GainDb { get => _gainDb; set => SetProperty(ref _gainDb, value); }

    public double Q { get => _q; set => SetProperty(ref _q, value); }
}

public sealed class MultibandCompressionSettings : ObservableObject
{
    private bool _isEnabled = true;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public CompressionBandSettings Low { get; } = new(-22, 2.0, 20, 150);

    public CompressionBandSettings Mid { get; } = new(-20, 1.8, 15, 120);

    public CompressionBandSettings High { get; } = new(-18, 1.6, 5, 80);

    public double LowCrossoverHz { get; set; } = 180;

    public double HighCrossoverHz { get; set; } = 3500;
}

public sealed class CompressionBandSettings : ObservableObject
{
    private double _thresholdDb;
    private double _ratio;
    private double _attackMs;
    private double _releaseMs;

    public CompressionBandSettings(double thresholdDb, double ratio, double attackMs, double releaseMs)
    {
        _thresholdDb = thresholdDb;
        _ratio = ratio;
        _attackMs = attackMs;
        _releaseMs = releaseMs;
    }

    public double ThresholdDb { get => _thresholdDb; set => SetProperty(ref _thresholdDb, value); }

    public double Ratio { get => _ratio; set => SetProperty(ref _ratio, value); }

    public double AttackMs { get => _attackMs; set => SetProperty(ref _attackMs, value); }

    public double ReleaseMs { get => _releaseMs; set => SetProperty(ref _releaseMs, value); }
}

public sealed class SaturationSettings : ObservableObject
{
    private bool _isEnabled = true;
    private double _drive = 0.15;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public double Drive { get => _drive; set => SetProperty(ref _drive, value); }
}

public sealed class RebalanceSettings : ObservableObject
{
    private bool _isEnabled = true;
    private double _vocalGainDb;
    private double _drumGainDb;
    private double _instrumentGainDb;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public double VocalGainDb { get => _vocalGainDb; set => SetProperty(ref _vocalGainDb, value); }

    public double DrumGainDb { get => _drumGainDb; set => SetProperty(ref _drumGainDb, value); }

    public double InstrumentGainDb { get => _instrumentGainDb; set => SetProperty(ref _instrumentGainDb, value); }
}

public sealed class StereoImagerSettings : ObservableObject
{
    private bool _isEnabled = true;
    private double _width = 1.0;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public double Width { get => _width; set => SetProperty(ref _width, value); }
}

public sealed class LimiterSettings : ObservableObject
{
    private bool _isEnabled = true;
    private double _ceilingDbTp = -1.0;
    private double _lookaheadMs = 3.0;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public double CeilingDbTp { get => _ceilingDbTp; set => SetProperty(ref _ceilingDbTp, value); }

    public double LookaheadMs { get => _lookaheadMs; set => SetProperty(ref _lookaheadMs, value); }
}

public sealed class LoudnessNormalizationSettings : ObservableObject
{
    private bool _isEnabled = true;
    private int _targetLufs = -14;

    public bool IsEnabled { get => _isEnabled; set => SetProperty(ref _isEnabled, value); }

    public int TargetLufs { get => _targetLufs; set => SetProperty(ref _targetLufs, value); }
}
