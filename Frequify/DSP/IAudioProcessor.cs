namespace Frequify.DSP;

/// <summary>
/// Defines in-place deterministic stereo audio processing.
/// </summary>
public interface IAudioProcessor
{
    /// <summary>
    /// Processes stereo buffers in place.
    /// </summary>
    /// <param name="left">Left channel sample span.</param>
    /// <param name="right">Right channel sample span.</param>
    /// <param name="sampleRate">Sample rate in hertz.</param>
    void Process(Span<float> left, Span<float> right, int sampleRate);
}
