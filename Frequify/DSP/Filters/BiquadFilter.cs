namespace Frequify.DSP.Filters;

/// <summary>
/// Implements a deterministic direct-form I biquad filter.
/// </summary>
public sealed class BiquadFilter
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

    private BiquadFilter(double b0, double b1, double b2, double a1, double a2)
    {
        _b0 = b0;
        _b1 = b1;
        _b2 = b2;
        _a1 = a1;
        _a2 = a2;
    }

    /// <summary>
    /// Creates a high-pass biquad filter using RBJ coefficients.
    /// </summary>
    public static BiquadFilter HighPass(int sampleRate, double frequencyHz, double q = 0.707)
    {
        var w0 = 2.0 * Math.PI * frequencyHz / sampleRate;
        var alpha = Math.Sin(w0) / (2.0 * q);
        var cosw0 = Math.Cos(w0);

        var b0 = (1 + cosw0) / 2;
        var b1 = -(1 + cosw0);
        var b2 = (1 + cosw0) / 2;
        var a0 = 1 + alpha;
        var a1 = -2 * cosw0;
        var a2 = 1 - alpha;

        return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    /// <summary>
    /// Creates a low shelf filter using RBJ coefficients.
    /// </summary>
    public static BiquadFilter LowShelf(int sampleRate, double frequencyHz, double gainDb, double q)
    {
        return CreateShelf(sampleRate, frequencyHz, gainDb, q, true);
    }

    /// <summary>
    /// Creates a high shelf filter using RBJ coefficients.
    /// </summary>
    public static BiquadFilter HighShelf(int sampleRate, double frequencyHz, double gainDb, double q)
    {
        return CreateShelf(sampleRate, frequencyHz, gainDb, q, false);
    }

    /// <summary>
    /// Creates a peaking equalizer filter using RBJ coefficients.
    /// </summary>
    public static BiquadFilter Peaking(int sampleRate, double frequencyHz, double gainDb, double q)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = 2.0 * Math.PI * frequencyHz / sampleRate;
        var alpha = Math.Sin(w0) / (2.0 * q);
        var cosw0 = Math.Cos(w0);

        var b0 = 1 + alpha * a;
        var b1 = -2 * cosw0;
        var b2 = 1 - alpha * a;
        var a0 = 1 + alpha / a;
        var a1 = -2 * cosw0;
        var a2 = 1 - alpha / a;

        return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }

    /// <summary>
    /// Processes one sample.
    /// </summary>
    public float Process(float input)
    {
        var y = _b0 * input + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;

        _x2 = _x1;
        _x1 = input;
        _y2 = _y1;
        _y1 = y;

        return (float)y;
    }

    private static BiquadFilter CreateShelf(int sampleRate, double frequencyHz, double gainDb, double q, bool low)
    {
        var a = Math.Pow(10, gainDb / 40.0);
        var w0 = 2.0 * Math.PI * frequencyHz / sampleRate;
        var alpha = Math.Sin(w0) / (2.0 * q) * Math.Sqrt(a);
        var cosw0 = Math.Cos(w0);
        var beta = 2 * Math.Sqrt(a) * alpha;

        double b0;
        double b1;
        double b2;
        double a0;
        double a1;
        double a2;

        if (low)
        {
            b0 = a * ((a + 1) - (a - 1) * cosw0 + beta);
            b1 = 2 * a * ((a - 1) - (a + 1) * cosw0);
            b2 = a * ((a + 1) - (a - 1) * cosw0 - beta);
            a0 = (a + 1) + (a - 1) * cosw0 + beta;
            a1 = -2 * ((a - 1) + (a + 1) * cosw0);
            a2 = (a + 1) + (a - 1) * cosw0 - beta;
        }
        else
        {
            b0 = a * ((a + 1) + (a - 1) * cosw0 + beta);
            b1 = -2 * a * ((a - 1) + (a + 1) * cosw0);
            b2 = a * ((a + 1) + (a - 1) * cosw0 - beta);
            a0 = (a + 1) - (a - 1) * cosw0 + beta;
            a1 = 2 * ((a - 1) - (a + 1) * cosw0);
            a2 = (a + 1) - (a - 1) * cosw0 - beta;
        }

        return new BiquadFilter(b0 / a0, b1 / a0, b2 / a0, a1 / a0, a2 / a0);
    }
}
