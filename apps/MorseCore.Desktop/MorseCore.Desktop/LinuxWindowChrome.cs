#if !MORSE_WINUI
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.Graphics;

namespace MorseCore.Desktop;

// The X11 host does not implement WinUI non-client caption regions. Keep
// this adapter limited to window input; the shared XAML owns the appearance.
internal sealed class LinuxWindowChrome : IDisposable
{
    private readonly Window window;
    private readonly FrameworkElement region;
    private readonly DispatcherTimer timer=new() { Interval=TimeSpan.FromMilliseconds(16) };
    private nint display;
    private int pointerX,pointerY;
    private PointInt32 origin;

    private LinuxWindowChrome(Window window,FrameworkElement region)
    {
        this.window=window; this.region=region;
        region.PointerPressed+=BeginMove;
        region.PointerReleased+=EndMove;
        region.PointerCanceled+=EndMove;
        region.PointerCaptureLost+=EndMove;
        region.Loaded+=Loaded;
        region.Unloaded+=Unloaded;
        window.Activated+=Activated;
        window.Closed+=Closed;
        timer.Tick+=Move;
    }

    internal static void Attach(Window window,FrameworkElement region)
    {
        if(OperatingSystem.IsLinux()) _=new LinuxWindowChrome(window,region);
    }

    internal static void RestoreActivate(Window window)
    {
        if(!OperatingSystem.IsLinux() ||
            Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow nativeWindow ||
            nativeWindow.WindowId==0) return;
        nint connection=Native.XOpenDisplay(0);
        if(connection==0) return;
        try
        {
            nint root=Native.XDefaultRootWindow(connection);
            nint atom=Native.XInternAtom(connection,"_NET_ACTIVE_WINDOW",0);
            if(atom==0) return;
            // Mapping is the ICCCM request to leave IconicState. HIDDEN is
            // owned by the window manager and must not be deleted directly.
            Native.XMapRaised(connection,nativeWindow.WindowId);
            var message=new ClientMessage
            {
                Type=33,Serial=0,SendEvent=1,Display=connection,Window=nativeWindow.WindowId,
                MessageType=atom,Format=32,
                // This request comes only from an explicit second launch via
                // the user-only IPC channel, so use the launcher source.
                Data0=2,Data1=0,Data2=0,Data3=0,Data4=0
            };
            // XEvent is a union of 24 native longs. The client-message fields
            // use native-long slots even though the wire format is 32 bits.
            nint eventData=Marshal.AllocHGlobal(24*IntPtr.Size);
            try
            {
                for(int i=0;i<24;i++) Marshal.WriteIntPtr(eventData,i*IntPtr.Size,0);
                Marshal.StructureToPtr(message,eventData,false);
                Native.XSendEvent(connection,root,0,(nint)((1<<20)|(1<<19)),eventData);
            }
            finally { Marshal.FreeHGlobal(eventData); }
            Native.XFlush(connection);
        }
        finally { Native.XCloseDisplay(connection); }
    }

    internal static void RemoveDecorations(Window window)
    {
        if(!OperatingSystem.IsLinux() ||
            Uno.UI.Xaml.WindowHelper.GetNativeWindow(window) is not Uno.UI.NativeElementHosting.X11NativeWindow nativeWindow ||
            nativeWindow.WindowId==0) return;
        nint connection=Native.XOpenDisplay(0);
        if(connection==0) return;
        try
        {
            // Use the host's actual XID, never the logical AppWindow.Id.
            nint atom=Native.XInternAtom(connection,"_MOTIF_WM_HINTS",0);
            if(atom==0) return;
            var hints=new nint[5];
            int result=Native.XGetWindowProperty(connection,nativeWindow.WindowId,atom,0,5,0,atom,out nint type,out int format,out nuint count,out _,out nint data);
            try
            {
                if(result==0 && type==atom && format==32 && data!=0)
                    for(int i=0;i<hints.Length && (nuint)i<count;i++)
                        hints[i]=Marshal.ReadIntPtr(data,i*IntPtr.Size);
            }
            finally { if(data!=0) Native.XFree(data); }
            // Only change the decoration hint. Keep the presenter's move,
            // minimize and close permissions, along with any other hints.
            hints[0]=(nint)((nuint)hints[0]|2u); // MWM_HINTS_DECORATIONS
            hints[2]=0;
            // Xlib format 32 consumes native longs, including 8-byte LP64
            // slots. A packed uint[5] would be the wrong ABI on Linux x64.
            Native.XChangeProperty(connection,nativeWindow.WindowId,atom,atom,32,0,hints,hints.Length);
            Native.XFlush(connection);
        }
        finally { Native.XCloseDisplay(connection); }
    }

    internal static bool TryGetWorkArea(out RectInt32 area)
    {
        area=default;
        if(!OperatingSystem.IsLinux()) return false;
        nint connection=Native.XOpenDisplay(0);
        if(connection==0) return false;
        try
        {
            nint root=Native.XDefaultRootWindow(connection);
            uint desktop=0;
            if(TryReadCardinals(connection,root,"_NET_CURRENT_DESKTOP",out var desktops) && desktops.Length>0)
                desktop=desktops[0];
            if(TryReadCardinals(connection,root,"_NET_WORKAREA",out var values) && desktop<values.Length/4)
            {
                int index=checked((int)desktop*4);
                if(values[index+2]>0 && values[index+2]<=int.MaxValue && values[index+3]>0 && values[index+3]<=int.MaxValue)
                {
                    area=new RectInt32 { X=unchecked((int)values[index]),Y=unchecked((int)values[index+1]),Width=(int)values[index+2],Height=(int)values[index+3] };
                    return true;
                }
            }
            int screen=Native.XDefaultScreen(connection);
            int width=Native.XDisplayWidth(connection,screen),height=Native.XDisplayHeight(connection,screen);
            if(width<=0 || height<=0) return false;
            area=new RectInt32 { Width=width,Height=height };
            return true;
        }
        finally { Native.XCloseDisplay(connection); }
    }

    private void BeginMove(object sender,PointerRoutedEventArgs e)
    {
        if(!e.GetCurrentPoint(region).Properties.IsLeftButtonPressed) return;
        StopMove();
        display=Native.XOpenDisplay(0);
        if(display==0) return;
        if(!ReadPointer(out pointerX,out pointerY)) { StopMove(); return; }
        origin=window.AppWindow.Position;
        timer.Start();
        e.Handled=true;
    }

    private void Move(object? sender,object e)
    {
        // Query the physical button on every tick. A release outside the XAML
        // window must stop moving even if no PointerReleased event is delivered.
        if(!ReadPointer(out int x,out int y)) { StopMove(); return; }
        var target=new PointInt32 { X=origin.X+x-pointerX,Y=origin.Y+y-pointerY };
        var position=window.AppWindow.Position;
        if(position.X!=target.X || position.Y!=target.Y) window.AppWindow.Move(target);
    }

    private bool ReadPointer(out int x,out int y)
    {
        x=y=0;
        return display!=0 && Native.XQueryPointer(display,Native.XDefaultRootWindow(display),out _,out _,out x,out y,out _,out _,out uint mask)!=0 && (mask&256)!=0;
    }

    private void EndMove(object sender,PointerRoutedEventArgs e)=>StopMove();
    private void Loaded(object sender,RoutedEventArgs e)=>RemoveDecorations(window);
    private void Unloaded(object sender,RoutedEventArgs e)=>StopMove();
    private void Activated(object sender,WindowActivatedEventArgs e)
    {
        if(e.WindowActivationState!=Windows.UI.Core.CoreWindowActivationState.Deactivated)
            RemoveDecorations(window);
    }
    private void Closed(object sender,WindowEventArgs e)=>Dispose();
    private void StopMove()
    {
        timer.Stop();
        if(display!=0) { Native.XCloseDisplay(display); display=0; }
    }

    public void Dispose()
    {
        StopMove();
        timer.Tick-=Move;
        region.PointerPressed-=BeginMove;
        region.PointerReleased-=EndMove;
        region.PointerCanceled-=EndMove;
        region.PointerCaptureLost-=EndMove;
        region.Loaded-=Loaded;
        region.Unloaded-=Unloaded;
        window.Activated-=Activated;
        window.Closed-=Closed;
    }

    private static bool TryReadCardinals(nint connection,nint root,string name,out uint[] values)
    {
        values=[];
        nint atom=Native.XInternAtom(connection,name,1);
        if(atom==0) return false;
        int result=Native.XGetWindowProperty(connection,root,atom,0,1024,0,6,out nint type,out int format,out nuint count,out _,out nint data);
        try
        {
            if(result!=0 || type!=6 || format!=32 || count==0 || count>1024 || data==0) return false;
            values=new uint[(int)count];
            // Xlib returns format-32 properties as native longs, including LP64.
            for(int i=0;i<values.Length;i++) values[i]=unchecked((uint)Marshal.ReadIntPtr(data,i*IntPtr.Size));
            return true;
        }
        finally { if(data!=0) Native.XFree(data); }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ClientMessage
    {
        internal int Type;
        internal nuint Serial;
        internal int SendEvent;
        internal nint Display,Window,MessageType;
        internal int Format;
        internal nint Data0,Data1,Data2,Data3,Data4;
    }

    private static class Native
    {
        private const string X11="libX11.so.6";
        [DllImport(X11)] internal static extern nint XOpenDisplay(nint name);
        [DllImport(X11)] internal static extern int XCloseDisplay(nint display);
        [DllImport(X11)] internal static extern nint XDefaultRootWindow(nint display);
        [DllImport(X11)] internal static extern int XDefaultScreen(nint display);
        [DllImport(X11)] internal static extern int XDisplayWidth(nint display,int screen);
        [DllImport(X11)] internal static extern int XDisplayHeight(nint display,int screen);
        [DllImport(X11)] internal static extern int XQueryPointer(nint display,nint window,out nint root,out nint child,out int rootX,out int rootY,out int windowX,out int windowY,out uint mask);
        [DllImport(X11,CharSet=CharSet.Ansi)] internal static extern nint XInternAtom(nint display,string name,int onlyIfExists);
        [DllImport(X11)] internal static extern int XGetWindowProperty(nint display,nint window,nint property,nint offset,nint length,int delete,nint requestedType,out nint actualType,out int actualFormat,out nuint itemCount,out nuint bytesAfter,out nint data);
        [DllImport(X11)] internal static extern int XChangeProperty(nint display,nint window,nint property,nint type,int format,int mode,[In] nint[] data,int count);
        [DllImport(X11)] internal static extern int XMapRaised(nint display,nint window);
        [DllImport(X11)] internal static extern int XSendEvent(nint display,nint window,int propagate,nint eventMask,nint eventData);
        [DllImport(X11)] internal static extern int XFlush(nint display);
        [DllImport(X11)] internal static extern int XFree(nint data);
    }
}
#endif
