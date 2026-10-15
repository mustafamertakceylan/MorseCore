using MorseCore.AppCore.Decoding;
using MorseCore.AppCore.Radio;
using MorseCore.Interop;

internal static class DecoderChecks
{
    public static void Run(Action<bool, string> check)
    {
        string directory = Path.Combine(Path.GetTempPath(), "morsecore-decoder-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            int index = 0;
            foreach (var test in new[] { ("SOS", 12u, 500u), ("CQ CQ DE TA1PDM", 20u, 700u), ("HELLO WORLD 73", 35u, 1100u), ("PARIS TEST", 60u, 2000u), ("TEST 123", 5u, 200u) })
            {
                string path = Path.Combine(directory, $"native-{index++}.wav");
                MorseAudio.WriteWav(path, MorseEngine.Encode(test.Item1).Text, new(test.Item2, test.Item3));
                var audio = WaveAudio.Read(path);
                var result = MorseDecoder.Decode(audio);
                check(result.Text == test.Item1, $"WAV decoder native roundtrip: {test.Item1} ({result.Text})");
                check(Math.Abs(result.FrequencyHz - test.Item3) <= 8, "WAV decoder automatic tone");
                check(Math.Abs(result.WordsPerMinute - test.Item2) < Math.Max(2, test.Item2 * .18), "WAV decoder automatic speed");
                check(result.UncertainCharacters == 0, "WAV decoder clean text has no uncertain symbols");
                var known = MorseDecoder.Decode(audio, new(test.Item3, test.Item2));
                check(known.Text == test.Item1, "WAV decoder manual tone and speed");
                var weak = new WaveAudio(audio.Samples.Select(v => v * .015f).ToArray(), audio.SampleRate);
                check(MorseDecoder.Decode(weak).Text == test.Item1, "WAV decoder low amplitude");
                var random = new Random(110 + index);
                var noisy = new WaveAudio(audio.Samples.Select(v => v + (float)((random.NextDouble() - .5) * .025)).ToArray(), audio.SampleRate);
                check(MorseDecoder.Decode(noisy).Text == test.Item1, "WAV decoder light background noise");
            }
            string prosignPath = Path.Combine(directory, "prosign.wav");
            MorseAudio.WriteWav(prosignPath, CwMessages.Encode("CQ <SK>"), new(20, 700));
            var prosign = MorseDecoder.Decode(WaveAudio.Read(prosignPath));
            check(prosign.Text == "CQ <SK>" && prosign.Morse.Contains("...-.-"), "WAV decoder joined procedure signal");
            foreach (var pair in new[] { ("ПРИВЕТ МИР", MorseAlphabet.Russian), ("سلام عالم", MorseAlphabet.Arabic) })
            {
                string path = Path.Combine(directory, "alphabet-" + pair.Item2 + ".wav");
                MorseAudio.WriteWav(path, MorseEngine.Encode(pair.Item1, pair.Item2).Text, new(20, 700));
                check(MorseDecoder.Decode(WaveAudio.Read(path), new(Alphabet: pair.Item2)).Text == pair.Item1, "WAV decoder alphabet: " + pair.Item2);
            }
            foreach (uint rate in new[] { 8000u, 16000u, 44100u, 48000u })
            {
                string path = Path.Combine(directory, "rate-" + rate + ".wav");
                MorseAudio.WriteWav(path, MorseEngine.Encode("SOS TEST").Text, new(20, 733, rate));
                check(MorseDecoder.Decode(WaveAudio.Read(path)).Text == "SOS TEST", "WAV resampling rate: " + rate);
            }
            string ambiguousPath = Path.Combine(directory, "ambiguous.wav");
            MorseAudio.WriteWav(ambiguousPath, MorseEngine.Encode("TTT").Text, new(20, 700));
            var ambiguous = MorseDecoder.Decode(WaveAudio.Read(ambiguousPath));
            check(ambiguous.TimingAmbiguous && ambiguous.Text == "□" && ambiguous.Morse == "?", "WAV equal-length marks do not invent dot/dash certainty");
            check(MorseDecoder.Decode(WaveAudio.Read(ambiguousPath), new(WordsPerMinute: 20)).Text == "TTT", "WAV manual speed resolves equal-length marks");

            string reference = Path.Combine(directory, "reference.wav");
            MorseAudio.WriteWav(reference, MorseEngine.Encode("SOS TEST").Text, new(20, 733));
            var original = WaveAudio.Read(reference);
            using (var highRate = MakeWave(original.Samples.SelectMany(v => Enumerable.Repeat(v, 12)).ToArray(), 16, 1, false, 96000))
                check(MorseDecoder.Decode(WaveAudio.Read(highRate)).Text == "SOS TEST", "WAV high-rate 96kHz streaming reduction");
            foreach (int bits in new[] { 8, 16, 24, 32 })
            {
                using var stream = MakeWave(original.Samples, bits, 1, false);
                check(MorseDecoder.Decode(WaveAudio.Read(stream)).Text == "SOS TEST", "WAV decoder PCM precision: " + bits);
            }
            foreach (int bits in new[] { 32, 64 })
            {
                using var stream = MakeWave(original.Samples, bits, 2, true);
                check(MorseDecoder.Decode(WaveAudio.Read(stream)).Text == "SOS TEST", "WAV decoder IEEE float stereo: " + bits);
            }
            using (var stream = MakeWave(original.Samples, 16, 2, false))
                check(MorseDecoder.Decode(WaveAudio.Read(stream)).Text == "SOS TEST", "WAV decoder stereo downmix");
            var padded = new WaveAudio(new float[8000].Concat(original.Samples).Concat(new float[4000]).ToArray(), 8000);
            check(MorseDecoder.Decode(padded).Text == "SOS TEST", "WAV decoder leading/trailing silence");
            var silence = new WaveAudio(new float[8000], 8000);
            Reject(() => MorseDecoder.Decode(silence), "WAV decoder silence rejection");
            var noiseRandom = new Random(99);
            var noise = new WaveAudio(Enumerable.Range(0, 16000).Select(_ => (float)(noiseRandom.NextDouble() - .5)).ToArray(), 8000);
            Reject(() => MorseDecoder.Decode(noise), "WAV decoder broadband noise rejection");
            var continuous = new WaveAudio(Enumerable.Range(0, 16000).Select(i => (float)(.4 * Math.Sin(i * 2 * Math.PI * 700 / 8000))).ToArray(), 8000);
            Reject(() => MorseDecoder.Decode(continuous), "WAV decoder continuous tone rejection");
            foreach (int length in new[] { 160, 320, 511 })
            {
                float[] shortTone = Enumerable.Range(0, length).Select(i => (float)(.4 * Math.Sin(i * 2 * Math.PI * 700 / 8000))).ToArray();
                using var shortWave = MakeWave(shortTone, 16, 1, false);
                var shortAudio = WaveAudio.Read(shortWave);
                Reject(() => MorseDecoder.Decode(shortAudio), "WAV short signal automatic tone rejects clearly: " + length);
                Reject(() => MorseDecoder.Decode(shortAudio, new(700)), "WAV short signal manual tone rejects clearly: " + length);
            }
            var speechLike = new WaveAudio(Enumerable.Range(0, 16000).Select(i => (float)(.2 * Math.Sin(i * 2 * Math.PI * (300 + 250 * Math.Sin(i / 500.0)) / 8000) + .1 * Math.Sin(i * .08))).ToArray(), 8000);
            Reject(() => MorseDecoder.Decode(speechLike), "WAV decoder non-CW changing tones rejection");
            Reject(() => WaveAudio.Read(new MemoryStream([])), "WAV empty input rejected");
            using (var oversized = new OversizedStream()) Reject(() => WaveAudio.Read(oversized), "WAV file size bound before allocation");
            Reject(() => WaveAudio.Read(new MemoryStream(new byte[80])), "WAV non-RIFF rejected");
            byte[] truncated = File.ReadAllBytes(reference)[..30];
            Reject(() => WaveAudio.Read(new MemoryStream(truncated)), "WAV truncated chunks rejected");
            byte[] corrupt = File.ReadAllBytes(reference); corrupt[4] = 0xFF; corrupt[5] = 0xFF; corrupt[6] = 0xFF; corrupt[7] = 0x7F;
            Reject(() => WaveAudio.Read(new MemoryStream(corrupt)), "WAV oversized declared RIFF rejected");
            byte[] compressed = File.ReadAllBytes(reference); compressed[20] = 6;
            Reject(() => WaveAudio.Read(new MemoryStream(compressed)), "WAV compressed input rejected");
            using (var nanWave = MakeWave([float.NaN, 0, 0], 32, 1, true))
                Reject(() => WaveAudio.Read(nanWave), "WAV nonfinite float rejected");
            using (var tooLong = MakeWave(new float[WaveAudio.AnalysisRate * (WaveAudio.MaxSeconds + 1)], 8, 1, false))
                Reject(() => WaveAudio.Read(tooLong), "WAV duration limit");
            using (var cancelled = new CancellationTokenSource())
            {
                cancelled.Cancel();
                try { MorseDecoder.Decode(original, cancellationToken: cancelled.Token); throw new Exception("Decoder cancellation ignored"); }
                catch (OperationCanceledException) { check(true, "WAV DSP cancellation"); }
                try { WaveAudio.Read(reference, cancelled.Token); throw new Exception("WAV cancellation ignored"); }
                catch (OperationCanceledException) { check(true, "WAV reader cancellation"); }
            }
            try { MorseDecoder.Decode(original, new(50)); throw new Exception("Invalid tone accepted"); }
            catch (ArgumentOutOfRangeException) { check(true, "WAV decoder tone bounds"); }
            var unusual = new WaveAudio(Marks([60, 125, 180]), 8000);
            check(MorseDecoder.Decode(unusual, new(700, 20)).UncertainCharacters > 0, "WAV irregular mark is explicitly uncertain");
        }
        finally { Directory.Delete(directory, true); }
        void Reject(Action action, string label)
        {
            try { action(); throw new Exception(label + " was accepted"); }
            catch (InvalidDataException) { check(true, label); }
            catch (EndOfStreamException) { check(true, label); }
        }
    }

    private sealed class OversizedStream : MemoryStream
    {
        public override long Length => WaveAudio.MaxFileBytes + 1;
    }

    private static MemoryStream MakeWave(float[] samples, int bits, int channels, bool floating, int rate = 8000)
    {
        var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true);
        int bytes = bits / 8, align = channels * bytes, length = samples.Length * align;
        writer.Write("RIFF"u8); writer.Write(36 + length); writer.Write("WAVEfmt "u8); writer.Write(16);
        writer.Write((ushort)(floating ? 3 : 1)); writer.Write((ushort)channels); writer.Write(rate); writer.Write(rate * align); writer.Write((ushort)align); writer.Write((ushort)bits);
        writer.Write("data"u8); writer.Write(length);
        foreach (float sample in samples)
        for (int channel = 0; channel < channels; channel++)
        {
            if (floating) { if (bits == 32) writer.Write(sample); else writer.Write((double)sample); }
            else switch (bits)
            {
                case 8: writer.Write((byte)Math.Clamp((int)(sample * 128 + 128), 0, 255)); break;
                case 16: writer.Write((short)(sample * 32767)); break;
                case 24:
                    int value = (int)(sample * 8388607); writer.Write((byte)value); writer.Write((byte)(value >> 8)); writer.Write((byte)(value >> 16)); break;
                default: writer.Write((int)(sample * 2147483647)); break;
            }
        }
        stream.Position = 0; return stream;
    }

    private static float[] Marks(int[] milliseconds)
    {
        var samples = new List<float>();
        foreach (int mark in milliseconds)
        {
            for (int i = 0; i < mark * 8; i++) samples.Add((float)(.4 * Math.Sin(i * 2 * Math.PI * 700 / 8000)));
            samples.AddRange(new float[60 * 8]);
        }
        return samples.ToArray();
    }
}
