using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Windows.Foundation;

namespace MorseCore.Desktop;

public sealed partial class RawBackdrop : UserControl
{
    private Storyboard? drift;
    private bool motionEnabled,windowActive=true,attached;
    internal bool IsAnimating=>drift!=null;

    public RawBackdrop()
    {
        InitializeComponent();
        Loaded+=(_,_)=> { attached=true; UpdateTone(); RefreshMotion(); };
        Unloaded+=(_,_)=> { attached=false; StopMotion(); };
        ActualThemeChanged+=(_,_)=>UpdateTone();
    }
    private void UpdateTone()=>Opacity=ActualTheme==ElementTheme.Dark ? 0.38 : 0.85;
    internal void SetMotionEnabled(bool enabled) { motionEnabled=enabled; RefreshMotion(); }
    internal void SetActive(bool active) { windowActive=active; RefreshMotion(); }
    private Viewbox[] Corners=>[TopLeft,TopRight,BottomLeft,BottomRight];
    private void ResizeBackdrop(object sender,SizeChangedEventArgs e)
    {
        double w=e.NewSize.Width,h=e.NewSize.Height;
        if(w<=0 || h<=0) return;
        BackdropRoot.Clip=new RectangleGeometry { Rect=new Rect(0,0,w,h) };
        double Width(double fraction,double heightFraction,double minimum,double maximum)=>Math.Max(1,Math.Min(h*heightFraction,Math.Clamp(w*fraction,minimum,maximum)));
        TopLeft.Width=Width(.33,.42,250,720); TopRight.Width=Width(.27,.28,180,520);
        BottomLeft.Width=Width(.24,.36,190,520); BottomRight.Width=Width(.30,.38,220,620);
    }
    private void RefreshMotion()
    {
        if(!motionEnabled || !windowActive || !attached) { StopMotion(); return; }
        if(drift!=null) return;
        var storyboard=new Storyboard();
        (double X,double Y,double Seconds)[] offsets=[(7,-5,16),(-6,6,19),(6,5,21),(-7,-6,17)];
        var corners=Corners;
        for(int i=0;i<corners.Length;i++)
        {
            var transform=(TranslateTransform)corners[i].RenderTransform;
            foreach(var axis in new[]{("X",offsets[i].X),("Y",offsets[i].Y)})
            {
                var animation=new DoubleAnimationUsingKeyFrames { Duration=new Duration(TimeSpan.FromSeconds(offsets[i].Seconds)),RepeatBehavior=RepeatBehavior.Forever };
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime=KeyTime.FromTimeSpan(TimeSpan.Zero),Value=0 });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime=KeyTime.FromTimeSpan(TimeSpan.FromSeconds(offsets[i].Seconds/2)),Value=axis.Item2,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
                animation.KeyFrames.Add(new EasingDoubleKeyFrame { KeyTime=KeyTime.FromTimeSpan(TimeSpan.FromSeconds(offsets[i].Seconds)),Value=0,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
                Storyboard.SetTarget(animation,transform); Storyboard.SetTargetProperty(animation,axis.Item1); storyboard.Children.Add(animation);
            }
        }
        drift=storyboard; storyboard.Begin();
    }
    private void StopMotion()
    {
        drift?.Stop(); drift=null;
        foreach(var corner in Corners) { var transform=(TranslateTransform)corner.RenderTransform; transform.X=transform.Y=0; }
    }
}
