using System.Globalization;

namespace MorseCore.AppCore.Radio;

public readonly record struct GeoPoint(double Latitude,double Longitude);
public static class Locator
{
    /// <summary>Returns the centre of the supplied Maidenhead cell, not a station's exact location.</summary>
    public static GeoPoint Centre(string locator)
    {
        string code=locator.Trim().ToUpperInvariant();
        if(code.Length is not(2 or 4 or 6 or 8 or 10 or 12)) throw new ArgumentException("Invalid Maidenhead locator.");
        double longitude=-180,latitude=-90,width=20,height=10;
        for(int pair=0;pair<code.Length/2;pair++)
        {
            bool digit=pair%2==1;
            int maximum=pair==0?18:digit?10:24;
            char start=digit?'0':'A';
            int x=code[2*pair]-start,y=code[2*pair+1]-start;
            if(x<0 || y<0 || x>=maximum || y>=maximum) throw new ArgumentException("Invalid Maidenhead locator.");
            if(pair>0) { width/=maximum; height/=maximum; }
            longitude+=x*width; latitude+=y*height;
        }
        return new(latitude+height/2,longitude+width/2);
    }
    public static GeoPoint? Position(QsoRecord record)=>record.Latitude is { } latitude && record.Longitude is { } longitude ? new(latitude,longitude) : record.GridSquare.Length>0 ? Centre(record.GridSquare) : null;
    public static void Validate(string grid,double? latitude,double? longitude)
    {
        if(grid.Length>0) _=Centre(grid);
        if(latitude.HasValue!=longitude.HasValue || latitude is { } lat && (!double.IsFinite(lat) || lat is <-90 or >90) || longitude is { } lon && (!double.IsFinite(lon) || lon is <-180 or >180)) throw new ArgumentException("Invalid geographic coordinates.");
    }
    internal static double ParseAdif(string value,bool latitude)
    {
        // ADIF Location: XDDD MM.MMM, degrees and minutes, always ASCII/invariant.
        if(value.Length!=11 || value[4]!=' ' || value[7]!='.' || !(latitude?"NS":"EW").Contains(value[0]) ||
            !int.TryParse(value.AsSpan(1,3),NumberStyles.None,CultureInfo.InvariantCulture,out int degrees) ||
            !decimal.TryParse(value.AsSpan(5),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal minutes) || minutes is <0 or >=60 || degrees>(latitude?90:180) || degrees==(latitude?90:180) && minutes!=0)
            throw new ArgumentException("Invalid ADIF location.");
        double coordinate=degrees+(double)minutes/60; return value[0] is 'S' or 'W'?-coordinate:coordinate;
    }
    internal static string ToAdif(double coordinate,bool latitude)
    {
        decimal absolute=(decimal)Math.Abs(coordinate);
        int degrees=(int)absolute; decimal minutes=Math.Round((absolute-degrees)*60,3,MidpointRounding.AwayFromZero);
        if(minutes==60) { minutes=0; degrees++; }
        return (latitude?(coordinate<0?'S':'N'):(coordinate<0?'W':'E'))+degrees.ToString("000",CultureInfo.InvariantCulture)+" "+minutes.ToString("00.000",CultureInfo.InvariantCulture);
    }
}
