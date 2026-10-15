namespace MorseCore.Desktop;

public sealed partial class MainPage
{
    // Keep reading order and equal field widths while adapting to the available client area.
    private Grid Columns(double minimumWidth,int maximumColumns,params FrameworkElement[] children)
    {
        var grid=new Grid { ColumnSpacing=16,RowSpacing=14 };
        foreach(var child in children) grid.Children.Add(child);
        int currentColumns=0;
        void Arrange(int count)
        {
            if(count==currentColumns) return;
            currentColumns=count; grid.ColumnDefinitions.Clear(); grid.RowDefinitions.Clear();
            for(int column=0;column<count;column++) grid.ColumnDefinitions.Add(new() { Width=new GridLength(1,GridUnitType.Star) });
            for(int row=0;row<(children.Length+count-1)/count;row++) grid.RowDefinitions.Add(new() { Height=GridLength.Auto });
            for(int i=0;i<children.Length;i++) { Grid.SetRow(children[i],i/count); Grid.SetColumn(children[i],i%count); }
        }
        void Adapt()=>Arrange(Math.Clamp((int)((grid.ActualWidth+grid.ColumnSpacing)/(minimumWidth+grid.ColumnSpacing)),1,Math.Max(1,Math.Min(maximumColumns,children.Length))));
        Arrange(1); grid.Loaded+=(_,_)=>Adapt(); grid.SizeChanged+=(_,_)=>Adapt();
        return grid;
    }
}
