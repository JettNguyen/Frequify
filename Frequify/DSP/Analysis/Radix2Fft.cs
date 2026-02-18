namespace Frequify.DSP.Analysis;

/// <summary>
/// Provides deterministic radix-2 FFT operations for spectrum analysis.
/// </summary>
public static class Radix2Fft
{
    /// <summary>
    /// Executes in-place FFT on real and imaginary arrays.
    /// </summary>
    /// <param name="real">Real values.</param>
    /// <param name="imag">Imaginary values.</param>
    public static void Execute(double[] real, double[] imag)
    {
        var n = real.Length;
        var bits = (int)Math.Log2(n);

        for (var i = 0; i < n; i++)
        {
            var j = ReverseBits(i, bits);
            if (j <= i)
            {
                continue;
            }

            (real[i], real[j]) = (real[j], real[i]);
            (imag[i], imag[j]) = (imag[j], imag[i]);
        }

        for (var size = 2; size <= n; size <<= 1)
        {
            var half = size / 2;
            var theta = -2.0 * Math.PI / size;

            for (var i = 0; i < n; i += size)
            {
                for (var j = 0; j < half; j++)
                {
                    var index1 = i + j;
                    var index2 = index1 + half;

                    var wr = Math.Cos(theta * j);
                    var wi = Math.Sin(theta * j);

                    var tr = wr * real[index2] - wi * imag[index2];
                    var ti = wr * imag[index2] + wi * real[index2];

                    real[index2] = real[index1] - tr;
                    imag[index2] = imag[index1] - ti;
                    real[index1] += tr;
                    imag[index1] += ti;
                }
            }
        }
    }

    private static int ReverseBits(int value, int bits)
    {
        var reversed = 0;
        for (var i = 0; i < bits; i++)
        {
            reversed = (reversed << 1) | (value & 1);
            value >>= 1;
        }

        return reversed;
    }
}
