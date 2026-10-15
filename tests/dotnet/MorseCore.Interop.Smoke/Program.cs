using System.Runtime.InteropServices;
using System.Text;
using MorseCore.Interop;

if (args.Length != 1) throw new ArgumentException("Pass the absolute native library path.");
string nativePath = Path.GetFullPath(args[0]);
NativeLibrary.SetDllImportResolver(typeof(MorseEngine).Assembly,
    (name, assembly, paths) => name == "MorseCore.Native" ? NativeLibrary.Load(nativePath) : 0);
int checks = 0;
void Check(bool condition, string label)
{
    if (!condition) throw new InvalidOperationException(label);
    Interlocked.Increment(ref checks);
}
void Fails(Action operation, int status)
{
    try { operation(); }
    catch (MorseNativeException error) { Check(error.Status == status, $"Expected native status {status}, got {error.Status}"); return; }
    throw new InvalidOperationException($"Expected native failure {status}");
}

Check(MorseEngine.NativeVersion == "0.3.0", "Version");
var converted = MorseEngine.Encode("Merhaba Dünya");
Check(MorseEngine.Decode(converted.Text).Text == "MERHABA DUNYA", "Turkish UTF-8 roundtrip");
Check(converted.Statistics == new ConversionStatistics(13, (ulong)converted.Text.Length, 2, 12, 1), "64-bit statistics layout");
foreach (char separator in "/|#*")
    Check(MorseEngine.Decode(MorseEngine.Encode("A/B C!", separator).Text).Text == "A/B C!", "Auto separator and punctuation");
Check(MorseEngine.Alphabet().Count == 54 && MorseEngine.Alphabet().Count(x => x.IsExtension) == 5, "Fixed alphabet layout");
string big = new('Z', 2 * 1024 * 1024 + 1);
Check(MorseEngine.Decode(MorseEngine.Encode(big).Text).Text == big, "Large managed/native roundtrip");
Fails(() => MorseEngine.Encode(""), -1);
Fails(() => MorseEngine.Encode("A\0B"), -2);
Fails(() => MorseEngine.Encode("%"), -3);
Fails(() => MorseEngine.Decode("........"), -4);
Fails(() => MorseEngine.Encode("SOS", 'x'), -20);
try { MorseEngine.Encode("\ud800"); throw new InvalidOperationException("Invalid surrogate accepted"); }
catch (EncoderFallbackException) { checks++; }
try { MorseAudio.WriteWav("\ud800.wav", "."); throw new InvalidOperationException("Invalid path surrogate accepted"); }
catch (EncoderFallbackException) { checks++; }
Parallel.For(0, 100, i =>
{
    string input = $"SOS {i}";
    Check(MorseEngine.Decode(MorseEngine.Encode(input).Text).Text == input, "Concurrent independent conversions");
});

using (var session = new PracticeSession(PracticeMode.Letters, 3, 123))
{
    var question = session.Next();
    Check(question.Number == 1 && question.Morse.Length > 0, "Question layout");
    Fails(() => session.Next(), -102);
    string answer = MorseEngine.Decode(question.Morse).Text;
    var feedback = session.Submit(answer.ToLowerInvariant());
    Check(feedback.Verdict == PracticeVerdict.Correct && feedback.ExpectedAnswer == answer, "Feedback layout");
    Fails(() => session.Submit(answer), -102);
    session.Next();
    Check(session.Submit("%").Verdict == PracticeVerdict.Incorrect, "Wrong answer");
    session.Next();
    Check(session.Skip().Verdict == PracticeVerdict.Skipped, "Skip");
    var stats = session.Statistics;
    Check(stats.Answered == 3 && stats.Correct == 1 && stats.Incorrect == 1 && stats.Skipped == 1, "Session counters");
    Check(Math.Abs(stats.Accuracy - 100.0 / 3) < 1e-10, "Double layout");
    Fails(() => session.Next(), -102);
}
var disposed = new PracticeSession(PracticeMode.Words, 1);
disposed.Dispose(); disposed.Dispose();
try { disposed.Next(); throw new InvalidOperationException("Disposed handle accepted"); }
catch (ObjectDisposedException) { checks++; }

Check(MorseAudio.Defaults == new AudioOptions(20, 600, 22050), "Audio options layout");
string sos = MorseEngine.Encode("SOS").Text;
AudioAnalysis analysis = MorseAudio.Analyze(sos);
Check(analysis == new AudioAnalysis(27, 35721, 9), "Audio analysis layout and timing");
Fails(() => MorseAudio.Analyze(sos, new AudioOptions(1, 600)), -103);
string directory = Directory.CreateTempSubdirectory("morsecore-interop-").FullName;
try
{
    string path = Path.Combine(directory, "Çığ ses.wav");
    MorseAudio.WriteWav(path, sos);
    byte[] wav = File.ReadAllBytes(path);
    Check(Encoding.ASCII.GetString(wav, 0, 4) == "RIFF" && (ulong)wav.Length == 44 + analysis.Samples * 2, "UTF-8 WAV path and payload");
}
finally { Directory.Delete(directory, true); }
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    try { await MorseAudio.PlayAsync(sos, cancellationToken: cancellation.Token); throw new InvalidOperationException("Pre-cancelled playback succeeded"); }
    catch (OperationCanceledException) { checks++; }
}
Console.WriteLine($"PASS: {checks} .NET/native checks; no GUI and no audio device used.");
