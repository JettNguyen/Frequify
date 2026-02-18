namespace Frequify.Models;

/// <summary>
/// Provides built-in genre presets.
/// </summary>
public static class GenrePresets
{
    /// <summary>
    /// Gets the default preset collection.
    /// </summary>
    /// <returns>Preset list.</returns>
    public static IReadOnlyList<GenrePreset> GetDefaults()
    {
        return new List<GenrePreset>
        {
            new(
                "Auto",
                "Analyzes the loaded track and automatically tunes mastering controls based on loudness, peaks, dynamics, and spectrum.",
                _ => { }),
            new(
                "Pop",
                "Balanced clarity with controlled low end and modern loudness.",
                settings =>
                {
                    settings.Equalizer.LowShelf.GainDb = 1.5;
                    settings.Equalizer.MidBell.GainDb = 1.0;
                    settings.Equalizer.HighShelf.GainDb = 2.0;
                    settings.Compression.Low.Ratio = 2.2;
                    settings.Compression.Mid.Ratio = 2.0;
                    settings.Compression.High.Ratio = 1.8;
                    settings.Saturation.Drive = 0.12;
                    settings.Loudness.TargetLufs = -14;
                }),
            new(
                "Hip-Hop",
                "Punchy low end and vocal presence with controlled highs.",
                settings =>
                {
                    settings.Equalizer.LowShelf.GainDb = 2.5;
                    settings.Equalizer.MidBell.GainDb = 0.5;
                    settings.Equalizer.HighShelf.GainDb = 1.0;
                    settings.Compression.Low.Ratio = 2.8;
                    settings.Compression.Mid.Ratio = 2.0;
                    settings.Compression.High.Ratio = 1.5;
                    settings.Saturation.Drive = 0.18;
                    settings.Loudness.TargetLufs = -12;
                }),
            new(
                "EDM",
                "Tight low frequencies, bright top end, and higher loudness impact.",
                settings =>
                {
                    settings.Equalizer.LowShelf.GainDb = 3.0;
                    settings.Equalizer.MidBell.GainDb = -0.5;
                    settings.Equalizer.HighShelf.GainDb = 2.8;
                    settings.Compression.Low.Ratio = 3.2;
                    settings.Compression.Mid.Ratio = 2.5;
                    settings.Compression.High.Ratio = 2.0;
                    settings.Saturation.Drive = 0.2;
                    settings.Loudness.TargetLufs = -9;
                }),
            new(
                "Rock",
                "Keeps dynamics while adding mid clarity and glue.",
                settings =>
                {
                    settings.Equalizer.LowShelf.GainDb = 1.0;
                    settings.Equalizer.MidBell.GainDb = 1.8;
                    settings.Equalizer.HighShelf.GainDb = 1.2;
                    settings.Compression.Low.Ratio = 2.4;
                    settings.Compression.Mid.Ratio = 2.2;
                    settings.Compression.High.Ratio = 1.7;
                    settings.Saturation.Drive = 0.14;
                    settings.Loudness.TargetLufs = -12;
                }),
            new(
                "Acoustic",
                "Natural tone with light control and preserved dynamics.",
                settings =>
                {
                    settings.Equalizer.LowShelf.GainDb = -0.8;
                    settings.Equalizer.MidBell.GainDb = 0.6;
                    settings.Equalizer.HighShelf.GainDb = 1.0;
                    settings.Compression.Low.Ratio = 1.8;
                    settings.Compression.Mid.Ratio = 1.6;
                    settings.Compression.High.Ratio = 1.4;
                    settings.Saturation.Drive = 0.08;
                    settings.Loudness.TargetLufs = -16;
                })
        };
    }
}
