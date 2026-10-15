using System.Text;
using MorseCore.AppCore.Radio;
using MorseCore.Interop;

namespace MorseCore.AppCore.Decoding;

public sealed record DecoderOptions(double? FrequencyHz = null, double? WordsPerMinute = null, double Threshold = .35, MorseAlphabet Alphabet = MorseAlphabet.Latin);
public sealed record DecoderResult(string Text, string Morse, double FrequencyHz, double WordsPerMinute, int UncertainCharacters, bool TimingAmbiguous, double DurationSeconds);

/// <summary>Offline narrow-band CW decoder for a single clean keyed tone, not voice or arbitrary radio mixtures.</summary>
public static class MorseDecoder
{
    public static Task<DecoderResult> DecodeFileAsync(string path, DecoderOptions? options = null, CancellationToken cancellationToken = default) =>
        Task.Run(() => Decode(WaveAudio.Read(path, cancellationToken), options, cancellationToken), cancellationToken);

    public static DecoderResult Decode(WaveAudio audio, DecoderOptions? options = null, CancellationToken cancellationToken = default)
    {
        options ??= new();
        if (options.FrequencyHz is { } frequency && (!double.IsFinite(frequency) || frequency is < 200 or > 2000) ||
            options.WordsPerMinute is { } speed && (!double.IsFinite(speed) || speed is < 5 or > 60) ||
            !double.IsFinite(options.Threshold) || options.Threshold is < .1 or > .7 || !Enum.IsDefined(options.Alphabet))
            throw new ArgumentOutOfRangeException(nameof(options));
        if (audio.SampleRate != WaveAudio.AnalysisRate || audio.Samples.Length > audio.SampleRate * WaveAudio.MaxSeconds) throw new ArgumentException("Unsupported analysis signal.");
        cancellationToken.ThrowIfCancellationRequested();
        var samples = audio.Samples;
        if (samples.Length < 160 || samples.Any(v => !float.IsFinite(v))) throw new InvalidDataException("decoderNoSignal");
        double energy = 0;
        foreach (float value in samples) energy += value * value;
        if (energy / samples.Length < 1e-10) throw new InvalidDataException("decoderNoSignal");
        var windows = LoudWindows(samples);
        if (windows.Length == 0) throw new InvalidDataException("decoderNoTone");
        double tone = options.FrequencyHz ?? FindTone(samples, windows, cancellationToken);
        double purity = windows.Average(start => TonePower(samples, start, tone));
        if (purity < .20) throw new InvalidDataException("decoderNoTone");
        float[] envelope = Envelope(samples, tone, cancellationToken);
        float peak = envelope.Max();
        if (peak < .00002) throw new InvalidDataException("decoderNoSignal");
        var runs = Runs(envelope, peak * options.Threshold, peak * options.Threshold * .7);
        var marks = runs.Where(r => r.On).Select(r => r.Duration).ToArray();
        if (marks.Length < 2) throw new InvalidDataException("decoderNotMorse");
        double unit = options.WordsPerMinute is { } wpm ? 1200 / wpm : EstimateUnit(marks);
        if (options.WordsPerMinute is null)
        {
            // The envelope shifts on/off edges in opposite directions. Onset-to-onset timing cancels that bias.
            var intervals = new List<double>();
            for (int i = 0; i + 1 < runs.Count; i++)
            {
                if (!runs[i].On || runs[i + 1].On || runs[i + 1].Duration / unit is < .5 or > 1.8) continue;
                intervals.Add((runs[i].Duration + runs[i + 1].Duration) / (runs[i].Duration / unit >= 2 ? 4 : 2));
            }
            if (intervals.Count > 0) { intervals.Sort(); unit = intervals[intervals.Count / 2]; }
        }
        if (unit is < 17 or > 260) throw new InvalidDataException("decoderNotMorse");
        var alphabet = MorseEngine.Alphabet(options.Alphabet).GroupBy(e => e.Code).ToDictionary(g => g.Key, g => g.First().Character.ToString());
        foreach (var pair in CwMessages.Prosigns) if (!alphabet.ContainsKey(pair.Value)) alphabet[pair.Value] = pair.Key;
        var text = new StringBuilder(); var morse = new StringBuilder(); var code = new StringBuilder();
        bool uncertain = false; int unknowns = 0, badMarks = 0;
        void Finish()
        {
            if (code.Length == 0) return;
            if (morse.Length > 0 && morse[^1] != ' ') morse.Append(' ');
            morse.Append(code);
            if (!uncertain && alphabet.TryGetValue(code.ToString(), out string? value)) text.Append(value);
            else { text.Append('□'); unknowns++; }
            code.Clear(); uncertain = false;
        }
        foreach (var run in runs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            double units = run.Duration / unit;
            if (run.On)
            {
                bool dash = units >= 2;
                code.Append(dash ? '-' : '.');
                if (Math.Abs(units - (dash ? 3 : 1)) > (dash ? .85 : .55)) { uncertain = true; badMarks++; }
                if (code.Length > 12) { uncertain = true; Finish(); }
            }
            else if (units >= 2)
            {
                Finish();
                if (units >= 5 && text.Length > 0 && text[^1] != ' ') { text.Append(' '); morse.Append(" / "); }
            }
            else if (units < .40 || units > 1.65) uncertain = true;
        }
        Finish();
        if (badMarks > marks.Length / 2 || text.Length == 0) throw new InvalidDataException("decoderNotMorse");
        bool ambiguous = options.WordsPerMinute is null && !marks.Any(d => d / unit > 2);
        if (ambiguous)
        {
            // Equal-length marks alone cannot tell dots from dashes without a known speed.
            // Character boundaries can also be ambiguous, so do not invent a character count.
            text.Clear(); text.Append('□'); unknowns = 1;
            morse.Clear(); morse.Append('?');
        }
        return new(text.ToString().Trim(), morse.ToString().Trim(), tone, 1200 / unit, unknowns, ambiguous, audio.Duration.TotalSeconds);
    }

    private static int[] LoudWindows(float[] samples)
    {
        const int length = 512;
        var windows = new List<(int Start, double Energy)>();
        for (int start = 0; start + length <= samples.Length; start += length)
        {
            double energy = 0;
            for (int i = start; i < start + length; i++) energy += samples[i] * samples[i];
            windows.Add((start, energy));
        }
        return windows.OrderByDescending(w => w.Energy).Take(8).Select(w => w.Start).ToArray();
    }
    private static double FindTone(float[] samples, int[] windows, CancellationToken token)
    {
        if (windows.Length == 0) throw new InvalidDataException("decoderNoTone");
        double best = 0, bestFrequency = 0;
        for (int frequency = 200; frequency <= 2000; frequency += 10)
        {
            token.ThrowIfCancellationRequested();
            double power = windows.Average(start => TonePower(samples, start, frequency));
            if (power > best) { best = power; bestFrequency = frequency; }
        }
        int center = (int)bestFrequency;
        for (int frequency = Math.Max(200, center - 10); frequency <= Math.Min(2000, center + 10); frequency++)
        {
            double power = windows.Average(start => TonePower(samples, start, frequency));
            if (power > best) { best = power; bestFrequency = frequency; }
        }
        return bestFrequency;
    }
    private static double TonePower(float[] samples, int start, double frequency)
    {
        const int count = 512;
        double coefficient = 2 * Math.Cos(2 * Math.PI * frequency / WaveAudio.AnalysisRate);
        double previous = 0, before = 0, energy = 0;
        for (int i = start; i < start + count; i++)
        {
            double value = samples[i] + coefficient * previous - before;
            before = previous; previous = value; energy += samples[i] * samples[i];
        }
        return energy > 1e-15 ? Math.Max(0, previous * previous + before * before - coefficient * previous * before) * 2 / (count * energy) : 0;
    }
    private static float[] Envelope(float[] samples, double tone, CancellationToken token)
    {
        const int step = 32;
        var output = new float[(samples.Length + step - 1) / step];
        double iFilter = 0, qFilter = 0, smoothed = 0, phase = 0;
        double increment = 2 * Math.PI * tone / WaveAudio.AnalysisRate;
        double alpha = 1 - Math.Exp(-1.0 / (WaveAudio.AnalysisRate * .003));
        for (int i = 0; i < samples.Length; i++)
        {
            if ((i & 4095) == 0) token.ThrowIfCancellationRequested();
            iFilter += alpha * (samples[i] * Math.Cos(phase) - iFilter);
            qFilter += alpha * (samples[i] * Math.Sin(phase) - qFilter);
            phase += increment; if (phase >= 2 * Math.PI) phase -= 2 * Math.PI;
            double magnitude = 2 * Math.Sqrt(iFilter * iFilter + qFilter * qFilter);
            smoothed += .08 * (magnitude - smoothed);
            if (i % step == step - 1 || i == samples.Length - 1) output[i / step] = (float)smoothed;
        }
        return output;
    }
    private readonly record struct Run(bool On, double Duration);
    private static List<Run> Runs(float[] envelope, double high, double low)
    {
        var runs = new List<Run>(); bool on = false; int start = 0;
        for (int i = 0; i < envelope.Length; i++)
        {
            bool next = on ? envelope[i] >= low : envelope[i] >= high;
            if (next == on) continue;
            if (i > start) runs.Add(new(on, (i - start) * 4));
            on = next; start = i;
        }
        if (start < envelope.Length) runs.Add(new(on, (envelope.Length - start) * 4));
        // Isolated sub-8-ms glitches cannot be dots at the supported 5–60 WPM.
        for (int i = 1; i + 1 < runs.Count; i++)
        {
            if (runs[i].Duration > 8 || runs[i - 1].On != runs[i + 1].On) continue;
            runs[i - 1] = new(runs[i - 1].On, runs[i - 1].Duration + runs[i].Duration + runs[i + 1].Duration);
            runs.RemoveRange(i, 2); i = Math.Max(0, i - 2);
        }
        while (runs.Count > 0 && !runs[0].On) runs.RemoveAt(0);
        while (runs.Count > 0 && !runs[^1].On) runs.RemoveAt(runs.Count - 1);
        return runs;
    }
    private static double EstimateUnit(double[] marks)
    {
        double bestScore = double.PositiveInfinity, best = 0;
        foreach (double duration in marks.Distinct().Take(128))
        foreach (int divisor in new[] { 1, 3 })
        {
            double unit = duration / divisor;
            if (unit is < 14 or > 270) continue;
            double error = marks.Average(mark => Math.Min(Math.Pow(mark / unit - 1, 2), Math.Pow(mark / unit - 3, 2)));
            // Prefer the interpretation containing dots when all marks have the same length.
            if (!marks.Any(mark => mark / unit < 2)) error += .05;
            if (error < bestScore) { bestScore = error; best = unit; }
        }
        if (best == 0) throw new InvalidDataException("decoderNotMorse");
        var dots = marks.Where(d => d / best < 2).Order().ToArray();
        return dots.Length > 0 ? dots[dots.Length / 2] : best;
    }
}
