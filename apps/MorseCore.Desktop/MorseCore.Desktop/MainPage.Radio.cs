using Microsoft.UI.Xaml.Media;
using MorseCore.AppCore.Radio;
using MorseCore.Interop;
using Windows.ApplicationModel.DataTransfer;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private StationDocument? stations;
    private StationProfile stationDraft=new();
    private string operatingCountry="",operatingClass="",operatingRegion="",manualCall="";
    private StationMode operatingMode;
    private string otherCall="",sentRst="",receivedRst="",qsoMessage="",qsoNote="",qsoGrid="",qsoPreview="",qsoMorse="";
    private double qsoFrequency=14.025;
    private QsoStage qsoStage;
    private bool practiceLog=true,showPracticeLog=true;
    private bool operatingDetailsExpanded,stationDetailsExpanded,qsoRecordExpanded,countryRulesExpanded;
    private bool returnToQsoAfterStation;
    private TextBox? qsoTextBox,qsoCodeBox;
    private void InvalidateQso()
    {
        qsoPreview=qsoMorse="";
        if(qsoTextBox!=null) qsoTextBox.Text="";
        if(qsoCodeBox!=null) qsoCodeBox.Text="";
    }
    private StationProfile? ActiveStation=>stations?.Profiles.FirstOrDefault(p=>p.Id==stations.ActiveId);
    private string CountryName(RadioCountry c)=>T(c.NameKey);
    private string ClassName(string value)=>value is "Foundation" or "Intermediate" or "Full" or "General" or "Technician" or "Extra" or "Advanced" or "Novice" or "Other" or "Special" ? T("class_"+value) : value;
    private TextBox RadioInput(string key,string value,Action<string> changed,bool ltr=false,int maximum=80)
    {
        var box=new TextBox { Header=T(key),Text=value,MaxLength=maximum,FlowDirection=ltr?FlowDirection.LeftToRight:Shell.FlowDirection };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(box,key);
        string previous=value;
        box.TextChanged+=(_,_)=>
        {
            if(previous==box.Text) return;
            previous=box.Text; changed(previous);
        };
        return box;
    }
    private void BuildRadio()
    {
        if(stations==null) { stations=radioStore.LoadProfiles(); if(ActiveStation is { } active) LoadStation(active); }
        Heading(T(page),T(page+"Hint"));
        if(radioStore.Warning!=null) PageContent.Children.Add(Label(T("radioDataError"),14,true));
        switch(page) { case "stationProfiles": BuildStationProfiles(); break; case "qsoAssistant": BuildQso(); break; case "qsoLog": BuildQsoLog(); break; }
    }
    private void LoadStation(StationProfile profile)
    {
        stationDraft=profile; operatingCountry=profile.CountryId; operatingClass=profile.ClassId;
        operatingRegion=CallSigns.Parse(profile.CallSign).Region ?? ""; manualCall=""; qsoPreview=qsoMorse="";
    }
    private void BuildStationProfiles()
    {
        var stack=new StackPanel { Spacing=12 };
        var profiles=stations!.Profiles.ToArray();
        stack.Children.Add(Label(T("uxStationStep"),14,true));
        if(profiles.Length>0) stack.Children.Add(Choice(T("stationPicker"),new[]{T("newStation")}.Concat(profiles.Select(p=>p.Label==p.CallSign?p.CallSign:$"{p.Label} · {p.CallSign}")),Array.FindIndex(profiles,p=>p.Id==stationDraft.Id)+1,i=>
        { if(i==0) { if(!profiles.Any(p=>p.Id==stationDraft.Id)) return; stationDraft=new(); } else { if(stationDraft.Id==profiles[i-1].Id) return; LoadStation(profiles[i-1]); } BuildPage(); }));
        stack.Children.Add(RadioInput("callSign",stationDraft.CallSign,v=>stationDraft=stationDraft with { CallSign=v },true,32));
        var analysis=Label("",14,true); stack.Children.Add(analysis);
        stack.Children.Add(Action("analyzeCall",()=>
        {
            try
            {
                var info=CallSigns.Parse(stationDraft.CallSign);
                stationDraft=stationDraft with { CallSign=info.Normalized,CountryId=info.CountryId ?? "OTHER",ClassId=info.ClassId ?? "" };
                BuildPage(); SetStatus(CallDescription(info));
            }
            catch(ArgumentException) { analysis.Text=T("invalidCall"); SetStatus(analysis.Text); }
            return Task.CompletedTask;
        },"\uE721"));
        if(stationDraft.CallSign.Length>0)
        { try { analysis.Text=CallDescription(CallSigns.Parse(stationDraft.CallSign)); } catch(ArgumentException) { analysis.Text=T("invalidCall"); } }
        var countries=RadioCatalog.Data.Countries;
        var homeCountry=Choice(T("homeCountry"),countries.Select(CountryName),Math.Max(0,Array.FindIndex(countries,c=>c.Id==stationDraft.CountryId)),i=>
        { if(stationDraft.CountryId==countries[i].Id) return; stationDraft=stationDraft with { CountryId=countries[i].Id,ClassId="" }; BuildPage(); });
        var country=RadioCatalog.Country(stationDraft.CountryId)!;
        var licenseClass=Choice(T("licenseClass"),new[]{T("selectClass")}.Concat(country.Classes.Select(ClassName)),Array.IndexOf(country.Classes,stationDraft.ClassId)+1,i=>stationDraft=stationDraft with { ClassId=i==0?"":country.Classes[i-1] });
        stack.Children.Add(Columns(260,2,homeCountry,licenseClass));
        var details=new StackPanel { Spacing=12 };
        details.Children.Add(Columns(260,2,RadioInput("profileName",stationDraft.Label,v=>stationDraft=stationDraft with { Label=v }),RadioInput("operatorName",stationDraft.OperatorName,v=>stationDraft=stationDraft with { OperatorName=v })));
        details.Children.Add(RadioInput("qth",stationDraft.Qth,v=>stationDraft=stationDraft with { Qth=v }));
        var optional=new Expander { Header=T("stationOptional"),Content=details,IsExpanded=stationDetailsExpanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(optional,"StationOptional");
        optional.Expanding+=(_,_)=>stationDetailsExpanded=true; optional.Collapsed+=(_,_)=>stationDetailsExpanded=false;
        stack.Children.Add(optional);
        stack.Children.Add(Label(T("callOfflineNote"),12,true));
        var controls=Row();
        var formError=Label("",14,true); formError.Visibility=Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(formError,"StationFormError");
        stack.Children.Add(formError);
        var save=Action("saveStation",()=>
        {
            CallSignInfo info;
            try { info=CallSigns.Parse(stationDraft.CallSign); }
            catch(ArgumentException)
            { formError.Text=T("invalidCall"); formError.Visibility=Visibility.Visible; SetStatus(formError.Text); return Task.CompletedTask; }
            if(!country.Classes.Contains(stationDraft.ClassId) || info.CountryId is { } recognized && recognized!=stationDraft.CountryId)
            { formError.Text=T("stationCheck"); formError.Visibility=Visibility.Visible; SetStatus(formError.Text); return Task.CompletedTask; }
            var profile=stationDraft with { CallSign=info.Normalized,Label=stationDraft.Label.Trim().Length==0?info.Normalized:stationDraft.Label.Trim() };
            var next=stations with { ActiveId=profile.Id,Profiles=stations.Profiles.Where(p=>p.Id!=profile.Id).Append(profile).ToList() };
            radioStore.SaveProfiles(next); stations=next; LoadStation(profile); SetStatus(T("saved"));
            if(returnToQsoAfterStation) { returnToQsoAfterStation=false; Navigate("qsoAssistant"); }
            else BuildPage();
            return Task.CompletedTask;
        },"\uE74E",true);
        if(returnToQsoAfterStation)
        {
            ((TextBlock)((StackPanel)save.Content).Children.Last()).Text=T("uxSaveAndContinue");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(save,T("uxSaveAndContinue"));
        }
        controls.Children.Add(save);
        if(profiles.Any(p=>p.Id==stationDraft.Id)) controls.Children.Add(Action("deleteStation",async()=>
        {
            if(!stations.Profiles.Any(p=>p.Id==stationDraft.Id)) return;
            var dialog=new ContentDialog { XamlRoot=XamlRoot,Title=T("deleteStation"),Content=stationDraft.CallSign,PrimaryButtonText=T("deleteStation"),CloseButtonText=T("cancelUpdate"),DefaultButton=ContentDialogButton.Close,RequestedTheme=RequestedTheme };
            if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
            var next=stations with { Profiles=stations.Profiles.Where(p=>p.Id!=stationDraft.Id).ToList() };
            next=next with { ActiveId=next.Profiles.FirstOrDefault()?.Id ?? "" }; radioStore.SaveProfiles(next); stations=next; stationDraft=new(); BuildPage();
        },"\uE74D"));
        stack.Children.Add(controls); WrapActions(controls,stack); PageContent.Children.Add(Card(stack));
        BuildCountryRules(country,stationDraft.ClassId);
    }
    private string CallDescription(CallSignInfo info)
    {
        string value=info.CountryId is { } id && RadioCatalog.Country(id) is { } country ? CountryName(country) : T("unknownCountry");
        value+=" · "+(info.ClassId is { } license ? T("licenseClass")+": "+ClassName(license) : T("classManual"));
        if(info.Region is { } region)
        {
            value+=" · "+T("region")+": "+region;
            if(info.CountryId=="TR" && CallSigns.TurkishArea(region) is { } places) value+="\n"+places.Replace("@EUROPE@",T("europe"),StringComparison.Ordinal).Replace("@ASIA@",T("asia"),StringComparison.Ordinal).Replace("@ISLANDS@",T("allIslands"),StringComparison.Ordinal);
        }
        return value;
    }
    private OperatingCall CurrentOperatingCall()
    {
        var station=ActiveStation ?? throw new InvalidOperationException("A station profile is required.");
        return CallSigns.Operating(station,new(operatingCountry,operatingClass,operatingMode,operatingRegion,manualCall));
    }
    private long QsoHz()=>double.IsFinite(qsoFrequency) && qsoFrequency>0 && qsoFrequency<=300_000 ? checked((long)Math.Round(qsoFrequency*1_000_000)) : throw new ArgumentException("Invalid frequency.");
    private void BuildQso()
    {
        if(ActiveStation is not { } station)
        {
            var empty=new StackPanel { Spacing=16 };
            empty.Children.Add(Label(T("stationRequired")));
            var create=Action("uxCreateStation",()=> { returnToQsoAfterStation=true; Navigate("stationProfiles"); return Task.CompletedTask; },"\uE77B",true);
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(create,"stationProfiles");
            empty.Children.Add(create); PageContent.Children.Add(Card(empty)); return;
        }
        if(operatingCountry.Length==0) LoadStation(station);
        var stack=new StackPanel { Spacing=12 };
        var profiles=stations!.Profiles.ToArray();
        stack.Children.Add(Choice(T("activeStation"),profiles.Select(p=>p.CallSign),Array.FindIndex(profiles,p=>p.Id==station.Id),i=>
        { if(stations.ActiveId==profiles[i].Id) return; var next=stations with { ActiveId=profiles[i].Id }; radioStore.SaveProfiles(next); stations=next; LoadStation(profiles[i]); BuildPage(); }));
        var details=new StackPanel { Spacing=12 };
        var countries=RadioCatalog.Data.Countries;
        details.Children.Add(Choice(T("operatingCountry"),countries.Select(CountryName),Math.Max(0,Array.FindIndex(countries,c=>c.Id==operatingCountry)),i=>
        { if(operatingCountry==countries[i].Id) return; operatingCountry=countries[i].Id; operatingClass=operatingCountry==station.CountryId?station.ClassId:""; manualCall=""; qsoPreview=qsoMorse=""; BuildPage(); }));
        var country=RadioCatalog.Country(operatingCountry)!;
        details.Children.Add(Choice(T("operatingClass"),new[]{T("selectClass")}.Concat(country.Classes.Select(ClassName)),Array.IndexOf(country.Classes,operatingClass)+1,i=> { string value=i==0?"":country.Classes[i-1]; if(value==operatingClass) return; operatingClass=value; InvalidateQso(); BuildPage(); }));
        details.Children.Add(Choice(T("stationMode"),[T("fixedStation"),T("portableStation"),T("mobileStation")],(int)operatingMode,i=> { if(operatingMode==(StationMode)i) return; operatingMode=(StationMode)i; InvalidateQso(); BuildPage(); }));
        if(operatingCountry=="TR") details.Children.Add(RadioInput("operatingRegion",operatingRegion,v=> { operatingRegion=v; InvalidateQso(); },true,1));
        details.Children.Add(RadioInput("manualOperatingCall",manualCall,v=> { manualCall=v; InvalidateQso(); },true,32));
        var identity=Label("",13,true);
        try { var call=CurrentOperatingCall(); identity.Text=call.CallSign+" · "+T(call.ExplanationKey); }
        catch(ArgumentException) { identity.Text=T("invalidCall"); }
        stack.Children.Add(identity);
        var operatingFields=details.Children.Cast<FrameworkElement>().ToArray(); details.Children.Clear();
        stack.Children.Add(Columns(260,2,operatingFields));
        var frequency=new NumberBox { Header=T("qsoFrequency"),Value=qsoFrequency,Minimum=0.000001,Maximum=300_000,SpinButtonPlacementMode=NumberBoxSpinButtonPlacementMode.Compact,FlowDirection=FlowDirection.LeftToRight };
        frequency.ValueChanged+=(_,_)=> { if(qsoFrequency.Equals(frequency.Value)) return; qsoFrequency=frequency.Value; InvalidateQso(); }; stack.Children.Add(frequency);
        var bandInfo=Label("",13,true); bandInfo.Visibility=Visibility.Collapsed; stack.Children.Add(bandInfo);
        stack.Children.Add(Action("checkBand",()=> { var match=RadioCatalog.Assess(operatingCountry,operatingClass,QsoHz()); bandInfo.Text=BandDescription(match); bandInfo.Visibility=Visibility.Visible; return Task.CompletedTask; },"\uE73E"));
        string callSummary;
        try { callSummary=CurrentOperatingCall().CallSign; } catch(ArgumentException) { callSummary=station.CallSign; }
        var setup=new Expander { Header=$"{callSummary} · {qsoFrequency:0.######} MHz · {CountryName(country)} ({ClassName(operatingClass)})",Content=stack,IsExpanded=operatingDetailsExpanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        setup.Expanding+=(_,_)=>operatingDetailsExpanded=true;
        setup.Collapsed+=(_,_)=> { operatingDetailsExpanded=false; try { setup.Header=$"{CurrentOperatingCall().CallSign} · {qsoFrequency:0.######} MHz · {CountryName(country)} ({ClassName(operatingClass)})"; } catch(ArgumentException) { } };
        PageContent.Children.Add(setup);
        BuildQsoMessages(station);
        BuildCountryRules(country,operatingClass);
    }
    private string BandDescription(BandAssessment match)=>match.Status==BandStatus.Match ? $"{T("bandMatch")} · {match.Band!.Band} · {match.Power!.Watts} W {(match.Power.Basis=="output"?T("powerOutput"):match.Power.Basis)} · {T(match.Band.ConditionsKey)}" : T(match.Status==BandStatus.ClassUnknown?"selectClass":match.Status==BandStatus.CountryUnknown?"unknownCountry":"bandUnlisted");
    private void BuildQsoMessages(StationProfile station)
    {
        var stack=new StackPanel { Spacing=12 };
        string[] stages=["checkFrequency","generalCall","answerCall","signalReport","freeMessage","closeQso"];
        stack.Children.Add(Columns(260,2,Choice(T("qsoStep"),stages.Select(T),(int)qsoStage,i=> { if(qsoStage==(QsoStage)i) return; qsoStage=(QsoStage)i; InvalidateQso(); BuildPage(); }),RadioInput("otherCall",otherCall,v=> { otherCall=v; InvalidateQso(); },true,32)));
        var reports=Columns(220,2,RadioInput("sentRst",sentRst,v=> { sentRst=v; InvalidateQso(); },true,3),RadioInput("receivedRst",receivedRst,v=>receivedRst=v,true,3));
        if(qsoStage==QsoStage.Report) { stack.Children.Add(reports); stack.Children.Add(Label(T("rstHint"),12,true)); }
        if(qsoStage==QsoStage.Message) stack.Children.Add(RadioInput("freeMessage",qsoMessage,v=> { qsoMessage=v; InvalidateQso(); },false,2000));
        var preview=new TextBox { Header=T("qsoPreview"),Text=qsoPreview,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=100,FlowDirection=FlowDirection.LeftToRight };
        var morse=new TextBox { Header=T("morseInput"),Text=qsoMorse,IsReadOnly=true,AcceptsReturn=true,TextWrapping=TextWrapping.Wrap,MinHeight=90,FontFamily=MonoFont,FlowDirection=FlowDirection.LeftToRight };
        morse.Background=Brush("ResultBrush"); morse.MinHeight=preview.MinHeight=130;
        qsoTextBox=preview; qsoCodeBox=morse;
        stack.Children.Add(Action("prepareMessage",()=>
        {
            var call=CurrentOperatingCall();
            if(call.NeedsConfirmation && string.IsNullOrWhiteSpace(manualCall)) { SetStatus(T(call.ExplanationKey)); return Task.CompletedTask; }
            string prepared=CwMessages.Compose(qsoStage,call.CallSign,otherCall,sentRst,station.OperatorName,station.Qth,qsoMessage);
            string encoded=CwMessages.Encode(prepared); qsoPreview=preview.Text=prepared; qsoMorse=morse.Text=encoded;
            SetStatus(BandDescription(RadioCatalog.Assess(operatingCountry,operatingClass,QsoHz()))); return Task.CompletedTask;
        },"\uE8A5",true));
        stack.Children.Add(Columns(300,2,preview,morse));
        var actions=Row();
        actions.Children.Add(Action("play",()=>qsoMorse.Length>0?PlayMorse(qsoMorse,'/'):Task.CompletedTask,"\uE768"));
        actions.Children.Add(Action("stop",()=> { StopPlayback(); return Task.CompletedTask; },"\uE71A"));
        actions.Children.Add(Action("copy",()=> { if(qsoPreview.Length>0) { var data=new DataPackage(); data.SetText(qsoPreview); Clipboard.SetContent(data); SetStatus(T("copied")); } return Task.CompletedTask; },"\uE8C8"));
        actions.Children.Add(Action("exportWav",async()=> { string snapshot=qsoMorse; var settings=preferences; if(snapshot.Length==0) return; string? path=await SavePath("wav","qso"); if(path!=null) await Task.Run(()=>MorseAudio.WriteWav(path,snapshot,new(settings.Wpm,settings.Frequency),'/')); },"\uE74E"));
        stack.Children.Add(actions); WrapActions(actions,stack); stack.Children.Add(Label(T("qsoProcedureNote"),12,true));
        PageContent.Children.Add(Card(stack));
        var record=new StackPanel { Spacing=12 };
        var practice=new ToggleSwitch { Header=T("practiceRecord"),IsOn=practiceLog,OnContent=T("practice"),OffContent=T("externalQso") };
        practice.Toggled+=(_,_)=>practiceLog=practice.IsOn; record.Children.Add(practice);
        record.Children.Add(RadioInput("qsoNote",qsoNote,v=>qsoNote=v,false,4000));
        record.Children.Add(RadioInput("logbook.grid",qsoGrid,v=>qsoGrid=v,true,12));
        record.Children.Add(Label(T("logbook.locationHint"),12,true));
        record.Children.Add(Action("saveQso",()=>
        {
            var call=CurrentOperatingCall();
            if(call.NeedsConfirmation && string.IsNullOrWhiteSpace(manualCall)) { SetStatus(T(call.ExplanationKey)); return Task.CompletedTask; }
            var match=RadioCatalog.Assess(operatingCountry,operatingClass,QsoHz());
            radioStore.Append(new() { OwnCall=call.CallSign,OtherCall=CallSigns.Parse(otherCall).Normalized,CountryId=operatingCountry,Band=match.Band?.Band ?? AdifBands.FromFrequency(QsoHz()) ?? "",FrequencyHz=QsoHz(),SentRst=sentRst,ReceivedRst=receivedRst,Practice=practiceLog,Note=qsoNote,GridSquare=qsoGrid.Trim().ToUpperInvariant() }); SetStatus(T("saved")); return Task.CompletedTask;
        },"\uE74E"));
        var recording=new Expander { Header=T("qsoRecordDetails"),Content=record,IsExpanded=qsoRecordExpanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(recording,"QsoRecord");
        recording.Expanding+=(_,_)=>qsoRecordExpanded=true; recording.Collapsed+=(_,_)=>qsoRecordExpanded=false;
        PageContent.Children.Add(recording);
    }
}
