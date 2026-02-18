using Frequify.Models;
using NAudio.Wave;
using System.IO;

namespace Frequify.Audio;

/// <summary>
/// Loads supported audio files into deterministic floating-point stereo buffers.
/// </summary>
public sealed class AudioLoader
{
    /// <summary>
    /// Loads a WAV or MP3 file and returns stereo sample arrays.
    /// </summary>
    /// <param name="filePath">Absolute path to input audio file.</param>
    /// <returns>Decoded stereo audio data.</returns>
    /// <exception cref="InvalidOperationException">Thrown when format is unsupported.</exception>
    public AudioFileData LoadAudio(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new InvalidOperationException("The selected file does not exist.");
        }

        var extension = Path.GetExtension(filePath);
        if (!string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".mp3", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only WAV and MP3 files are supported. Please choose a .wav or .mp3 file.");
        }

        using var reader = new AudioFileReader(filePath);
        var inputRate = reader.WaveFormat.SampleRate;
        var channels = reader.WaveFormat.Channels;

        if (channels is not (1 or 2))
        {
            throw new InvalidOperationException("Only mono or stereo audio files are supported.");
        }

        var allSamples = new List<float>();
        var readBuffer = new float[Math.Max(4096, reader.WaveFormat.SampleRate * channels / 2)];
        int read;
        while ((read = reader.Read(readBuffer, 0, readBuffer.Length)) > 0)
        {
            allSamples.AddRange(readBuffer.Take(read));
        }

        if (channels == 1)
        {
            var mono = allSamples.ToArray();
            var left = new float[mono.Length];
            var right = new float[mono.Length];
            for (var i = 0; i < mono.Length; i++)
            {
                left[i] = mono[i];
                right[i] = mono[i];
            }

            var (resampledLeft, resampledRight, normalizedRate) = NormalizeSampleRate(left, right, inputRate);
            return new AudioFileData(resampledLeft, resampledRight, normalizedRate);
        }

        var frameCount = allSamples.Count / 2;
        var stereoLeft = new float[frameCount];
        var stereoRight = new float[frameCount];

        for (var i = 0; i < frameCount; i++)
        {
            stereoLeft[i] = allSamples[i * 2];
            stereoRight[i] = allSamples[i * 2 + 1];
        }

        var (normalizedLeft, normalizedRight, outputRate) = NormalizeSampleRate(stereoLeft, stereoRight, inputRate);
        return new AudioFileData(normalizedLeft, normalizedRight, outputRate);
    }

    private static (float[] Left, float[] Right, int SampleRate) NormalizeSampleRate(float[] left, float[] right, int inputRate)
    {
        if (inputRate is 44100 or 48000)
        {
            return (left, right, inputRate);
        }

        var targetRate = inputRate < 46000 ? 44100 : 48000;
        var ratio = targetRate / (double)inputRate;
        var outputLength = Math.Max(1, (int)Math.Round(left.Length * ratio));
        var outLeft = new float[outputLength];
        var outRight = new float[outputLength];

        for (var i = 0; i < outputLength; i++)
        {
            var sourcePos = i / ratio;
            var index0 = Math.Clamp((int)sourcePos, 0, left.Length - 1);
            var index1 = Math.Clamp(index0 + 1, 0, left.Length - 1);
            var t = sourcePos - index0;

            outLeft[i] = (float)(left[index0] + ((left[index1] - left[index0]) * t));
            outRight[i] = (float)(right[index0] + ((right[index1] - right[index0]) * t));
        }

        return (outLeft, outRight, targetRate);
    }
}
