using Frequify.Models;

namespace Frequify.DSP.Stereo;

/// <summary>
/// Adjusts stereo width with mono-compatibility-safe bounds.
/// </summary>
public sealed class StereoImagerProcessor : IAudioProcessor
{
    private readonly StereoImagerSettings _settings;

    /// <summary>
    /// Initializes a new stereo imager.
    /// </summary>
    /// <param name="settings">Stereo imaging settings.</param>
    public StereoImagerProcessor(StereoImagerSettings settings)
    {
        _settings = settings;
    }

    /// <inheritdoc/>
    public void Process(Span<float> left, Span<float> right, int sampleRate)
    {
        var width = Math.Clamp(_settings.Width, 0.7, 1.3);

        for (var i = 0; i < left.Length; i++)
        {
            var mid = (left[i] + right[i]) * 0.5f;
            var side = (left[i] - right[i]) * 0.5f;
            side *= (float)width;

            left[i] = mid + side;
            right[i] = mid - side;
        }
    }
}
