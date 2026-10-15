#if DEBUG && MORSE_WINUI
using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Windows.Graphics;

namespace MorseCore.Desktop;

internal static partial class WindowChrome
{
    internal sealed record MouseDragResult(bool MovedWhileHeld,bool StoppedOnRelease,bool ClickDidNotLatch,bool SecondMovedWhileHeld,bool SecondStoppedOnRelease,int EnteredLoops,int ExitedLoops);

    internal static PointInt32 ScreenPoint(Window window,double x,double y,double scale)
    {
        var point=new PointInt32 { X=(int)Math.Round(x*scale),Y=(int)Math.Round(y*scale) };
        if(!ClientToScreen(WinRT.Interop.WindowNative.GetWindowHandle(window),ref point)) throw new Win32Exception();
        return point;
    }
    internal static bool IsClientHitTarget(Window window,PointInt32 point)
    {
        var handle=WinRT.Interop.WindowNative.GetWindowHandle(window);
        var target=WindowFromPoint(point);
        if(GetAncestor(target,2)!=handle) return false;
        nint packed=unchecked((nint)((point.Y&0xffff)<<16 | (point.X&0xffff)));
        return SendMessage(target,0x0084,0,packed)==1;
    }

    // Opt-in input test, restricted to this test process's foreground window.
    internal static async Task<MouseDragResult> CheckMouseDrag(Window window,PointInt32 anchor)
    {
        if(Environment.GetEnvironmentVariable("MORSECORE_DESKTOP_MOUSE_SMOKE")!="1") throw new InvalidOperationException("Mouse smoke test was not enabled.");
        var handle=WinRT.Interop.WindowNative.GetWindowHandle(window);
        window.Activate();
        SetForegroundWindow(handle);
        int entered=0,exited=0;
        SubclassProc callback=(hwnd,message,wParam,lParam,id,data)=>
        {
            if(message==0x0231) Interlocked.Increment(ref entered);
            if(message==0x0232) Interlocked.Increment(ref exited);
            return DefSubclassProc(hwnd,message,wParam,lParam);
        };
        if(!SetWindowSubclass(handle,callback,0x4D43,0)) throw new Win32Exception();
        try
        {
            return await Task.Run(()=>
            {
                if((GetAsyncKeyState(1)&0x8000)!=0) throw new InvalidOperationException("Mouse test requires the left button to be released.");
                if(!GetCursorPos(out var originalCursor)) throw new Win32Exception();
                void OwnWindow()
                {
                    if(GetForegroundWindow()!=handle) throw new InvalidOperationException("Mouse test window lost foreground; input stopped.");
                }
                NativeRect Bounds()
                {
                    if(!GetWindowRect(handle,out var bounds)) throw new Win32Exception();
                    return bounds;
                }
                void Mouse(uint flags,int x=0,int y=0)
                {
                    var input=new MouseInputPacket { Mouse=new MouseInput { X=x,Y=y,Flags=flags } };
                    if(SendInput(1,[input],Marshal.SizeOf<MouseInputPacket>())!=1) throw new Win32Exception();
                }
                void Move(int x,int y)
                {
                    OwnWindow();
                    int width=GetSystemMetrics(78),height=GetSystemMetrics(79);
                    Mouse(0xC001,(int)Math.Round((x-GetSystemMetrics(76))*65535.0/(width-1)),(int)Math.Round((y-GetSystemMetrics(77))*65535.0/(height-1)));
                }
                void AtCaption(int x,int y)
                {
                    Move(x,y); Thread.Sleep(60); OwnWindow();
                    var point=new PointInt32 { X=x,Y=y };
                    var target=WindowFromPoint(point);
                    if(GetAncestor(target,2)!=handle) throw new InvalidOperationException("Mouse test point is outside its own window.");
                    nint packed=unchecked((nint)((y&0xffff)<<16 | (x&0xffff)));
                    if(SendMessage(target,0x0084,0,packed)!=2) throw new InvalidOperationException("Title bar is not a native caption hit target.");
                }
                (bool Moved,bool Stopped) Drag(int x,int y,int dx,int dy)
                {
                    AtCaption(x,y);
                    var before=Bounds();
                    Mouse(2); Thread.Sleep(60);
                    for(int i=1;i<=10;i++) { Move(x+dx*i/10,y+dy*i/10); Thread.Sleep(15); }
                    var held=Bounds();
                    Mouse(4); Thread.Sleep(150);
                    var released=Bounds();
                    Move(x+dx+20,y+dy+4); Thread.Sleep(120);
                    return (held.Left!=before.Left || held.Top!=before.Top,released.Left==Bounds().Left && released.Top==Bounds().Top);
                }
                try
                {
                    OwnWindow();
                    var initial=Bounds();
                    var first=Drag(anchor.X,anchor.Y,40,6);
                    var afterFirst=Bounds();
                    int nextX=anchor.X+afterFirst.Left-initial.Left,nextY=anchor.Y+afterFirst.Top-initial.Top;
                    AtCaption(nextX,nextY);
                    Mouse(2); Thread.Sleep(30); Mouse(4); Thread.Sleep(100);
                    var clicked=Bounds();
                    Move(nextX+16,nextY+3); Thread.Sleep(120);
                    var afterClick=Bounds();
                    bool clickStayed=clicked.Left==afterClick.Left && clicked.Top==afterClick.Top;
                    // A different press point avoids treating this separate drag as a caption double-click.
                    var second=Drag(nextX+60,nextY,-32,-4);
                    return new MouseDragResult(first.Moved,first.Stopped,clickStayed,second.Moved,second.Stopped,Volatile.Read(ref entered),Volatile.Read(ref exited));
                }
                finally
                {
                    if(GetForegroundWindow()==handle)
                    {
                        Mouse(4);
                        PostMessage(handle,0x0100,0x1B,0);
                        Move(originalCursor.X,originalCursor.Y);
                    }
                }
            });
        }
        finally { RemoveWindowSubclass(handle,callback,0x4D43); GC.KeepAlive(callback); }
    }

    [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInput { public int X,Y; public uint Data,Flags,Time; public nuint Extra; }
    [StructLayout(LayoutKind.Sequential)] private struct MouseInputPacket { public uint Type; public MouseInput Mouse; }
    private delegate nint SubclassProc(nint window,uint message,nuint wParam,nint lParam,nuint id,nuint data);
    [DllImport("comctl32.dll")] private static extern bool SetWindowSubclass(nint window,SubclassProc callback,nuint id,nuint data);
    [DllImport("comctl32.dll")] private static extern bool RemoveWindowSubclass(nint window,SubclassProc callback,nuint id);
    [DllImport("comctl32.dll")] private static extern nint DefSubclassProc(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll",SetLastError=true)] private static extern uint SendInput(uint count,MouseInputPacket[] input,int size);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(nint window,out NativeRect bounds);
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out PointInt32 point);
    [DllImport("user32.dll")] private static extern bool ClientToScreen(nint window,ref PointInt32 point);
    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    [DllImport("user32.dll")] private static extern nint WindowFromPoint(PointInt32 point);
    [DllImport("user32.dll")] private static extern nint GetAncestor(nint window,uint flags);
    [DllImport("user32.dll",EntryPoint="SendMessageW")] private static extern nint SendMessage(nint window,uint message,nuint wParam,nint lParam);
    [DllImport("user32.dll",EntryPoint="PostMessageW")] private static extern bool PostMessage(nint window,uint message,nuint wParam,nint lParam);
}
#endif
