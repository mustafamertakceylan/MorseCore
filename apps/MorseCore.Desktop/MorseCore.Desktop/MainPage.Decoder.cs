using Microsoft.UI.Xaml.Media;
using MorseCore.AppCore.Decoding;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;

namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    private CancellationTokenSource? decoderOperation, decoderRecordStop;
    private string? decoderPath;
    private DecoderResult? decoderResult;
    private bool decoderWasCapture, decoderHasCaptureDevice = false, decoderUseMicrophone;
    private Button? decoderChooseButton, decoderSolveButton, decoderRecordButton = null, decoderRefreshButton = null, decoderStopRecordButton = null;
    private Control? decoderDevicesChoice = null;
    private TextBlock? decoderSource, decoderProgress;
    private Button? decoderCancelButton;
    private Control? decoderSourceTabs;
    private FrameworkElement? decoderResultPanel;
    private TextBox? decoderText, decoderMorseText;
    private StackPanel? decoderDetails;
    private bool decoderAutomaticTone = true, decoderAutomaticSpeed = true;
    private double decoderFrequency = 700, decoderWpm = 20, decoderThreshold = 35;
#if MORSE_WINUI
    private string decoderDeviceId = "";
#endif
    private int decoderGeneration;

    private void BuildAudioDecoderSection()
    {
        decoderChooseButton = decoderSolveButton = decoderRecordButton = decoderRefreshButton = decoderStopRecordButton = null;
        decoderDevicesChoice = null;
        var stack = new StackPanel { Spacing = 14 };
        stack.Children.Add(Label(T("uxDecoderIntro"), 13, true));
        decoderSourceTabs = new ContentControl
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Content = WorkspaceTabs("DecoderSourceTabs", ["uxWaveSource", "uxMicrophoneSource"], decoderUseMicrophone ? 1 : 0, selected =>
            {
                if (decoderOperation != null || decoderUseMicrophone == (selected == 1)) return;
                decoderUseMicrophone = selected == 1;
                decoderResult = null;
                BuildPage();
            })
        };
        stack.Children.Add(decoderSourceTabs);
        stack.Children.Add(AlphabetChoice());
        var sourceControls = new StackPanel { Spacing = 10 };
        if (!decoderUseMicrophone)
        {
            var pick = Action("decoderChoose", async () =>
            {
                var picker = new FileOpenPicker(); picker.FileTypeFilter.Add(".wav"); InitializePicker(picker);
                var selected = await picker.PickSingleFileAsync();
                if (selected is null || disposed) return;
                decoderPath = selected.Path; decoderWasCapture = false; decoderResult = null; BuildPage();
            }, "\uE8B7");
            Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(pick, "DecoderChooseWav");
            decoderChooseButton = pick;
            sourceControls.Children.Add(pick);
        }
        var fileName = Label(DecoderSourceName(), 13, true);
        decoderSource = fileName;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(fileName, "DecoderSource");
        fileName.FlowDirection = FlowDirection.LeftToRight;
        if (!decoderUseMicrophone || decoderWasCapture) sourceControls.Children.Add(fileName);
        if (decoderUseMicrophone) BuildDecoderCapture(sourceControls);
        stack.Children.Add(sourceControls);
        var tone = new NumberBox { Header = T("decoderTone"), Minimum = 200, Maximum = 2000, Value = decoderFrequency, IsEnabled = !decoderAutomaticTone, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        tone.ValueChanged += (_, _) => { if (double.IsFinite(tone.Value)) decoderFrequency = Math.Clamp(tone.Value, 200, 2000); };
        var autoTone = new ToggleSwitch { Header = T("decoderAutoTone"), IsOn = decoderAutomaticTone };
        autoTone.Toggled += (_, _) => { decoderAutomaticTone = autoTone.IsOn; tone.IsEnabled = !autoTone.IsOn; };
        var speed = new NumberBox { Header = T("speed"), Minimum = 5, Maximum = 60, Value = decoderWpm, IsEnabled = !decoderAutomaticSpeed, SpinButtonPlacementMode = NumberBoxSpinButtonPlacementMode.Compact };
        speed.ValueChanged += (_, _) => { if (double.IsFinite(speed.Value)) decoderWpm = Math.Clamp(speed.Value, 5, 60); };
        var autoSpeed = new ToggleSwitch { Header = T("decoderAutoSpeed"), IsOn = decoderAutomaticSpeed };
        autoSpeed.Toggled += (_, _) => { decoderAutomaticSpeed = autoSpeed.IsOn; speed.IsEnabled = !autoSpeed.IsOn; };
        var advanced = new StackPanel { Spacing = 10 };
        advanced.Children.Add(Label(T("decoderHint"), 13, true));
        advanced.Children.Add(Label(T("decoderCaptureHint"), 13, true));
        advanced.Children.Add(Columns(240,2,autoTone,autoSpeed,tone,speed));
        var threshold = new Slider { Header = T("decoderThreshold"), Minimum = 10, Maximum = 70, Value = decoderThreshold, StepFrequency = 5 };
        threshold.ValueChanged += (_, _) => decoderThreshold = threshold.Value;
        advanced.Children.Add(threshold);
        advanced.Children.Add(Label(T("decoderThresholdHint"), 12, true));
        stack.Children.Add(new Expander { Header = T("uxAdvanced"), Content = advanced, HorizontalAlignment = HorizontalAlignment.Stretch });
        var controls = Row();
        var solve = Action("decoderSolve", () => decoderPath is { } path ? DecodeAudioPath(path) : Task.CompletedTask, "\uE8D4", true);
        solve.IsEnabled = decoderPath != null;
        decoderSolveButton = solve;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(solve, "DecoderSolve");
        if (!decoderUseMicrophone) controls.Children.Add(solve);
        var cancel = Action("decoderCancel", () =>
        {
            CleanupAudioDecoder();
            // Recreate the controls with the new generation; pending old callbacks remain stale.
            BuildPage(); SetStatus(T("ready")); return Task.CompletedTask;
        }, "\uE711");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(cancel, "DecoderCancel");
        decoderCancelButton = cancel;
        controls.Children.Add(cancel);
        stack.Children.Add(controls); WrapActions(controls, stack);
        decoderProgress = Label("", 13, true);
        decoderProgress.Visibility = Visibility.Collapsed;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(decoderProgress, "DecoderProgress");
        stack.Children.Add(decoderProgress);
        PageContent.Children.Add(Card(stack));
        var results = new StackPanel { Spacing = 12 };
        results.Children.Add(Label(T("uxDecoderResult"), 18));
        var output = new TextBox { Header = T("decodedText"), Text = decoderResult?.Text ?? "", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 100, FlowDirection = PlainDirection, FontSize = 18 };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(output, "DecoderText");
        decoderText = output;
        var code = new TextBox { Header = T("morseInput"), Text = decoderResult?.Morse ?? "", IsReadOnly = true, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 72, FlowDirection = FlowDirection.LeftToRight, FontFamily = new FontFamily("Consolas") };
        decoderMorseText = code;
        code.Background=Brush("ResultBrush"); code.MinHeight=output.MinHeight=120;
        results.Children.Add(Columns(300,2,output,code));
        decoderDetails = new StackPanel { Spacing = 8 };
        if (decoderResult is { } result)
        {
            string tempo = result.TimingAmbiguous ? "?" : result.WordsPerMinute.ToString("0.#");
            decoderDetails.Children.Add(Label($"{result.FrequencyHz:0} Hz · {tempo} WPM · {result.DurationSeconds:0.#} s · {T("decoderUncertain")}: {result.UncertainCharacters}", 12, true));
            if (result.TimingAmbiguous) decoderDetails.Children.Add(Label(T("decoderAmbiguous"), 13, true));
        }
        results.Children.Add(decoderDetails);
        var exports = Row();
        exports.Children.Add(Action("copy", () =>
        {
            if (decoderResult != null) { var data = new DataPackage(); data.SetText(decoderResult.Text); Clipboard.SetContent(data); SetStatus(T("copied")); }
            return Task.CompletedTask;
        }));
        exports.Children.Add(Action("save", async () =>
        {
            string? value = decoderResult?.Text; if (value is null) return;
            string? path = await SavePath("txt", "decoded-morse");
            if (path != null) { await File.WriteAllTextAsync(path, value, lifetime.Token); SetStatus(T("saved")); }
        }));
        results.Children.Add(exports); WrapActions(exports, results);
        decoderResultPanel = Card(results);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(decoderResultPanel, "DecoderResult");
        PageContent.Children.Add(decoderResultPanel);
        UpdateDecoderControls();
    }

    private void BuildDecoderCapture(StackPanel stack)
    {
#if MORSE_WINUI
        int enumerationGeneration = decoderGeneration;
        var devices = new ComboBox { Header = T("decoderDevice"), HorizontalAlignment = HorizontalAlignment.Stretch, DisplayMemberPath = nameof(AudioCaptureDevice.Name) };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(devices, "DecoderInputs");
        var start = Action("decoderRecord", async () =>
        {
            if (devices.SelectedItem is not AudioCaptureDevice device || decoderOperation != null) return;
            StopPlayback();
            int generation = decoderGeneration;
            using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            using var stop = new CancellationTokenSource();
            decoderResult = null;
            decoderOperation = operation; decoderRecordStop = stop; UpdateDecoderControls();
            string? path = null;
            try
            {
                SetDecoderStatus(T("decoderRecording"));
                path = await MorseAudioCapture.RecordAsync(device.Id, stop.Token, operation.Token);
                if (disposed || generation != decoderGeneration) return;
                decoderPath = null; decoderWasCapture = true;
                if (decoderSource != null) decoderSource.Text = DecoderSourceName();
                decoderRecordStop = null; decoderOperation = null;
                await DecodeAudioPath(path);
            }
            catch (OperationCanceledException) { }
            catch (UnauthorizedAccessException) { if (!disposed && generation == decoderGeneration) SetDecoderStatus(T("decoderPermission")); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { if (!disposed && generation == decoderGeneration) SetDecoderStatus(T("decoderCaptureError")); }
            finally
            {
                if (ReferenceEquals(decoderOperation, operation)) decoderOperation = null;
                if (ReferenceEquals(decoderRecordStop, stop)) decoderRecordStop = null;
                if (!disposed && generation == decoderGeneration) UpdateDecoderControls();
                if (path != null) MorseAudioCapture.Delete(path);
            }
        }, "\uE720", true);
        decoderRecordButton = start; decoderDevicesChoice = devices;
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(start, "DecoderRecord");
        start.IsEnabled = false;
        decoderHasCaptureDevice = false;
        devices.SelectionChanged += (_, _) => { decoderDeviceId = (devices.SelectedItem as AudioCaptureDevice)?.Id ?? ""; decoderHasCaptureDevice = devices.SelectedItem != null; UpdateDecoderControls(); };
        var deviceStatus = Label("", 12, true);
        async Task RefreshDevices()
        {
            try
            {
                var available = await MorseAudioCapture.DevicesAsync();
                if (disposed || enumerationGeneration != decoderGeneration) return;
                devices.ItemsSource = available;
                devices.SelectedItem = available.FirstOrDefault(d => d.Id == decoderDeviceId) ?? available.FirstOrDefault();
                deviceStatus.Text = available.Count == 0 ? T("decoderNoDevice") : "";
                decoderHasCaptureDevice = devices.SelectedItem != null; UpdateDecoderControls();
            }
            catch (UnauthorizedAccessException) { if (!disposed && enumerationGeneration == decoderGeneration) deviceStatus.Text = T("decoderPermission"); }
            catch (Exception ex) when (ex is not OutOfMemoryException) { if (!disposed && enumerationGeneration == decoderGeneration) deviceStatus.Text = T("decoderCaptureError"); }
        }
        stack.Children.Add(devices); stack.Children.Add(deviceStatus);
        var controls = Row(); controls.Children.Add(start);
        decoderStopRecordButton = Action("decoderStopRecord", () => { decoderRecordStop?.Cancel(); return Task.CompletedTask; }, "\uE71A");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(decoderStopRecordButton, "DecoderStopRecord");
        decoderRefreshButton = Action("decoderRefreshDevices", RefreshDevices, "\uE72C");
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetAutomationId(decoderRefreshButton, "DecoderRefreshInputs");
        controls.Children.Add(decoderStopRecordButton);
        controls.Children.Add(decoderRefreshButton);
        stack.Children.Add(controls); WrapActions(controls, stack);
        _ = RefreshDevices();
#else
        stack.Children.Add(Label(T("decoderWindowsCapture"), 13, true));
#endif
    }

    private async Task DecodeAudioPath(string path)
    {
        if (decoderOperation != null) return;
        int generation = decoderGeneration;
        var options = new DecoderOptions(decoderAutomaticTone ? null : decoderFrequency, decoderAutomaticSpeed ? null : decoderWpm, decoderThreshold / 100, preferences.Alphabet);
        using var operation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        decoderOperation = operation;
        decoderResult = null;
        if (decoderText != null) decoderText.Text = "";
        if (decoderMorseText != null) decoderMorseText.Text = "";
        decoderDetails?.Children.Clear();
        UpdateDecoderControls(); SetDecoderStatus(T("decoderWorking"));
        try
        {
            var result = await MorseDecoder.DecodeFileAsync(path, options, operation.Token);
            if (disposed || generation != decoderGeneration) return;
            decoderResult = result; decoderOperation = null; BuildPage(); SetStatus(T("ready"));
            Root.UpdateLayout();
            if(decoderResultPanel is { Visibility:Visibility.Visible } panel)
            {
                double top=panel.TransformToVisual(ContentScroll).TransformPoint(new Windows.Foundation.Point(0,0)).Y+ContentScroll.VerticalOffset;
                ContentScroll.ChangeView(null,Math.Max(0,top-16),null,true);
            }
        }
        catch (OperationCanceledException) { }
        catch (InvalidDataException ex)
        {
            if (!disposed && generation == decoderGeneration)
                SetDecoderStatus(T(ex.Message.StartsWith("decoder", StringComparison.Ordinal) && text.Keys.Contains(ex.Message) ? ex.Message : "decoderFormat"));
        }
        catch (IOException) { if (!disposed && generation == decoderGeneration) SetDecoderStatus(T("fileError")); }
        catch (UnauthorizedAccessException) { if (!disposed && generation == decoderGeneration) SetDecoderStatus(T("fileError")); }
        finally
        {
            if (ReferenceEquals(decoderOperation, operation)) decoderOperation = null;
            if (!disposed && generation == decoderGeneration) UpdateDecoderControls();
        }
    }

    private void CleanupAudioDecoder()
    {
        decoderGeneration++;
        decoderRecordStop?.Cancel(); decoderOperation?.Cancel();
        // Owners dispose their cancellation sources once asynchronous work has actually released the device.
        decoderRecordStop = null; decoderOperation = null;
        UpdateDecoderControls();
    }

    private string DecoderSourceName() => decoderWasCapture ? T("decoderRecordedSource") : decoderPath is null ? T("decoderNoFile") : Path.GetFileName(decoderPath);

    private void SetDecoderStatus(string value)
    {
        SetStatus(value);
        if (decoderProgress != null) { decoderProgress.Text = value; decoderProgress.Visibility = Visibility.Visible; }
    }

    private void UpdateDecoderControls()
    {
        bool idle = decoderOperation is null;
        if (decoderSourceTabs != null) decoderSourceTabs.IsEnabled = idle;
        if (decoderCancelButton != null) decoderCancelButton.Visibility = idle ? Visibility.Collapsed : Visibility.Visible;
        if (decoderResultPanel != null) decoderResultPanel.Visibility = idle && decoderResult != null ? Visibility.Visible : Visibility.Collapsed;
        if (decoderProgress != null && idle && decoderResult != null) decoderProgress.Visibility = Visibility.Collapsed;
        if (decoderChooseButton != null) decoderChooseButton.IsEnabled = idle;
        if (decoderSolveButton != null) decoderSolveButton.IsEnabled = idle && decoderPath != null;
        if (decoderRecordButton != null) decoderRecordButton.IsEnabled = idle && decoderHasCaptureDevice;
        if (decoderRefreshButton != null) decoderRefreshButton.IsEnabled = idle;
        if (decoderDevicesChoice != null) decoderDevicesChoice.IsEnabled = idle;
        if (decoderStopRecordButton != null)
        {
            decoderStopRecordButton.IsEnabled = decoderRecordStop != null;
            decoderStopRecordButton.Visibility = decoderRecordStop != null ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
