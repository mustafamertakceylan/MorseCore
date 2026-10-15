using System.Globalization;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Path=System.IO.Path;
using MorseCore.AppCore.Radio;
using Windows.Storage.Pickers;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private string logCall="",logBand="",logFrom="",logThrough="";
    private LogQuery? appliedLogQuery;
    private readonly List<string?> logCursors=[null];
    private int logPageIndex;
    private bool logShowMap,logLoading,logFiltersExpanded,logFilesExpanded;
    private QsoRecord? logDraft;
    private AdifDocument? importDocument;
    private ImportPreview? importPreview;
    private string importOwnCall="",importCountry="OTHER",importFileName="";
    private bool importPractice=true;

    private LogQuery CurrentLogQuery()
    {
        DateOnly? Date(string value)
        {
            if(string.IsNullOrWhiteSpace(value)) return null;
            if(!DateOnly.TryParseExact(value.Trim(),"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)) throw new ArgumentException(T("logbook.dateHint"));
            return parsed;
        }
        var query=new LogQuery(showPracticeLog,logCall.Trim(),logBand.Trim(),Date(logFrom),Date(logThrough));
        if(query.From>query.Through) throw new ArgumentException(T("logbook.dateHint"));
        return query;
    }
    private void ResetLogPage() { logCursors.Clear(); logCursors.Add(null); logPageIndex=0; }
    private void BuildQsoLog()
    {
        PageContent.Children.Add(Choice(T("recordType"),[T("practice"),T("externalQso")],showPracticeLog?0:1,i=>
        {
            if(showPracticeLog==(i==0)) return; showPracticeLog=i==0;
            if(appliedLogQuery!=null) appliedLogQuery=appliedLogQuery with { Practice=showPracticeLog };
            ResetLogPage(); BuildPage();
        }));
        var filters=new StackPanel { Spacing=10 };
        var callInput=RadioInput("logbook.searchCall",logCall,v=>logCall=v,true,32);
        var bandInput=RadioInput("logbook.band",logBand,v=>logBand=v,true,30);
        var fromInput=RadioInput("logbook.from",logFrom,v=>logFrom=v,true,10);
        var throughInput=RadioInput("logbook.through",logThrough,v=>logThrough=v,true,10);
        filters.Children.Add(Columns(260,2,callInput,bandInput));
        filters.Children.Add(Columns(260,2,fromInput,throughInput));
        filters.Children.Add(Label(T("logbook.dateHint"),12,true));
        var filterActions=Row();
        filterActions.Children.Add(Action("logbook.search",()=>
        {
            // TextChanged is deferred by native TextBox implementations. Read the visible controls at invocation time.
            logCall=callInput.Text; logBand=bandInput.Text; logFrom=fromInput.Text; logThrough=throughInput.Text;
            appliedLogQuery=CurrentLogQuery(); ResetLogPage(); BuildPage(); return Task.CompletedTask;
        },"\uE721",true));
        filterActions.Children.Add(Action("logbook.reset",()=> { logCall=logBand=logFrom=logThrough=""; appliedLogQuery=null; ResetLogPage(); BuildPage(); return Task.CompletedTask; }));
        filters.Children.Add(filterActions); WrapActions(filterActions,filters);
        var search=new Expander { Header=T("logbook.filters"),Content=filters,IsExpanded=logFiltersExpanded || appliedLogQuery!=null,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        search.Expanding+=(_,_)=>logFiltersExpanded=true; search.Collapsed+=(_,_)=>logFiltersExpanded=false;
        PageContent.Children.Add(Action("logbook.map",()=> { logShowMap=!logShowMap; BuildPage(); return Task.CompletedTask; },"\uE707",logShowMap));
        PageContent.Children.Add(search);
        var files=new StackPanel { Spacing=10 };
        var actions=Row();
        foreach(bool adif in new[]{false,true})
            actions.Children.Add(Action(adif?"exportAdif":"exportCsv",async()=>
            {
                bool practice=showPracticeLog; string? path=await SavePath(adif?"adi":"csv",practice?"morsecore-practice":"morsecore-qso");
                if(path!=null) { await Task.Run(()=>radioStore.ExportAsync(path,practice,adif,lifetime.Token,replaceExisting:true)); SetStatus(T("saved")); }
            },"\uE74E"));
        actions.Children.Add(Action("logbook.import",OpenAdifImport,"\uE8B5")); files.Children.Add(actions); WrapActions(actions,files);
        files.Children.Add(Label(T("logbook.browseHint")+" "+T("logbook.exportAll"),12,true));
        var transfer=new Expander { Header=T("radioFiles"),Content=files,IsExpanded=logFilesExpanded,HorizontalAlignment=HorizontalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(transfer,"LogFileActions");
        transfer.Expanding+=(_,_)=>logFilesExpanded=true; transfer.Collapsed+=(_,_)=>logFilesExpanded=false;
        PageContent.Children.Add(transfer);
        if(importDocument!=null && importPreview!=null) BuildImportPreview();
        if(logDraft!=null) BuildLogEditor();
        var host=new StackPanel { Spacing=10 }; PageContent.Children.Add(host);
        var loading=Label(T("busy"),13,true); host.Children.Add(loading);
        var query=(appliedLogQuery??new LogQuery(showPracticeLog)) with { Before=logCursors[logPageIndex] };
        _=LoadLogRows(host,query,logShowMap);
    }
    private async Task LoadLogRows(StackPanel host,LogQuery query,bool map)
    {
        try
        {
            var token=lifetime.Token;
            var result=await Task.Run(()=>radioStore.Query(query,token));
            LogMap? positions=map?await Task.Run(()=>radioStore.Map(query,500,token)):null;
            if(disposed || !PageContent.Children.Contains(host)) return;
            host.Children.Clear();
            host.Children.Add(Label($"{T("logbook.matches")}: {result.Total} · {T("logbook.page")} {logPageIndex+1}",13,true));
            if(positions!=null) BuildLogMap(host,positions);
            if(result.Entries.Count==0) host.Children.Add(Label(T("noRecords"),14,true));
            foreach(var entry in result.Entries)
            {
                var stack=new StackPanel { Spacing=6 };
                var title=Label($"{entry.Utc.UtcDateTime.ToString(entry.TimeHasSeconds==false?"yyyy-MM-dd HH:mm":"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)} UTC · {entry.OwnCall} → {entry.OtherCall}",15); title.FlowDirection=FlowDirection.LeftToRight; stack.Children.Add(title);
                var details=Label($"{(entry.FrequencyHz>0?(entry.FrequencyHz/1_000_000m).ToString("0.######",CultureInfo.InvariantCulture)+" MHz":T("logbook.frequencyUnknown"))} · {entry.Band} · CW · {entry.SentRst} / {entry.ReceivedRst}",13,true); details.FlowDirection=FlowDirection.LeftToRight; stack.Children.Add(details);
                if(entry.GridSquare.Length>0) { var locator=Label(entry.GridSquare,12,true); locator.FlowDirection=FlowDirection.LeftToRight; stack.Children.Add(locator); }
                if(entry.Note.Length>0) stack.Children.Add(Label(entry.Note,13));
                stack.Children.Add(Action("logbook.edit",()=> { logDraft=entry; BuildPage(); return Task.CompletedTask; },"\uE70F")); host.Children.Add(Card(stack));
            }
            var navigation=Row();
            var previous=Action("logbook.previous",()=> { if(logPageIndex>0) { logPageIndex--; BuildPage(); } return Task.CompletedTask; }); previous.IsEnabled=logPageIndex>0; navigation.Children.Add(previous);
            var next=Action("logbook.next",()=>
            {
                if(result.NextCursor==null) return Task.CompletedTask;
                if(logCursors.Count>logPageIndex+1) logCursors.RemoveRange(logPageIndex+1,logCursors.Count-logPageIndex-1);
                logCursors.Add(result.NextCursor); logPageIndex++; BuildPage(); return Task.CompletedTask;
            }); next.IsEnabled=result.NextCursor!=null; navigation.Children.Add(next); host.Children.Add(navigation); WrapActions(navigation,host);
        }
        catch(OperationCanceledException) { }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException)
        { if(!disposed && PageContent.Children.Contains(host)) { host.Children.Clear(); host.Children.Add(Label(T("radioDataError"),14,true)); } }
    }
    private void BuildLogEditor()
    {
        var original=logDraft!; var draft=original; string utc=original.Utc.UtcDateTime.ToString(original.TimeHasSeconds==false?"yyyy-MM-dd HH:mm":"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        string frequency=original.FrequencyHz==0?"":(original.FrequencyHz/1_000_000m).ToString("0.######",CultureInfo.InvariantCulture);
        string latitude=original.Latitude?.ToString("R",CultureInfo.InvariantCulture)??"",longitude=original.Longitude?.ToString("R",CultureInfo.InvariantCulture)??"";
        var stack=new StackPanel { Spacing=10 }; stack.Children.Add(Label(T("logbook.edit"),18));
        var inputs=new Dictionary<string,TextBox>(); var fields=new List<FrameworkElement>();
        void Input(string key,string value,bool ltr=true,int maximum=80)
        { var input=RadioInput(key,value,_=> { },ltr,maximum); inputs.Add(key,input); fields.Add(input); }
        Input("logbook.utc",utc,maximum:19); Input("callSign",draft.OwnCall,maximum:32); Input("otherCall",draft.OtherCall,maximum:32);
        Input("qsoFrequency",frequency,maximum:18); Input("logbook.band",draft.Band,maximum:30);
        Input("sentRst",draft.SentRst,maximum:3); Input("receivedRst",draft.ReceivedRst,maximum:3);
        var countries=RadioCatalog.Data.Countries; var operatingCountryInput=Choice(T("operatingCountry"),countries.Select(CountryName),Math.Max(0,Array.FindIndex(countries,c=>c.Id==draft.CountryId)),_=> { }); fields.Add(operatingCountryInput);
        var practice=new ToggleSwitch { Header=T("practiceRecord"),IsOn=draft.Practice,OnContent=T("practice"),OffContent=T("externalQso") }; practice.Toggled+=(_,_)=>draft=draft with { Practice=practice.IsOn }; fields.Add(practice);
        Input("qsoNote",draft.Note,false,4000); Input("logbook.grid",draft.GridSquare,maximum:12);
        Input("logbook.latitude",latitude,maximum:24); Input("logbook.longitude",longitude,maximum:24);
        stack.Children.Add(Columns(260,2,fields.ToArray())); stack.Children.Add(Label(T("logbook.locationHint"),12,true));
        var error=Label("",13,true); stack.Children.Add(error); var actions=Row();
        actions.Children.Add(Action("logbook.save",async()=>
        {
            try
            {
                utc=inputs["logbook.utc"].Text; frequency=inputs["qsoFrequency"].Text;
                latitude=inputs["logbook.latitude"].Text; longitude=inputs["logbook.longitude"].Text;
                draft=draft with { OwnCall=inputs["callSign"].Text,OtherCall=inputs["otherCall"].Text,Band=inputs["logbook.band"].Text,SentRst=inputs["sentRst"].Text,ReceivedRst=inputs["receivedRst"].Text,Note=inputs["qsoNote"].Text,GridSquare=inputs["logbook.grid"].Text,CountryId=countries[operatingCountryInput.SelectedIndex].Id,Practice=practice.IsOn };
                if(!DateTime.TryParseExact(utc,["yyyy-MM-dd HH:mm:ss","yyyy-MM-dd HH:mm"],CultureInfo.InvariantCulture,DateTimeStyles.None,out var instant)) throw new ArgumentException();
                long hz=0; if(frequency.Trim().Length>0) { if(!decimal.TryParse(frequency,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out decimal mhz) || mhz is <=0 or >300000 || decimal.Round(mhz*1_000_000,0)!=mhz*1_000_000) throw new ArgumentException(); hz=(long)(mhz*1_000_000); }
                double? Coordinate(string value)=>value.Trim().Length==0?null:double.TryParse(value,NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out double number)?number:throw new ArgumentException();
                string band=draft.Band.Trim(); if(band.Length>0) band=AdifBands.Normalize(band)??throw new ArgumentException(); if(band.Length==0) band=AdifBands.FromFrequency(hz)??"";
                if(hz>0 && band.Length>0 && !AdifBands.Contains(band,hz)) throw new ArgumentException();
                var saved=draft with { Utc=new DateTimeOffset(DateTime.SpecifyKind(instant,DateTimeKind.Utc)),TimeHasSeconds=utc.Length==19,FrequencyHz=hz,Band=band,Latitude=Coordinate(latitude),Longitude=Coordinate(longitude) };
                await Task.Run(()=>radioStore.Update(saved)); logDraft=null; ResetLogPage(); BuildPage(); SetStatus(T("saved"));
            }
            catch(Exception e) when(e is ArgumentException or InvalidDataException or OverflowException) { error.Text=T("logbook.editInvalid"); }
        },"\uE74E",true));
        actions.Children.Add(Action("logbook.cancel",()=> { logDraft=null; BuildPage(); return Task.CompletedTask; })); stack.Children.Add(actions); WrapActions(actions,stack); PageContent.Children.Add(Card(stack));
    }
    private async Task OpenAdifImport()
    {
        if(logLoading) return;
        var settings=new StackPanel { Spacing=10 };
        if(importOwnCall.Length==0 && ActiveStation is { } active) { importOwnCall=active.CallSign; importCountry=active.CountryId; }
        settings.Children.Add(Label(T("logbook.importHint"),13,true)); var ownCallInput=RadioInput("logbook.fallbackCall",importOwnCall,_=> { },true,32); settings.Children.Add(ownCallInput);
        var countries=RadioCatalog.Data.Countries; var countryInput=Choice(T("operatingCountry"),countries.Select(CountryName),Math.Max(0,Array.FindIndex(countries,c=>c.Id==importCountry)),_=> { }); settings.Children.Add(countryInput);
        var kindInput=Choice(T("logbook.unmarked"),[T("practice"),T("externalQso")],importPractice?0:1,_=> { }); settings.Children.Add(kindInput);
        var dialog=new ContentDialog { XamlRoot=XamlRoot,Title=T("logbook.import"),Content=settings,PrimaryButtonText=T("logbook.chooseAdi"),CloseButtonText=T("logbook.cancel"),DefaultButton=ContentDialogButton.Close,RequestedTheme=RequestedTheme };
        if(await dialog.ShowAsync()!=ContentDialogResult.Primary) return;
        importOwnCall=ownCallInput.Text; importCountry=countries[countryInput.SelectedIndex].Id; importPractice=kindInput.SelectedIndex==0;
        var picker=new FileOpenPicker(); picker.FileTypeFilter.Add(".adi"); picker.FileTypeFilter.Add(".adif"); InitializePicker(picker); var file=await picker.PickSingleFileAsync(); if(file==null) return;
        logLoading=true; SetStatus(T("busy"));
        try
        {
            var options=new AdifImportOptions(importOwnCall,importCountry,importPractice);
            var parsed=await Task.Run(()=>
            {
                if(new FileInfo(file.Path).Length>AdifImport.MaximumBytes) throw new InvalidDataException();
                return AdifImport.Parse(File.ReadAllText(file.Path),options,lifetime.Token);
            });
            var preview=await Task.Run(()=>radioStore.PreviewImport(parsed.Records,lifetime.Token));
            if(disposed) return; importDocument=parsed; importPreview=preview; importFileName=Path.GetFileName(file.Path); BuildPage(); SetStatus(T("logbook.review"));
        }
        catch(Exception e) when(e is IOException or InvalidDataException or UnauthorizedAccessException or ArgumentException) { SetStatus(T("logbook.importInvalid")); }
        finally { logLoading=false; }
    }
    private void BuildImportPreview()
    {
        var document=importDocument!; var preview=importPreview!; var stack=new StackPanel { Spacing=10 };
        stack.Children.Add(Label(T("logbook.review")+" · "+importFileName,18));
        int practice=preview.Candidates.Count(e=>e.Practice),actual=preview.Candidates.Count-practice;
        stack.Children.Add(Label($"{T("logbook.importTotal")}: {document.Total} · {T("logbook.ready")}: {preview.Candidates.Count} · {T("logbook.duplicates")}: {preview.Duplicates} · {T("logbook.rejected")}: {document.Total-document.Records.Count}",13));
        stack.Children.Add(Label($"{T("practice")}: {practice} · {T("externalQso")}: {actual}",13));
        stack.Children.Add(Label(T("logbook.duplicateHint"),12,true));
        if(document.IgnoredFields>0) stack.Children.Add(Label($"{T("logbook.ignored")}: {document.IgnoredFields}",12,true));
        foreach(var issue in document.Issues.Take(20)) stack.Children.Add(Label($"#{issue.Record} · {T(issue.Key)} · {issue.Field}",12,true));
        if(document.Issues.Count>20) stack.Children.Add(Label(T("logbook.moreIssues")+" "+(document.Issues.Count-20),12,true));
        foreach(var sample in preview.Candidates.Take(3)) { var row=Label($"{sample.Utc.UtcDateTime.ToString(sample.TimeHasSeconds==false?"yyyy-MM-dd HH:mm":"yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture)} UTC · {sample.OwnCall} → {sample.OtherCall} · {sample.Band}",12); row.FlowDirection=FlowDirection.LeftToRight; stack.Children.Add(row); }
        var actions=Row(); var confirm=Action("logbook.confirm",async()=>
        {
            if(logLoading) return; logLoading=true;
            try
            {
                var added=await Task.Run(()=>radioStore.Import(preview.Candidates,lifetime.Token));
                importDocument=null; importPreview=null; ResetLogPage(); BuildPage(); SetStatus($"{T("logbook.imported")}: {added.Added} · {T("logbook.duplicates")}: {preview.Duplicates+added.Duplicates}");
            }
            finally { logLoading=false; }
        },"\uE73E",true); confirm.IsEnabled=preview.Candidates.Count>0; actions.Children.Add(confirm);
        actions.Children.Add(Action("logbook.cancel",()=> { importDocument=null; importPreview=null; BuildPage(); return Task.CompletedTask; })); stack.Children.Add(actions); WrapActions(actions,stack); PageContent.Children.Add(Card(stack));
    }
    private void BuildLogMap(StackPanel host,LogMap map)
    {
        var stack=new StackPanel { Spacing=8 }; stack.Children.Add(Label(T("logbook.map"),18));
        stack.Children.Add(Label($"{T("logbook.located")}: {map.Located}/{map.Total} · {T("logbook.shown")}: {map.Entries.Count}",12,true)); stack.Children.Add(Label(T("logbook.mapHint"),12,true));
        var canvas=new Canvas { Height=280,HorizontalAlignment=HorizontalAlignment.Stretch,Background=Brush("CanvasBrush"),FlowDirection=FlowDirection.LeftToRight,MinWidth=200 };
        void Draw()
        {
            double width=canvas.ActualWidth; if(width<1) return; const double height=280;
            canvas.Children.Clear();
            double X(double lon)=>(lon+180)/360*width; double Y(double lat)=>(90-lat)/180*height;
            foreach(var ring in WorldOutline.Data.Rings)
            {
                var polygon=new Polygon { Fill=Brush("PaneBrush"),Stroke=Brush("LineBrush"),StrokeThickness=0.7,IsHitTestVisible=false };
                foreach(var coordinate in ring) polygon.Points.Add(new Windows.Foundation.Point(X(coordinate[0]),Y(coordinate[1])));
                canvas.Children.Add(polygon);
            }
            for(int longitude=-180;longitude<=180;longitude+=60)
            {
                canvas.Children.Add(new Line { X1=X(longitude),X2=X(longitude),Y1=0,Y2=height,Stroke=Brush("LineBrush"),StrokeThickness=1 });
                var label=Label(longitude.ToString(CultureInfo.InvariantCulture)+"°",10,true); Canvas.SetLeft(label,Math.Clamp(X(longitude)+3,2,Math.Max(2,width-32))); Canvas.SetTop(label,height-18); canvas.Children.Add(label);
            }
            for(int latitude=-60;latitude<=60;latitude+=30)
            {
                canvas.Children.Add(new Line { X1=0,X2=width,Y1=Y(latitude),Y2=Y(latitude),Stroke=Brush(latitude==0?"MutedBrush":"LineBrush"),StrokeThickness=1 });
                var label=Label(latitude.ToString(CultureInfo.InvariantCulture)+"°",10,true); Canvas.SetLeft(label,3); Canvas.SetTop(label,Y(latitude)+2); canvas.Children.Add(label);
            }
            foreach(var entry in map.Entries)
            {
                var point=Locator.Position(entry)!.Value; var dot=new Ellipse { Width=9,Height=9,Fill=Brush("AccentInkBrush") };
                Canvas.SetLeft(dot,Math.Clamp(X(point.Longitude)-4.5,0,Math.Max(0,width-9))); Canvas.SetTop(dot,Math.Clamp(Y(point.Latitude)-4.5,0,height-9));
                string description=$"{entry.OtherCall} · {point.Latitude:0.####}, {point.Longitude:0.####} · {entry.GridSquare}";
                ToolTipService.SetToolTip(dot,description); Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(dot,description); canvas.Children.Add(dot);
            }
        }
        canvas.SizeChanged+=(_,_)=>Draw(); stack.Children.Add(canvas);
        stack.Children.Add(new HyperlinkButton { Content=T("logbook.mapCredit"),NavigateUri=new Uri("https://www.naturalearthdata.com/about/terms-of-use/"),FontSize=11,Padding=new Thickness(0),HorizontalAlignment=HorizontalAlignment.Left,Foreground=Brush("MutedBrush") });
        foreach(var entry in map.Entries.Take(10))
        {
            var point=Locator.Position(entry)!.Value; var row=Label($"{entry.OtherCall} · {point.Latitude.ToString("0.####",CultureInfo.InvariantCulture)}, {point.Longitude.ToString("0.####",CultureInfo.InvariantCulture)} · {entry.GridSquare}",12,true); row.FlowDirection=FlowDirection.LeftToRight; stack.Children.Add(row);
        }
        host.Children.Add(Card(stack));
    }
}
