using Frequify.Models;

namespace Frequify.DSP.Analysis;

/// <summary>
/// Computes deterministic track analysis including loudness, peaks, dynamics, and spectrum.
/// </summary>
public sealed class AudioAnalyzer
{
    /// <summary>
    /// Analyzes stereo audio and returns key metrics.
    /// </summary>
    /// <param name="audioData">Audio data to analyze.</param>
    /// <returns>Computed analysis metrics.</returns>
    public AnalysisMetrics Analyze(AudioFileData audioData)
    {
        var sampleCount = Math.Min(audioData.Left.Length, audioData.Right.Length);
        if (sampleCount < 2)
        {
            return new AnalysisMetrics
            {
                IntegratedLufs = -70,
                TruePeakDbTp = -90,
                RmsDbFs = -90,
                CrestFactorDb = 0,
                Spectrum = new[] { 0.0 }
            };
        }

        var left = audioData.Left.AsSpan(0, sampleCount);
        var right = audioData.Right.AsSpan(0, sampleCount);

        var integratedLufs = LoudnessMeter.CalculateIntegratedLufs(left, right, audioData.SampleRate);

        var truePeak = EstimateTruePeak(left, right);
        var truePeakDbTp = 20.0 * Math.Log10(Math.Max(truePeak, 1e-9));

        double sumSquares = 0;
        double maxPeak = 0;
        for (var i = 0; i < left.Length; i++)
        {
            var l = left[i];
            var r = right[i];
            sumSquares += (l * l + r * r) * 0.5;
            maxPeak = Math.Max(maxPeak, Math.Abs(l));
            maxPeak = Math.Max(maxPeak, Math.Abs(r));
        }

        var rms = Math.Sqrt(sumSquares / Math.Max(left.Length, 1));
        var rmsDb = 20.0 * Math.Log10(Math.Max(rms, 1e-9));
        var crestDb = 20.0 * Math.Log10(Math.Max(maxPeak / Math.Max(rms, 1e-9), 1e-9));

        return new AnalysisMetrics
        {
            IntegratedLufs = integratedLufs,
            TruePeakDbTp = truePeakDbTp,
            RmsDbFs = rmsDb,
            CrestFactorDb = crestDb,
            Spectrum = ComputeSpectrum(left, right)
        };
    }

    private static double EstimateTruePeak(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        double peak = 0;
        for (var i = 0; i < left.Length - 1; i++)
        {
            var l0 = left[i];
            var l1 = left[i + 1];
            var r0 = right[i];
            var r1 = right[i + 1];

            for (var s = 0; s < 4; s++)
            {
                var t = s / 4.0;
                var li = l0 + (l1 - l0) * t;
                var ri = r0 + (r1 - r0) * t;
                peak = Math.Max(peak, Math.Abs(li));
                peak = Math.Max(peak, Math.Abs(ri));
            }
        }

        return peak;
    }

    private static IReadOnlyList<double> ComputeSpectrum(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
    {
        const int fftSize = 2048;
        var real = new double[fftSize];
        var imag = new double[fftSize];

        var start = Math.Max(0, left.Length / 2 - fftSize / 2);
        var available = Math.Min(fftSize, left.Length - start);

        for (var i = 0; i < available; i++)
        {
            var sample = (left[start + i] + right[start + i]) * 0.5;
            var window = 0.5 * (1 - Math.Cos(2 * Math.PI * i / (fftSize - 1)));
            real[i] = sample * window;
        }

        Radix2Fft.Execute(real, imag);
        var bins = fftSize / 2;
        var magnitudes = new double[bins];
        double max = 1e-9;

        for (var i = 0; i < bins; i++)
        {
            var mag = Math.Sqrt(real[i] * real[i] + imag[i] * imag[i]);
            magnitudes[i] = mag;
            max = Math.Max(max, mag);
        }

        var normalized = new double[128];
        for (var i = 0; i < normalized.Length; i++)
        {
            var index = (int)((i / (double)normalized.Length) * (bins - 1));
            var linear = magnitudes[index] / max;
            normalized[i] = Math.Clamp(linear, 0, 1);
        }

        return normalized;
    }
}
