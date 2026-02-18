namespace Frequify.Models;

/// <summary>
/// Represents decoded stereo audio sample data.
/// </summary>
public sealed class AudioFileData
{
    /// <summary>
    /// Initializes a new audio data container.
    /// </summary>
    /// <param name="left">Left channel samples.</param>
    /// <param name="right">Right channel samples.</param>
    /// <param name="sampleRate">Sample rate in hertz.</param>
    public AudioFileData(float[] left, float[] right, int sampleRate)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var commonLength = Math.Min(left.Length, right.Length);
        if (commonLength == left.Length && commonLength == right.Length)
        {
            Left = left;
            Right = right;
        }
        else
        {
            Left = new float[commonLength];
            Right = new float[commonLength];
            Array.Copy(left, Left, commonLength);
            Array.Copy(right, Right, commonLength);
        }

        SampleRate = sampleRate;
    }

    /// <summary>
    /// Gets the left channel samples.
    /// </summary>
    public float[] Left { get; }

    /// <summary>
    /// Gets the right channel samples.
    /// </summary>
    public float[] Right { get; }

    /// <summary>
    /// Gets the sample rate in hertz.
    /// </summary>
    public int SampleRate { get; }

    /// <summary>
    /// Gets the number of samples per channel.
    /// </summary>
    public int Length => Left.Length;

    /// <summary>
    /// Creates a deep copy of the audio buffer.
    /// </summary>
    /// <returns>Copied stereo audio data.</returns>
    public AudioFileData Clone()
    {
        return new AudioFileData((float[])Left.Clone(), (float[])Right.Clone(), SampleRate);
    }
}
