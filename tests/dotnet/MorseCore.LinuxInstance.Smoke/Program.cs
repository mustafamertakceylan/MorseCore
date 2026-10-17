using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using MorseCore.Desktop;

if(!OperatingSystem.IsLinux()) throw new PlatformNotSupportedException("This smoke verifies real Linux file locks and user-only IPC.");
if(args.Length>0)
{
    var child=await LinuxInstanceGate.Acquire();
    using var childGate=child.Gate;
    if(args[0]=="secondary") return !child.Continue && child.Gate is null ? 0 : 10;
    if(args[0]=="isolated") return child.Continue && child.Gate is not null ? 0 : 11;
    if(args[0]=="crash-primary")
    {
        if(!child.Continue || child.Gate is null) return 12;
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_HOME")!,"ready"),"1");
        await Task.Delay(Timeout.InfiniteTimeSpan);
    }
    return 13;
}
string root=Path.Combine(Environment.GetEnvironmentVariable("MORSECORE_INSTANCE_SMOKE_ROOT") ?? Path.GetTempPath(),"MorseCore-LinuxInstanceSmoke-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
Environment.SetEnvironmentVariable("MORSECORE_DESKTOP_HOME",root);
Environment.SetEnvironmentVariable("DISPLAY",":MorseCoreInstanceSmoke");
var results=new List<string>();
var registration=await LinuxInstanceGate.Acquire();
Check(registration.Continue && registration.Gate is not null,"primary owns lock");
var primary=registration.Gate!;
try
{
    using(var secondary=Start("secondary")) await Successful(secondary,"second process redirects");
    await WaitFor(()=>App.Activations>=1,"activation reaches primary");
    string isolated=Path.Combine(root,"isolated");
    using(var separate=Start("isolated",isolated)) await Successful(separate,"different profile owns independent lock");
    var others=Enumerable.Range(0,4).Select(_=>Start("secondary")).ToArray();
    try { foreach(var other in others) await Successful(other,"concurrent secondary redirects"); }
    finally { foreach(var other in others) other.Dispose(); }
    await WaitFor(()=>App.Activations>=5,"all activation requests arrive");
}
finally { primary.Dispose(); }
var reopened=await LinuxInstanceGate.Acquire();
Check(reopened.Continue && reopened.Gate is not null,"normal close releases lock");
reopened.Gate!.Dispose();
using(var crashed=Start("crash-primary"))
{
    try { await WaitFor(()=>File.Exists(Path.Combine(root,"ready")),"child primary ready"); }
    finally { if(!crashed.HasExited) crashed.Kill(); await crashed.WaitForExitAsync(); }
}
var recovered=await LinuxInstanceGate.Acquire();
Check(recovered.Continue && recovered.Gate is not null,"process crash releases lock");
recovered.Gate!.Dispose();
string report=Path.Combine(root,"result.json");
File.WriteAllText(report,JsonSerializer.Serialize(new { Passed=results.Count,Checks=results,Activations=App.Activations },new JsonSerializerOptions { WriteIndented=true }));
File.Copy(report,Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!,"instance-smoke-"+Guid.NewGuid().ToString("N")+".json"));
Console.WriteLine(File.ReadAllText(report));
Console.WriteLine("REPORT="+report);
return 0;

void Check(bool condition,string description)
{
    if(!condition) throw new InvalidOperationException(description);
    results.Add(description);
}
Process Start(string mode,string? home=null)
{
    var start=new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true };
    if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="dotnet") start.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
    start.ArgumentList.Add(mode);
    if(home is not null) start.Environment["MORSECORE_DESKTOP_HOME"]=home;
    return Process.Start(start)!;
}
async Task Successful(Process process,string check)
{
    using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
    try { await process.WaitForExitAsync(timeout.Token); }
    finally { if(!process.HasExited) process.Kill(); }
    string stderr=await process.StandardError.ReadToEndAsync();
    Check(process.ExitCode==0,check+" "+stderr.Trim());
}
async Task WaitFor(Func<bool> condition,string check)
{
    for(int i=0;i<200 && !condition();i++) await Task.Delay(25);
    Check(condition(),check);
}

namespace MorseCore.Desktop
{
    internal static class App
    {
        internal static int Activations;
        internal static void ActivateLinuxInstance()=>Interlocked.Increment(ref Activations);
    }
}
