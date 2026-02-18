using Frequify.Models;
using NAudio.Wave;

namespace Frequify.Audio;

/// <summary>
/// Exports processed stereo buffers to WAV files.
/// </summary>
public sealed class AudioExporter
{
    /// <summary>
    /// Writes stereo float WAV output.
    /// </summary>
    /// <param name="filePath">Destination file path.</param>
    /// <param name="audioData">Audio data to write.</param>
    public void ExportWav(string filePath, AudioFileData audioData)
    {
        var interleaved = new float[audioData.Length * 2];
        for (var i = 0; i < audioData.Length; i++)
        {
            interleaved[i * 2] = audioData.Left[i];
            interleaved[i * 2 + 1] = audioData.Right[i];
        }

        var waveFormat = WaveFormat.CreateIeeeFloatWaveFormat(audioData.SampleRate, 2);
        using var writer = new WaveFileWriter(filePath, waveFormat);
        writer.WriteSamples(interleaved, 0, interleaved.Length);
    }
}
