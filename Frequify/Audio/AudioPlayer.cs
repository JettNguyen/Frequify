using Frequify.Models;
using NAudio.Wave;
using System.IO;

namespace Frequify.Audio;

/// <summary>
/// Provides offline playback preview for stereo float audio buffers.
/// </summary>
public sealed class AudioPlayer : IDisposable
{
    private WaveOutEvent? _waveOut;
    private RawSourceWaveStream? _sourceStream;
    private MemoryStream? _pcmStream;
    private float _volume = 0.9f;

    /// <summary>
    /// Gets current playback duration in seconds.
    /// </summary>
    public double DurationSeconds => _sourceStream?.TotalTime.TotalSeconds ?? 0;

    /// <summary>
    /// Gets current playback position in seconds.
    /// </summary>
    public double PositionSeconds => _sourceStream?.CurrentTime.TotalSeconds ?? 0;

    /// <summary>
    /// Gets current playback state.
    /// </summary>
    public PlaybackState PlaybackState => _waveOut?.PlaybackState ?? PlaybackState.Stopped;

    /// <summary>
    /// Loads a stereo buffer into the player.
    /// </summary>
    /// <param name="audioData">Audio data to load.</param>
    /// <returns>True when load succeeds.</returns>
    public bool Load(AudioFileData audioData)
    {
        try
        {
            ArgumentNullException.ThrowIfNull(audioData);

            Stop();

            var sampleCount = Math.Min(audioData.Left.Length, audioData.Right.Length);
            if (sampleCount < 2)
            {
                return false;
            }

            var interleaved = new float[sampleCount * 2];
            for (var i = 0; i < sampleCount; i++)
            {
                interleaved[i * 2] = audioData.Left[i];
                interleaved[i * 2 + 1] = audioData.Right[i];
            }

            var sampleRate = audioData.SampleRate > 0 ? audioData.SampleRate : 44100;
            var waveFormat = WaveFormat.CreateCustomFormat(WaveFormatEncoding.Pcm, sampleRate, 2, sampleRate * 4, 4, 16);
            var bytes = new byte[interleaved.Length * sizeof(short)];
            for (var i = 0; i < interleaved.Length; i++)
            {
                var clamped = Math.Clamp(interleaved[i], -1.0f, 1.0f);
                var sample = (short)(clamped * short.MaxValue);
                var byteIndex = i * 2;
                bytes[byteIndex] = (byte)(sample & 0xFF);
                bytes[byteIndex + 1] = (byte)((sample >> 8) & 0xFF);
            }

            _pcmStream = new MemoryStream(bytes, writable: false);
            _sourceStream = new RawSourceWaveStream(_pcmStream, waveFormat);

            _waveOut = new WaveOutEvent
            {
                DesiredLatency = 150,
                NumberOfBuffers = 3
            };
            _waveOut.PlaybackStopped += OnPlaybackStopped;
            _waveOut.Init(_sourceStream);
            _waveOut.Volume = _volume;
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    /// <summary>
    /// Starts or resumes playback.
    /// </summary>
    /// <returns>True when playback starts.</returns>
    public bool Play()
    {
        if (_waveOut is null || _sourceStream is null)
        {
            return false;
        }

        try
        {
            _waveOut.Play();
            return true;
        }
        catch
        {
            Stop();
            return false;
        }
    }

    /// <summary>
    /// Pauses playback.
    /// </summary>
    public void Pause()
    {
        if (_waveOut is null)
        {
            return;
        }

        if (_waveOut.PlaybackState == PlaybackState.Playing)
        {
            _waveOut.Pause();
        }
    }

    /// <summary>
    /// Restarts playback from the beginning and starts playing.
    /// </summary>
    /// <returns>True when restart succeeds.</returns>
    public bool Restart()
    {
        if (_sourceStream is null)
        {
            return false;
        }

        Seek(0);
        return Play();
    }

    /// <summary>
    /// Seeks to a target position.
    /// </summary>
    /// <param name="seconds">Target position in seconds.</param>
    public void Seek(double seconds)
    {
        if (_sourceStream is null)
        {
            return;
        }

        var duration = Math.Max(DurationSeconds, 0.001);
        var clamped = Math.Clamp(seconds, 0, duration);
        _sourceStream.CurrentTime = TimeSpan.FromSeconds(clamped);
    }

    /// <summary>
    /// Sets output volume.
    /// </summary>
    /// <param name="volume">Linear volume from 0 to 1.</param>
    public void SetVolume(float volume)
    {
        _volume = Math.Clamp(volume, 0f, 1f);
        if (_waveOut is not null)
        {
            _waveOut.Volume = _volume;
        }
    }

    /// <summary>
    /// Stops current playback if running.
    /// </summary>
    public void Stop()
    {
        if (_waveOut is not null)
        {
            _waveOut.PlaybackStopped -= OnPlaybackStopped;
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }

        _sourceStream?.Dispose();
        _sourceStream = null;
        _pcmStream?.Dispose();
        _pcmStream = null;
    }

    private void OnPlaybackStopped(object? sender, StoppedEventArgs e)
    {
        if (_waveOut is null)
        {
            return;
        }

        _waveOut.PlaybackStopped -= OnPlaybackStopped;
        _waveOut.Dispose();
        _waveOut = null;
        _sourceStream?.Dispose();
        _sourceStream = null;
        _pcmStream?.Dispose();
        _pcmStream = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Stop();
    }

}
