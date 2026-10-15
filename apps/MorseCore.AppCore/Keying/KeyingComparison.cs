namespace MorseCore.AppCore.Keying;

public static class KeyingComparison
{
    /// <summary>Character edit accuracy, including missing and extra characters. Both inputs must already use the same alphabet normalization.</summary>
    public static int Accuracy(string expected,string actual)
    {
        ArgumentNullException.ThrowIfNull(expected); ArgumentNullException.ThrowIfNull(actual);
        if(expected.Length>256 || actual.Length>8192) throw new ArgumentOutOfRangeException(nameof(expected));
        expected=expected.Trim(); actual=actual.Trim();
        if(expected.Length==0) return actual.Length==0 ? 100 : 0;
        if(actual.Length==0) return 0;
        var previous=new int[actual.Length+1]; var row=new int[actual.Length+1];
        for(int j=0;j<previous.Length;j++) previous[j]=j;
        for(int i=1;i<=expected.Length;i++)
        {
            row[0]=i;
            for(int j=1;j<=actual.Length;j++) row[j]=Math.Min(Math.Min(previous[j]+1,row[j-1]+1),previous[j-1]+(expected[i-1]==actual[j-1] ? 0 : 1));
            (previous,row)=(row,previous);
        }
        return (int)Math.Round(Math.Clamp(100d*(1-(double)previous[actual.Length]/Math.Max(expected.Length,actual.Length)),0,100));
    }
}
