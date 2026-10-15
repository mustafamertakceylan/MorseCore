using System.Globalization;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using MorseCore.AppCore.Radio;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private void BuildCountryRules(RadioCountry country,string license)
    {
        var ranges=country.Bands.SelectMany(b=>b.Powers.Where(p=>p.ClassId==license).Select(p=>(Band:b,Power:p))).ToArray();
        var body=new StackPanel { Spacing=14 };
        var summary=new Grid { ColumnSpacing=10 };
        for(int i=0;i<3;i++) summary.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        (string Caption,string Value)[] facts=[(T("guide.country"),CountryName(country)),(T("licenseClass"),country.Classes.Contains(license)?ClassName(license):T("selectClass")),(T("guide.ranges"),ranges.Length.ToString(CultureInfo.InvariantCulture))];
        for(int i=0;i<facts.Length;i++)
        {
            var content=new StackPanel { Spacing=5 };
            content.Children.Add(Label(facts[i].Caption,12,true)); content.Children.Add(Label(facts[i].Value,21));
            var tile=new Border { Child=content,Background=Brush("AccentSoftBrush"),CornerRadius=new CornerRadius(12),Padding=new Thickness(12) };
            Grid.SetColumn(tile,i); summary.Children.Add(tile);
        }
        body.Children.Add(summary);
        body.Children.Add(Label(T("guide.intro"),14));
        body.Children.Add(Label(T("guide.scope"),12,true));
        if(!country.Classes.Contains(license)) body.Children.Add(Label(T("selectClass"),16));
        else if(ranges.Length==0) body.Children.Add(Label(T("guide.empty"),14));
        else
        {
            var cards=new Grid { ColumnSpacing=12,RowSpacing=12 };
            foreach(var range in ranges) cards.Children.Add(BandGuideCard(range.Band,range.Power));
            int columns=0;
            void Arrange(double width)
            {
                int count=width>=620 ? 2 : 1;
                if(columns==count) return;
                columns=count; cards.ColumnDefinitions.Clear(); cards.RowDefinitions.Clear();
                for(int i=0;i<count;i++) cards.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
                for(int i=0;i<(cards.Children.Count+count-1)/count;i++) cards.RowDefinitions.Add(new() { Height=GridLength.Auto });
                for(int i=0;i<cards.Children.Count;i++) { Grid.SetRow((FrameworkElement)cards.Children[i],i/count); Grid.SetColumn((FrameworkElement)cards.Children[i],i%count); }
            }
            cards.SizeChanged+=(_,e)=>Arrange(e.NewSize.Width); Arrange(760);
            body.Children.Add(cards); body.Children.Add(Label(T("guide.scale"),12,true));
        }
        var notes=new StackPanel { Spacing=10 };
        notes.Children.Add(Label(T(country.NotesKey),13));
        notes.Children.Add(Label(T("rulesCoverage"),12,true));
        foreach(var source in RadioCatalog.Data.Sources.Where(s=>country.SourceIds.Contains(s.Id)))
            notes.Children.Add(new HyperlinkButton { Content=source.Title+" · "+source.Section,NavigateUri=new Uri(source.Url),HorizontalAlignment=HorizontalAlignment.Left,FontSize=12,Padding=new Thickness(0),Foreground=Brush("AccentInkBrush") });
        body.Children.Add(new Expander { Header=T("guide.details"),Content=notes,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch });
        body.Children.Add(Label(T("rulesReviewed")+" · "+RadioCatalog.Data.ReviewedOn.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture),12,true));
        var guide=new Expander { Header=T("guide.title")+" · "+CountryName(country),Content=body,IsExpanded=countryRulesExpanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        AutomationProperties.SetAutomationId(guide,"CountryRules");
        guide.Expanding+=(_,_)=>countryRulesExpanded=true; guide.Collapsed+=(_,_)=>countryRulesExpanded=false;
        PageContent.Children.Add(guide);
    }
    private Border BandGuideCard(RadioBand band,ClassPower power)
    {
        string MHz(long hz)=>(hz/1_000_000m).ToString("0.######",CultureInfo.InvariantCulture)+" MHz";
        string basis=power.Basis switch { "output"=>T("powerOutput"),"PEP"=>T("guide.pep"),"ERP"=>T("guide.erp"),_=>power.Basis };
        string watts=power.Watts.ToString("0.######",CultureInfo.InvariantCulture)+" W";
        var stack=new StackPanel { Spacing=12 };
        var header=new Grid { ColumnSpacing=12 };
        header.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); header.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        var bandTitle=Label(band.Band,30); bandTitle.FontFamily=(FontFamily)Application.Current.Resources["DisplayFont"]; bandTitle.FlowDirection=FlowDirection.LeftToRight;
        header.Children.Add(bandTitle);
        var powerInfo=new StackPanel { Spacing=2,HorizontalAlignment=HorizontalAlignment.Right };
        powerInfo.Children.Add(Label(T("guide.power"),11,true));
        var amount=Label(watts,22); amount.FlowDirection=FlowDirection.LeftToRight; powerInfo.Children.Add(amount);
        powerInfo.Children.Add(Label(basis,11,true)); Grid.SetColumn(powerInfo,1); header.Children.Add(powerInfo); stack.Children.Add(header);
        var endpoints=new Grid { ColumnSpacing=12,FlowDirection=FlowDirection.LeftToRight };
        endpoints.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) }); endpoints.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
        foreach(var endpoint in new[]{(Key:"guide.start",Hz:band.StartHz,Column:0),(Key:"guide.end",Hz:band.EndHz,Column:1)})
        {
            var labels=new StackPanel { Spacing=4,HorizontalAlignment=endpoint.Column==0?HorizontalAlignment.Left:HorizontalAlignment.Right };
            var caption=Label(T(endpoint.Key),11,true); caption.FlowDirection=Shell.FlowDirection; labels.Children.Add(caption); labels.Children.Add(Label(MHz(endpoint.Hz),16));
            Grid.SetColumn(labels,endpoint.Column); endpoints.Children.Add(labels);
        }
        stack.Children.Add(endpoints);
        var axis=new Grid { Height=12,FlowDirection=FlowDirection.LeftToRight };
        axis.Children.Add(new Border { Height=4,CornerRadius=new CornerRadius(2),Background=Brush("AccentSoftBrush"),VerticalAlignment=VerticalAlignment.Center });
        axis.Children.Add(new Ellipse { Width=10,Height=10,Fill=Brush("AccentInkBrush"),HorizontalAlignment=HorizontalAlignment.Left });
        axis.Children.Add(new Ellipse { Width=10,Height=10,Fill=Brush("CardBrush"),Stroke=Brush("AccentInkBrush"),StrokeThickness=2,HorizontalAlignment=HorizontalAlignment.Right });
        stack.Children.Add(axis);
        if(band.ConditionsKey!="bandConditions" && (band.ConditionsKey!="trHomebuilt" || power.ClassId=="B"))
        {
            var condition=new StackPanel { Spacing=4 };
            condition.Children.Add(Label(T("guide.condition"),12)); condition.Children.Add(Label(T(band.ConditionsKey),12));
            stack.Children.Add(new Border { Child=condition,Background=Brush("AccentSoftBrush"),CornerRadius=new CornerRadius(8),Padding=new Thickness(10) });
        }
        var card=Card(stack); AutomationProperties.SetAutomationId(card,"BandGuideCard");
        AutomationProperties.SetName(card,$"{band.Band}: {MHz(band.StartHz)} – {MHz(band.EndHz)}; {watts}; {basis}");
        return card;
    }
}
