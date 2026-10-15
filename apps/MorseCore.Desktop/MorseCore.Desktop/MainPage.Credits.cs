using MorseCore.AppCore;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private void BuildCredits()
    {
        var content=new StackPanel { Spacing=14 };
        content.Children.Add(Label(T("thanksBody"),13,true));
        foreach(var project in OpenSourceCredits.Projects)
        {
            var item=new StackPanel { Spacing=3 };
            var link=new HyperlinkButton { Content=project.Name,NavigateUri=project.Source,Padding=new Thickness(0),Foreground=Brush("AccentInkBrush"),HorizontalAlignment=HorizontalAlignment.Left };
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(link,project.Name+" · "+project.Author);
            item.Children.Add(link);
            item.Children.Add(Label(project.Author+" · "+project.License,12,true));
            content.Children.Add(item);
        }
        var voices=new HyperlinkButton { Content=T("voiceCredits"),NavigateUri=new Uri("https://huggingface.co/rhasspy/piper-voices"),Padding=new Thickness(0),Foreground=Brush("AccentInkBrush"),HorizontalAlignment=HorizontalAlignment.Left };
        content.Children.Add(voices); content.Children.Add(Label(T("creditsLicenseNote"),12,true));
        var section=new Expander { Header=T("thanksTitle"),Content=content,HorizontalAlignment=HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(section,"OpenSourceCredits");
        PageContent.Children.Add(section);
    }
}
