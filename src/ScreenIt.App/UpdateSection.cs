using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

internal static class UpdateText
{
    private static CultureInfo Culture => CultureInfo.GetCultureInfo(L.Language=="ru" ? "ru-RU" : "en-US");
    internal static string Bytes(long bytes) => string.Format(Culture,L.T("{0} MB"),(bytes/1_000_000d).ToString("0.0",Culture));
    internal static string Detail(UpdateState state) => state.BytesDownloaded is not { } bytes ? "" : state.TotalBytes is { } total ? string.Format(L.T("{0} of {1}"),Bytes(bytes),Bytes(total)) : string.Format(L.T("{0} downloaded"),Bytes(bytes));
    internal static string Error(UpdateError? error) => L.T(error switch
    {
        UpdateError.Check=>"Could not check for updates. Check your connection and retry.",
        UpdateError.Download=>"Could not download the update. Check your connection and retry.",
        UpdateError.Verification=>"Could not verify the update. The downloaded file is damaged or does not match the expected release.",
        UpdateError.Launch=>"Could not start the installer. Your session is unchanged. Retry the update.",
        _=>"Could not prepare the update for installation. Nothing was installed. Retry the update."
    });
    internal static string Status(UpdateState state) => state.Phase switch
    {
        UpdatePhase.Idle=>"",
        UpdatePhase.Checking=>L.T("Checking for updates…"),
        UpdatePhase.UpToDate=>L.T("You are up to date."),
        UpdatePhase.Available=>string.Format(L.T("Version {0} is available."),state.AvailableVersion),
        UpdatePhase.Downloading=>state.Progress is { } value ? string.Format(L.T("Downloading update — {0}%"),Math.Floor(value*100).ToString("0",Culture)) : L.T("Downloading update…"),
        UpdatePhase.Verifying=>L.T("Verifying update…"),
        UpdatePhase.PreparingInstall=>L.T("Preparing installation…"),
        UpdatePhase.LaunchingInstaller=>L.T("Launching installer…"),
        UpdatePhase.InstallerLaunched=>L.T("Installer launched. ScreenIt is handing off the update."),
        _=>Error(state.Error)
    };
}

// Shared production/manual-harness renderer. The flow, not the controls, owns state.
internal sealed class UpdateSection : StackPanel
{
    private readonly IUpdateFlow flow;
    private readonly Func<string,Action,Button> button;
    private readonly TextBlock status=new() { FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,10) };
    private readonly TextBlock bytes=new() { FontSize=12,Margin=new Thickness(0,8,0,12) };
    private readonly UpdateProgress bar=new() { Height=6,Margin=new Thickness(0,0,0,0) };
    private readonly StackPanel actions=new();
    private readonly TextBlock portable=new() { FontSize=14,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) };
    internal UpdateSection(IUpdateFlow flow,Func<string,Action,Button> button)
    {
        this.flow=flow;this.button=button;
        Children.Add(status);Children.Add(bar);Children.Add(bytes);Children.Add(actions);Children.Add(portable);
        Loaded+=(_,_)=> { flow.Changed+=Refresh;Refresh(); };
        Unloaded+=(_,_)=> { flow.Changed-=Refresh;bar.Stop(); };
        Refresh();
    }
    internal void Refresh()
    {
        var state=flow.State;status.Text=UpdateText.Status(state);status.Foreground=state.Phase==UpdatePhase.Failed ? Appearance.Palette.Warning : UtilityUi.Ink;
        status.Visibility=status.Text.Length==0 ? Visibility.Collapsed : Visibility.Visible;
        bytes.Text=UpdateText.Detail(state);bytes.Foreground=UtilityUi.Muted;bytes.Visibility=state.Phase==UpdatePhase.Downloading ? Visibility.Visible : Visibility.Collapsed;
        bar.Visibility=state.Active ? Visibility.Visible : Visibility.Collapsed;bar.Set(state.Progress,state.Active);
        actions.Children.Clear();
        if(!state.Active && state.Phase!=UpdatePhase.InstallerLaunched)
        {
            if(state.Phase==UpdatePhase.Failed) actions.Children.Add(button("Retry",async()=> { if(!flow.CanInstall && state.Error!=UpdateError.Check) flow.OpenPage();else await flow.Retry(); }));
            else
            {
                actions.Children.Add(button("Check for updates",async()=>await flow.Check()));
                if(state.Release!=null) actions.Children.Add(button(flow.CanInstall ? "Download and install" : "Open release page",async()=> { if(flow.CanInstall) await flow.Download();else flow.OpenPage(); }));
            }
        }
        portable.Text=L.T("Portable copy: install updates manually from the release page.");portable.Foreground=UtilityUi.Muted;
        portable.Visibility=!flow.CanInstall && state.Release!=null ? Visibility.Visible : Visibility.Collapsed;
    }
}

// No native/WPF progress chrome. Unknown work uses an accent segment; Windows
// animation policy replaces its movement with a static striped accent track.
internal sealed class UpdateProgress : FrameworkElement
{
    private static readonly DependencyProperty PositionProperty=DependencyProperty.Register("Position",typeof(double),typeof(UpdateProgress),new FrameworkPropertyMetadata(0d,FrameworkPropertyMetadataOptions.AffectsRender));
    private double? progress;
    private bool moving;
    internal bool AnimationActive => moving;
    internal void Set(double? value,bool active,bool? animations=null)
    {
        progress=value;bool animate=active && value==null && (animations ?? SystemParameters.ClientAreaAnimation);
        if(animate!=moving)
        {
            moving=animate;BeginAnimation(PositionProperty,null);
            if(animate) BeginAnimation(PositionProperty,new DoubleAnimation(0,1,TimeSpan.FromSeconds(1.4)) { AutoReverse=true,RepeatBehavior=RepeatBehavior.Forever,EasingFunction=new SineEase { EasingMode=EasingMode.EaseInOut } });
        }
        InvalidateVisual();
    }
    internal void Stop() { moving=false;BeginAnimation(PositionProperty,null); }
    protected override void OnRender(DrawingContext dc)
    {
        var rect=new Rect(0,0,ActualWidth,ActualHeight);if(rect.IsEmpty || ActualWidth<=0) return;
        dc.DrawRoundedRectangle(Appearance.Palette.Border,null,rect,3,3);dc.PushClip(new RectangleGeometry(rect,3,3));
        if(progress is { } value) dc.DrawRectangle(UtilityUi.Accent,null,new Rect(0,0,ActualWidth*Math.Clamp(value,0,1),ActualHeight));
        else if(moving) dc.DrawRectangle(UtilityUi.Accent,null,new Rect((ActualWidth*.7)*(double)GetValue(PositionProperty),0,ActualWidth*.3,ActualHeight));
        else for(double x=0;x<ActualWidth;x+=14) dc.DrawRectangle(UtilityUi.Accent,null,new Rect(x,0,7,ActualHeight));
        dc.Pop();
    }
}
