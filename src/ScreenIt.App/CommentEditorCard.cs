using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

// Three distinct layout regions: editorial header, integrated input, keyboard footer.
// The supplied TextBox remains the same editing control with its native text services.
internal sealed class CommentEditorCard : Border
{
    private readonly TextBlock title,brand;
    private readonly TextBox input;
    private readonly TextBlock error;
    private readonly Grid layout,header;
    private readonly Border footer;
    private readonly FrameworkElement divider;
    private readonly System.Windows.Shapes.Path signal,slashes;
    private readonly (Border Cap,TextBlock Label,string Text)[] commands;
    internal TextBox Input => input;
    internal CommentEditorCard(TextBlock title,TextBox input,TextBlock error)
    {
        this.title=title;this.input=input;this.error=error;
        BorderThickness=new Thickness(1);Padding=new Thickness(18,16,18,16);
        layout=new Grid();
        layout.RowDefinitions.Add(new() { Height=new GridLength(36) });
        layout.RowDefinitions.Add(new() { Height=new GridLength(20) });
        layout.RowDefinitions.Add(new());
        layout.RowDefinitions.Add(new() { Height=GridLength.Auto });
        layout.RowDefinitions.Add(new() { Height=GridLength.Auto });
        header=new Grid();header.ColumnDefinitions.Add(new() { Width=GridLength.Auto });header.ColumnDefinitions.Add(new() { Width=GridLength.Auto });header.ColumnDefinitions.Add(new() { Width=GridLength.Auto });header.ColumnDefinitions.Add(new());
        signal=new System.Windows.Shapes.Path { Data=System.Windows.Media.Geometry.Parse("M0,22 L10,0 29,0 19,22 Z"),Width=17,Height=16,Stretch=Stretch.Fill,Margin=new Thickness(0,0,8,0),VerticalAlignment=VerticalAlignment.Center };
        header.Children.Add(signal);
        title.FontFamily=ConstructDesign.Display;title.FontSize=26;title.FontWeight=FontWeights.Bold;title.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(title,1);header.Children.Add(title);
        var separator=new Border { Width=1,Height=24,Margin=new Thickness(12,0,12,0),VerticalAlignment=VerticalAlignment.Center };Grid.SetColumn(separator,2);header.Children.Add(separator);
        var identity=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        brand=new TextBlock { Text="SCREENIT",FontSize=10.5,FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center };
        slashes=new System.Windows.Shapes.Path { Data=System.Windows.Media.Geometry.Parse("M0,12 L6,0 M7,12 L13,0"),Width=10,Height=10,Stretch=Stretch.Fill,StrokeThickness=2,Margin=new Thickness(9,0,0,0),VerticalAlignment=VerticalAlignment.Center };
        identity.Children.Add(brand);identity.Children.Add(slashes);Grid.SetColumn(identity,3);header.Children.Add(identity);layout.Children.Add(header);
        divider=new EditorDivider();Grid.SetRow(divider,1);layout.Children.Add(divider);
        input.Height=double.NaN;input.MinHeight=64;input.Margin=new();input.Padding=new Thickness(12,10,12,10);input.FontSize=14;
        input.BorderThickness=new Thickness(0);input.SelectionOpacity=.35;input.FocusVisualStyle=null;
        input.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" xmlns:chrome="clr-namespace:ScreenIt.Presentation;assembly=ScreenIt.App" TargetType="TextBox">
              <chrome:CommentInputFrame x:Name="InputFrame" Background="{TemplateBinding Background}" BorderBrush="{DynamicResource CommentInputBorder}" BorderThickness="1" >
                <ScrollViewer x:Name="PART_ContentHost" Focusable="False" HorizontalScrollBarVisibility="Disabled" VerticalScrollBarVisibility="{TemplateBinding VerticalScrollBarVisibility}"/>
              </chrome:CommentInputFrame>
              <ControlTemplate.Triggers>
                <Trigger Property="IsKeyboardFocusWithin" Value="True"><Setter TargetName="InputFrame" Property="BorderBrush" Value="{DynamicResource CommentInputFocus}"/><Setter TargetName="InputFrame" Property="BorderThickness" Value="2"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.5"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        input.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollBar">
              <Setter Property="Width" Value="8"/><Setter Property="Margin" Value="0,4,2,8"/><Setter Property="MinWidth" Value="0"/><Setter Property="Background" Value="Transparent"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
                <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" ViewportSize="{TemplateBinding ViewportSize}" Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight="20"><Thumb.Template><ControlTemplate TargetType="Thumb"><Border Background="{DynamicResource CommentScrollThumb}" CornerRadius="2" Margin="2,0"/></ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                </Track>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        input.Loaded+=(_,_)=>ConfigureScrollbar();
        Grid.SetRow(input,2);layout.Children.Add(input);
        error.Margin=new Thickness(0,6,0,0);error.Visibility=Visibility.Collapsed;Grid.SetRow(error,3);layout.Children.Add(error);
        var hints=new WrapPanel { Orientation=Orientation.Horizontal };
        (Border,TextBlock,string) Command(string key,string label)
        {
            var row=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,10,0),VerticalAlignment=VerticalAlignment.Center,Height=24 };
            var cap=new Border { Child=new TextBlock { Text=key,FontSize=10.5,FontWeight=FontWeights.SemiBold },BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(3),Padding=new Thickness(5,2,5,2),VerticalAlignment=VerticalAlignment.Center };
            var caption=new TextBlock { FontSize=12,Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center };
            row.Children.Add(cap);row.Children.Add(caption);hints.Children.Add(row);return (cap,caption,label);
        }
        commands=[Command("Enter","Save comment"),Command("Shift+Enter","New line"),Command("Esc","Cancel")];
        footer=new Border { Child=hints,BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,8,0,0),Margin=new Thickness(0,10,0,0) };Grid.SetRow(footer,4);layout.Children.Add(footer);
        Child=layout;SizeChanged+=(_,_)=>Adapt();ApplyTheme();
    }
    private void ConfigureScrollbar()
    {
        input.ApplyTemplate();
        if(input.Template.FindName("PART_ContentHost",input) is ScrollViewer host)
        {
            host.ApplyTemplate();
            if(host.Template.FindName("PART_VerticalScrollBar",host) is System.Windows.Controls.Primitives.ScrollBar bar)
            {
                bar.Style=(Style)input.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)];bar.MinWidth=0;bar.Width=8;
            }
        }
    }
    private void Adapt()
    {
        bool narrow=ActualWidth<330;
        Padding=new Thickness(narrow ? 16 : 18,16,narrow ? 16 : 18,16);
        layout.RowDefinitions[0].Height=new GridLength(36);
        title.FontSize=narrow ? 22 : 26;brand.FontSize=11;
        header.Children[3].Visibility=ActualWidth<300 ? Visibility.Collapsed : Visibility.Visible;
        header.Children[2].Visibility=ActualWidth<300 ? Visibility.Collapsed : Visibility.Visible;
        input.FontSize=14;
    }
    internal void ApplyTheme()
    {
        Background=SystemParameters.HighContrast ? SystemColors.WindowBrush : Appearance.Current==UiTheme.Dark ? ConstructDesign.Ink : ConstructDesign.Paper;BorderBrush=Appearance.Palette.Border;
        title.Foreground=UtilityUi.Ink;brand.Foreground=UtilityUi.Muted;signal.Fill=UtilityUi.Accent;slashes.Stroke=ConstructDesign.Acid;
        header.Children.OfType<Border>().Single().Background=Appearance.Palette.Border;
        input.Background=SystemParameters.HighContrast ? Background : Appearance.Current==UiTheme.Dark ? ConstructDesign.Ink : ConstructDesign.Paper;input.Foreground=input.CaretBrush=UtilityUi.Ink;input.SelectionBrush=UtilityUi.Accent;
        input.Resources["CommentInputBorder"]=SystemParameters.HighContrast ? SystemColors.WindowTextBrush : Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(154,157,149) : ConstructDesign.Ink;
        input.Resources["CommentInputFocus"]=UtilityUi.Accent;
        input.Resources["CommentScrollThumb"]=UtilityUi.Muted;
        error.Foreground=Appearance.Palette.Warning;footer.BorderBrush=Appearance.Palette.Border;
        foreach(var (cap,label,text) in commands)
        {
            cap.BorderBrush=UtilityUi.Muted;cap.Background=SystemParameters.HighContrast ? Background : CommentChrome.Surface;
            ((TextBlock)cap.Child).Foreground=UtilityUi.Ink;label.Text=L.T(text);
            label.Foreground=SystemParameters.HighContrast ? UtilityUi.Ink : CommentChrome.Tint(Appearance.Current==UiTheme.Dark ? CommentChrome.Paper : ConstructDesign.Ink.Color,205);
        }
        divider.InvalidateVisual();
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(ActualWidth<=0 || ActualHeight<=0) return;
        CommentChrome.Panel(dc,new Size(ActualWidth,ActualHeight),Background,BorderBrush);
    }

    private sealed class EditorDivider : FrameworkElement
    {
        protected override void OnRender(DrawingContext dc)
        {
            double y=ActualHeight-10,bend=Math.Max(0,ActualWidth-76);
            var line=new StreamGeometry();using(var path=line.Open()) { path.BeginFigure(new Point(0,y),false,false);path.LineTo(new Point(bend,y),true,false);path.LineTo(new Point(bend+12,y-12),true,false);path.LineTo(new Point(ActualWidth,y-12),true,false); }
            dc.DrawGeometry(null,new Pen(Appearance.Palette.Border,1),line);
        }
    }
}

// Coordinates are overlay DIPs; this element is never passed to Painter.
internal sealed class CommentEditorLink : FrameworkElement
{
    private Point start,end;
    private Rect obstacle;
    internal static readonly DependencyProperty RevealProperty=DependencyProperty.Register("Reveal",typeof(double),typeof(CommentEditorLink),new FrameworkPropertyMetadata(1d,FrameworkPropertyMetadataOptions.AffectsRender));
    internal void Place(Point from,Point to,Rect badge) { start=from;end=to;obstacle=badge;InvalidateVisual(); }
    internal void Reveal()
    {
        BeginAnimation(RevealProperty,null);SetValue(RevealProperty,1d);
        if(SystemParameters.ClientAreaAnimation && IsVisible)
            BeginAnimation(RevealProperty,new System.Windows.Media.Animation.DoubleAnimation(0,1,TimeSpan.FromMilliseconds(140)) { FillBehavior=System.Windows.Media.Animation.FillBehavior.Stop,EasingFunction=new System.Windows.Media.Animation.QuadraticEase { EasingMode=System.Windows.Media.Animation.EasingMode.EaseOut } });
    }
    internal void Stop() { double current=(double)GetValue(RevealProperty);BeginAnimation(RevealProperty,null);SetValue(RevealProperty,current); }
    protected override void OnRender(DrawingContext dc)
    {
        // Axial stem plus a single 45-degree elbow; no outlined double stroke.
        Vector direction=end-start;bool vertical=Math.Abs(direction.Y)>=Math.Abs(direction.X);
        double diagonal=Math.Min(Math.Abs(direction.X),Math.Abs(direction.Y));
        double sx=Math.Sign(direction.X),sy=Math.Sign(direction.Y);
        Point elbow=vertical ? new Point(start.X,end.Y-sy*diagonal) : new Point(end.X-sx*diagonal,start.Y);
        Point[] points=[start,elbow,end];
        double length=Enumerable.Range(0,points.Length-1).Sum(i=>(points[i+1]-points[i]).Length);
        double remaining=length*(double)GetValue(RevealProperty);
        var geometry=new StreamGeometry();using(var path=geometry.Open())
        {
            path.BeginFigure(start,false,false);
            for(int i=0;i<points.Length-1 && remaining>0;i++)
            {
                Vector delta=points[i+1]-points[i];double segment=delta.Length;
                path.LineTo(segment<=remaining ? points[i+1] : points[i]+delta*(remaining/segment),true,false);remaining-=segment;
            }
        }
        var line=SystemParameters.HighContrast ? UtilityUi.Accent : new SolidColorBrush(Color.FromRgb(244,131,108));
        dc.DrawGeometry(null,new Pen(line,1.2) { LineJoin=PenLineJoin.Miter },geometry);
        // This port belongs to the editor connection, never to exported pixels.
        dc.DrawEllipse(ConstructDesign.Paper,new Pen(UtilityUi.Accent,1.3),start,2.2,2.2);
    }
}
