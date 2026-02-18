namespace Frequify.DSP.Analysis;

/// <summary>
/// Calculates integrated LUFS using EBU R128-style block gating.
/// </summary>
public static class LoudnessMeter
{
    /// <summary>
    /// Calculates integrated loudness in LUFS.
    /// </summary>
    /// <param name="left">Left channel samples.</param>
    /// <param name="right">Right channel samples.</param>
    /// <param name="sampleRate">Sample rate in hertz.</param>
    /// <returns>Integrated LUFS.</returns>
    public static double CalculateIntegratedLufs(ReadOnlySpan<float> left, ReadOnlySpan<float> right, int sampleRate)
    {
        var kLeft = ApplyKWeighting(left, sampleRate);
        var kRight = ApplyKWeighting(right, sampleRate);

        var blockLength = (int)(0.4 * sampleRate);
        var hop = (int)(0.1 * sampleRate);
        if (blockLength <= 0 || left.Length < blockLength)
        {
            return -70;
        }

        var blockLoudness = new List<double>();
        for (var start = 0; start + blockLength <= left.Length; start += hop)
        {
            double sum = 0;
            for (var i = 0; i < blockLength; i++)
            {
                var l = kLeft[start + i];
                var r = kRight[start + i];
                sum += (l * l + r * r) * 0.5;
            }

            var meanSquare = sum / blockLength;
            var lufs = -0.691 + 10.0 * Math.Log10(Math.Max(meanSquare, 1e-12));
            blockLoudness.Add(lufs);
        }

        var absoluteGated = blockLoudness.Where(v => v > -70).ToList();
        if (absoluteGated.Count == 0)
        {
            return -70;
        }

        var absPower = absoluteGated.Select(DbToPower).Average();
        var absIntegrated = PowerToLufs(absPower);
        var relativeThreshold = absIntegrated - 10.0;

        var relativeGated = absoluteGated.Where(v => v >= relativeThreshold).ToList();
        if (relativeGated.Count == 0)
        {
            return absIntegrated;
        }

        var finalPower = relativeGated.Select(DbToPower).Average();
        return PowerToLufs(finalPower);
    }

    private static double[] ApplyKWeighting(ReadOnlySpan<float> input, int sampleRate)
    {
        var stage1 = new double[input.Length];
        var stage2 = new double[input.Length];

        var hp = new Biquad(1.53512485958697, -2.69169618940638, 1.19839281085285, -1.69065929318241, 0.73248077421585);
        var shelf = new Biquad(1.0, -2.0, 1.0, -1.99004745483398, 0.99007225036621);

        for (var i = 0; i < input.Length; i++)
        {
            stage1[i] = hp.Process(input[i]);
            stage2[i] = shelf.Process(stage1[i]);
        }

        if (sampleRate == 48000)
        {
            return stage2;
        }

        var ratio = 48000.0 / sampleRate;
        var resampled = new double[input.Length];
        for (var i = 0; i < resampled.Length; i++)
        {
            var src = i * ratio;
            var i0 = Math.Min((int)src, stage2.Length - 1);
            var i1 = Math.Min(i0 + 1, stage2.Length - 1);
            var t = src - i0;
            resampled[i] = stage2[i0] + (stage2[i1] - stage2[i0]) * t;
        }

        return resampled;
    }

    private static double DbToPower(double lufs) => Math.Pow(10.0, (lufs + 0.691) / 10.0);

    private static double PowerToLufs(double power) => -0.691 + 10.0 * Math.Log10(Math.Max(power, 1e-12));

    private sealed class Biquad
    {
        private readonly double _b0;
        private readonly double _b1;
        private readonly double _b2;
        private readonly double _a1;
        private readonly double _a2;

        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        public Biquad(double b0, double b1, double b2, double a1, double a2)
        {
            _b0 = b0;
            _b1 = b1;
            _b2 = b2;
            _a1 = a1;
            _a2 = a2;
        }

        public double Process(double x)
        {
            var y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return y;
        }
    }
}
