namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private TextBlock? workspaceFeedback;
    private TextBlock WorkspaceFeedback()
    {
        workspaceFeedback=Label("",14,true);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(workspaceFeedback,"WorkspaceFeedback");
        workspaceFeedback.Visibility=Visibility.Collapsed;
        return workspaceFeedback;
    }
    private FrameworkElement WorkspaceTabs(string automationId,string[] labelKeys,int selected,Action<int> changed)
    {
        var buttons=labelKeys.Select((key,index)=>
        {
            var button=Action(key,()=> { if(index!=selected) changed(index); return Task.CompletedTask; },primary:index==selected);
            button.HorizontalAlignment=HorizontalAlignment.Stretch;
            button.HorizontalContentAlignment=HorizontalAlignment.Center;
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(button,automationId+"-"+index);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetItemStatus(button,index==selected?T("uxSelected"):"");
            return (FrameworkElement)button;
        }).ToArray();
        var tabs=Columns(165,labelKeys.Length,buttons);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(tabs,automationId);
        return tabs;
    }
}
