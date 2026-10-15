namespace MorseCore.Interop;

public enum PracticeMode : uint { Letters, Digits, Words }
public enum PracticeVerdict { Incorrect, Correct, Skipped }
public sealed record PracticeQuestion(uint Number, string Morse);
public sealed record PracticeFeedback(PracticeVerdict Verdict, uint QuestionNumber, string ExpectedAnswer);
public sealed record PracticeStatistics(uint Total, uint Answered, uint Correct, uint Incorrect, uint Skipped, double Accuracy);

/// <summary>Owns one native session. Calls and disposal are serialized.</summary>
public sealed unsafe class PracticeSession : IDisposable
{
    private readonly object gate = new();
    private readonly PracticeHandle handle;
    private bool disposed;
    public PracticeSession(PracticeMode mode, uint questions = 10, uint seed = 0)
    {
        Interop.EnsureCompatible();
        Interop.Check(NativeMethods.mc_practice_create((uint)mode, questions, seed, out nint pointer));
        handle = new(pointer);
    }
    public PracticeQuestion Next()
    {
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            Interop.Check(NativeMethods.mc_practice_next(handle, out nint result));
            var question = Interop.ReadResult(result);
            return new(Statistics.Answered + 1, question.Text);
        }
    }
    public PracticeFeedback Submit(string answer)
    {
        byte[] bytes = Interop.Bytes(answer);
        lock (gate)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            NativeFeedback feedback;
            fixed (byte* input = bytes)
                Interop.Check(NativeMethods.mc_practice_submit(handle, input, (ulong)bytes.Length, out feedback, (uint)sizeof(NativeFeedback)));
            return new((PracticeVerdict)feedback.Verdict, feedback.Number, Interop.FixedString(feedback.Expected, 64));
        }
    }
    public PracticeFeedback Skip() => Submit("");
    public PracticeStatistics Statistics
    {
        get
        {
            lock (gate)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                Interop.Check(NativeMethods.mc_practice_stats(handle, out NativePracticeStats stats, (uint)sizeof(NativePracticeStats)));
                return new(stats.Total, stats.Answered, stats.Correct, stats.Incorrect, stats.Skipped, stats.Accuracy);
            }
        }
    }
    public void Dispose()
    {
        lock (gate) { if (!disposed) { handle.Dispose(); disposed = true; } }
    }
}
