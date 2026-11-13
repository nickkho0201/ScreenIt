using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Path = System.Windows.Shapes.Path;

// One mode toggle plus passive keyboard guidance. Only the toggle consumes input;
// the rest of the guide passes through to the existing selection surface.
internal sealed class CaptureSelectionBar : Border
{
    private readonly WrapPanel groups=new() { Orientation=Orientation.Horizontal };
    private readonly TextBlock brandTitle=new() { Text="ScreenIt",FontWeight=FontWeights.Bold,VerticalAlignment=VerticalAlignment.Center };
    private readonly Border monitor,modifier,cancel;
    private readonly TextBlock monitorLabel,modifierLabel,cancelLabel;
    private readonly Path monitorIcon;
    private readonly TextBlock modifierKey;
    private readonly TextBlock modeName=new() { FontSize=13,FontWeight=FontWeights.SemiBold };
    private readonly Path modeIcon=ConstructDesign.Icon("Region",ConstructDesign.Ink);
    private readonly Path switchIcon=ConstructDesign.Icon("Switch",ConstructDesign.Ink);
    private readonly Border modeKey=Keycap("W");
    private bool? captionWindowMode;
    internal Button ModeButton { get; }
    internal bool WindowMode { get; private set; }
    internal bool ModifierActive { get; private set; }
    internal FrameworkElement[] Sections { get; }

    internal CaptureSelectionBar(TextBlock modifierKey,Action toggleMode)
    {
        this.modifierKey=modifierKey;
        Focusable=false;
        System.Windows.Documents.TextElement.SetFontFamily(this,ConstructDesign.Body);
        System.Windows.Documents.TextElement.SetFontSize(this,13);
        Padding=new Thickness(5);CornerRadius=new CornerRadius(4);BorderThickness=new Thickness(1);
        Effect=new System.Windows.Media.Effects.DropShadowEffect { BlurRadius=6,ShadowDepth=1,Opacity=.12 };
        var brand=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(8,2,16,2),Height=34 };
        brand.Children.Add(new Image { Source=UtilityUi.WindowIcon(),Width=28,Height=28,Margin=new Thickness(0,0,8,0) });brand.Children.Add(brandTitle);
        ModeButton=UtilityUi.Button("Region","Switch capture mode (W)",toggleMode);
        ModeButton.MinWidth=176;ModeButton.MinHeight=34;
        var modeCaption=new Grid { Width=150,VerticalAlignment=VerticalAlignment.Center };
        modeCaption.ColumnDefinitions.Add(new() { Width=new GridLength(28) });modeCaption.ColumnDefinitions.Add(new());
        modeCaption.ColumnDefinitions.Add(new() { Width=new GridLength(44) });modeCaption.ColumnDefinitions.Add(new() { Width=new GridLength(16) });
        modeIcon.Margin=new Thickness(0,0,10,0);modeIcon.VerticalAlignment=VerticalAlignment.Center;modeCaption.Children.Add(modeIcon);
        modeName.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(modeName,1);modeCaption.Children.Add(modeName);
        modeKey.Margin=new Thickness(12,0,12,0);Grid.SetColumn(modeKey,2);modeCaption.Children.Add(modeKey);
        switchIcon.Width=switchIcon.Height=16;switchIcon.VerticalAlignment=VerticalAlignment.Center;Grid.SetColumn(switchIcon,3);modeCaption.Children.Add(switchIcon);
        ModeButton.Content=modeCaption;
        monitor=Mode("Full monitor",out monitorLabel,out monitorIcon,"Space");
        var modifierRow=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        modifierRow.Children.Add(new Border { Child=modifierKey,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),Padding=new Thickness(4,1,4,1),Margin=new Thickness(0,0,8,0) });
        modifierRow.Children.Add(modifierLabel=new TextBlock { VerticalAlignment=VerticalAlignment.Center });
        modifier=new Border { Child=modifierRow,Padding=new Thickness(10,6,10,6),Margin=new Thickness(2),MinHeight=34,CornerRadius=new CornerRadius(4),BorderThickness=new Thickness(1) };
        var cancelRow=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        cancelRow.Children.Add(Keycap("Esc"));cancelRow.Children.Add(cancelLabel=new TextBlock { Margin=new Thickness(6,0,0,0),VerticalAlignment=VerticalAlignment.Center });
        cancel=new Border { Child=cancelRow,Padding=new Thickness(8,6,8,6),Margin=new Thickness(2),MinHeight=34 };
        Sections=[brand,ModeButton,monitor,modifier,cancel];
        foreach(var group in Sections.Where(g=>g!=ModeButton)) group.IsHitTestVisible=false;
        foreach(var group in Sections) groups.Children.Add(group);
        Child=groups;
    }
    // Keep the painted container itself transparent to hit testing, while allowing
    // WPF to hit the real Button child. Empty space and passive hints never start UI gestures.
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters) => null;
    private static Border Keycap(string key) => new()
    {
        Child=new TextBlock { Text=key,FontSize=10,FontWeight=FontWeights.SemiBold },
        BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(2),Padding=new Thickness(4,1,4,1),
        Margin=new Thickness(4,0,4,0),VerticalAlignment=VerticalAlignment.Center
    };
    private static Border Mode(string name,out TextBlock label,out Path icon,string? key=null)
    {
        var row=new StackPanel { Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center };
        icon=ConstructDesign.Icon(name,UtilityUi.Ink);icon.Margin=new Thickness(0,0,8,0);row.Children.Add(icon);
        row.Children.Add(label=new TextBlock { FontWeight=FontWeights.SemiBold,VerticalAlignment=VerticalAlignment.Center });
        if(key!=null) row.Children.Add(Keycap(key));
        return new Border { Child=row,Padding=new Thickness(10,6,10,6),MinHeight=34,Margin=new Thickness(2),CornerRadius=new CornerRadius(4) };
    }
    internal void SetAvailableWidth(double width)
    {
        MaxWidth=width;brandTitle.Visibility=width>=1050 ? Visibility.Visible : Visibility.Collapsed;
    }
    internal void Refresh(bool windowMode,AnnotationModifier key,bool held)
    {
        WindowMode=windowMode;ModifierActive=held;
        Background=Appearance.Palette.Surface;BorderBrush=Appearance.Palette.Border;
        brandTitle.Foreground=UtilityUi.Ink;
        ModeButton.Background=UtilityUi.Accent;ModeButton.Foreground=ConstructDesign.OnAccent;
        ModeButton.Resources["UiFocus"]=ConstructDesign.Focus;
        modeName.Text=L.T(windowMode ? "Window" : "Region");
        modeName.Foreground=modeIcon.Stroke=switchIcon.Stroke=ConstructDesign.OnAccent;
        if(captionWindowMode!=windowMode) { modeIcon.Data=ConstructDesign.Icon(windowMode ? "Window" : "Region",ConstructDesign.OnAccent).Data;captionWindowMode=windowMode; }
        if(ModeButton.ToolTip is ToolTip tip) { tip.Content=L.T("Switch capture mode (W)");tip.Background=Appearance.Palette.Surface;tip.Foreground=UtilityUi.Ink;tip.BorderBrush=Appearance.Palette.Border; }
        System.Windows.Automation.AutomationProperties.SetName(ModeButton,L.T(windowMode ? "Window" : "Region")+" (W)");
        System.Windows.Automation.AutomationProperties.SetHelpText(ModeButton,L.T("Switch capture mode (W)"));
        monitorLabel.Text=L.T("Full monitor");
        modifierLabel.Text=L.T("Hold to annotate");cancelLabel.Text=L.T("Cancel");modifierKey.Text=key.ToString();
        monitor.Background=Brushes.Transparent;monitorLabel.Foreground=monitorIcon.Stroke=UtilityUi.Ink;
        foreach(var cap in DescendantCaps(groups))
        {
            cap.BorderBrush=Appearance.Palette.Border;
            if(cap.Child is TextBlock text) text.Foreground=UtilityUi.Muted;
        }
        // The toggle's keycap uses the primary action contrast pairing.
        modeKey.BorderBrush=ConstructDesign.OnAccent;((TextBlock)modeKey.Child).Foreground=ConstructDesign.OnAccent;
        modifier.Background=held ? ConstructDesign.Acid : Brushes.Transparent;
        modifier.BorderBrush=held ? ConstructDesign.Focus : Brushes.Transparent;
        modifierLabel.Foreground=held ? ConstructDesign.Ink : UtilityUi.Muted;
        modifierKey.Foreground=held ? ConstructDesign.Ink : UtilityUi.Ink;
        ((Border)modifierKey.Parent).BorderBrush=held ? ConstructDesign.Ink : Appearance.Palette.Border;
        cancelLabel.Foreground=UtilityUi.Muted;
    }
    private static System.Collections.Generic.IEnumerable<Border> DescendantCaps(DependencyObject root)
    {
        if(root is Border { Child:TextBlock } cap) yield return cap;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
            foreach(var item in DescendantCaps(child)) yield return item;
    }
}
