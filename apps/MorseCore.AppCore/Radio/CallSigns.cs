using System.Text.RegularExpressions;

namespace MorseCore.AppCore.Radio;

public static partial class CallSigns
{
    // KEGM Annex 1. The number identifies an allocation area, not live station location.
    private static readonly string[] TurkishAreas=[
        "@ISLANDS@",
        "Çanakkale (@EUROPE@), Edirne, İstanbul (@EUROPE@), Kırklareli, Tekirdağ",
        "Ankara, Bartın, Bilecik, Bolu, Düzce, Eskişehir, İstanbul (@ASIA@), Karabük, Kırıkkale, Kocaeli, Sakarya, Yalova, Zonguldak",
        "Balıkesir, Bursa, Çanakkale (@ASIA@), İzmir, Manisa",
        "Afyon, Antalya, Aydın, Burdur, Denizli, Isparta, Kütahya, Muğla, Uşak",
        "Adana, Aksaray, Hatay, Karaman, Konya, Mersin, Nevşehir, Niğde, Osmaniye",
        "Amasya, Çankırı, Çorum, Kastamonu, Kırşehir, Samsun, Sinop, Tokat, Yozgat",
        "Bayburt, Erzincan, Giresun, Gümüşhane, Kayseri, Ordu, Sivas, Trabzon, Tunceli",
        "Adıyaman, Bingöl, Diyarbakır, Elazığ, Gaziantep, Kahramanmaraş, Kilis, Malatya, Mardin, Şanlıurfa, Şırnak",
        "Ağrı, Ardahan, Artvin, Batman, Bitlis, Erzurum, Hakkari, Iğdır, Kars, Muş, Rize, Siirt, Van"];
    public static string? TurkishArea(string region)=>region.Length==1 && char.IsAsciiDigit(region[0]) ? TurkishAreas[region[0]-'0'] : null;
    [GeneratedRegex("^[A-Z0-9]{1,4}[0-9][A-Z]{1,4}$",RegexOptions.CultureInvariant)]
    private static partial Regex BasicPattern();
    [GeneratedRegex("^(TA|TB|TC|YM)[0-9][A-Z]{1,3}$",RegexOptions.CultureInvariant)]
    private static partial Regex TurkishPattern();
    [GeneratedRegex("^D[ABCF-HJ-R][0-9][A-Z]{1,3}$",RegexOptions.CultureInvariant)]
    private static partial Regex GermanPattern();
    [GeneratedRegex("^(R[A-Z]?|U[A-I])[0-9][A-Z]{1,4}$",RegexOptions.CultureInvariant)]
    private static partial Regex RussianPattern();
    [GeneratedRegex("^([KNW][A-Z]?|A[A-L])[0-9][A-Z]{1,3}$",RegexOptions.CultureInvariant)]
    private static partial Regex AmericanPattern();
    [GeneratedRegex("^([GM][BDEIJMUW]?|2[EWMIDJU])[0-9][A-Z]{1,3}$",RegexOptions.CultureInvariant)]
    private static partial Regex BritishPattern();
    [GeneratedRegex("^(HZ|7Z|8Z)[0-9][A-Z]{1,4}$",RegexOptions.CultureInvariant)]
    private static partial Regex SaudiPattern();
    [GeneratedRegex("^A6[0-9][A-Z]{1,4}$",RegexOptions.CultureInvariant)]
    private static partial Regex EmiratesPattern();
    public static CallSignInfo Parse(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        string normalized=value.Trim().ToUpperInvariant();
        if(normalized.Length is <3 or >32 || normalized.Any(c=>c is not (>= 'A' and <= 'Z') and not (>= '0' and <= '9') and not '/')) throw new ArgumentException("Invalid call sign.",nameof(value));
        string[] parts=normalized.Split('/');
        if(parts.Any(p=>p.Length==0) || parts.Length>4) throw new ArgumentException("Invalid portable call sign.",nameof(value));
        var candidates=parts.Where(p=>BasicPattern().IsMatch(p)).ToArray();
        if(candidates.Length!=1) throw new ArgumentException("A single base call sign is required.",nameof(value));
        string call=candidates[0]; string? country=null,license=null,region=null;
        if(TurkishPattern().IsMatch(call))
        {
            country="TR"; region=call[2].ToString();
            license=call.StartsWith("TA",StringComparison.Ordinal)?"A":call.StartsWith("TB",StringComparison.Ordinal)?"CPTW".Contains(call[3]) && call.Length==6?"C":"B":null;
        }
        else if(call.StartsWith("TC",StringComparison.Ordinal)) country="TR";
        else if(GermanPattern().IsMatch(call))
        {
            country="DE";
            license=call.StartsWith("DN9",StringComparison.Ordinal)?"N":call.StartsWith("DO",StringComparison.Ordinal)?"E":
                call.StartsWith("DN",StringComparison.Ordinal) || call.StartsWith("DA",StringComparison.Ordinal) || call.StartsWith("DP",StringComparison.Ordinal) || call.StartsWith("DR",StringComparison.Ordinal) ? null : "A";
        }
        else if(RussianPattern().IsMatch(call)) country="RU";
        else if(AmericanPattern().IsMatch(call)) country="US";
        else if(BritishPattern().IsMatch(call)) country="GB";
        else if(SaudiPattern().IsMatch(call)) country="SA";
        else if(EmiratesPattern().IsMatch(call)) { country="AE"; license=call.StartsWith("A61",StringComparison.Ordinal)?"A":call.StartsWith("A67",StringComparison.Ordinal)?"B":null; }
        return new(normalized,call,country,license,region,country!=null);
    }
    public static OperatingCall Operating(StationProfile profile,OperatingContext context)
    {
        var parsed=Parse(profile.CallSign);
        if(context.CountryId=="TR" && context.Region.Length>0 && TurkishArea(context.Region)==null) throw new ArgumentException("Operating area must be a digit from 0 to 9.");
        if(!string.IsNullOrWhiteSpace(context.ManualCall))
        {
            var manual=Parse(context.ManualCall);
            if(manual.BaseCall!=parsed.BaseCall) throw new ArgumentException("Operating call must retain the station's base call.");
            return new(manual.Normalized,true,"manualCallReview");
        }
        if(context.CountryId!=profile.CountryId) return new(parsed.BaseCall,true,"visitorCallReview");
        string call=parsed.BaseCall;
        if(profile.CountryId=="TR" && context.Region.Length==1 && char.IsAsciiDigit(context.Region[0]) && context.Region!=parsed.Region) call+="/"+context.Region;
        if(context.Mode!=StationMode.Fixed)
        {
            // These common suffixes are proposed only for countries with reviewed domestic rules.
            if(profile.CountryId is "DE" or "GB" or "US" or "SA") call+=context.Mode==StationMode.Mobile ? "/M" : "/P";
            else return new(call,true,"portableCallReview");
        }
        return new(call,false,context.Mode==StationMode.Fixed ? "fixedCallNote":"portableCallNote");
    }
}
