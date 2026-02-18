namespace Frequify.Models;

/// <summary>
/// Represents deterministic analysis results for an audio file.
/// </summary>
public sealed class AnalysisMetrics
{
    /// <summary>
    /// Gets or sets integrated loudness in LUFS.
    /// </summary>
    public double IntegratedLufs { get; set; }

    /// <summary>
    /// Gets or sets the true peak value in dBTP.
    /// </summary>
    public double TruePeakDbTp { get; set; }

    /// <summary>
    /// Gets or sets RMS level in dBFS.
    /// </summary>
    public double RmsDbFs { get; set; }

    /// <summary>
    /// Gets or sets crest factor in dB.
    /// </summary>
    public double CrestFactorDb { get; set; }

    /// <summary>
    /// Gets or sets normalized spectrum magnitudes between 0 and 1.
    /// </summary>
    public IReadOnlyList<double> Spectrum { get; set; } = Array.Empty<double>();
}
