using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

// Dedicated verification executable only. No Coordinator, settings, network,
// update storage, installer process, single-instance mutex or runtime hotkeys.
internal sealed class UpdateUiHarness : IUpdateFlow
{
    internal static readonly UpdateRelease Release=new(new Version(0,1,3),new Uri("https://example.invalid/release"),new Uri("https://example.invalid/installer"),new Uri("https://example.invalid/checksum"));
    internal static readonly UpdateState[] Scenarios=
    [
        new(UpdatePhase.Available,Release),
        new(UpdatePhase.Downloading,Release,0,50_400_000),
        new(UpdatePhase.Downloading,Release,12_600_000,50_400_000),
        new(UpdatePhase.Downloading,Release,25_200_000,50_400_000),
        new(UpdatePhase.Downloading,Release,37_800_000,50_400_000),
        new(UpdatePhase.Downloading,Release,50_400_000,50_400_000),
        new(UpdatePhase.Downloading,Release,21_200_000),
        new(UpdatePhase.Verifying,Release),
        new(UpdatePhase.PreparingInstall,Release),
        new(UpdatePhase.LaunchingInstaller,Release),
        new(UpdatePhase.Failed,Release,Error:UpdateError.Download),
        new(UpdatePhase.Failed,Release,Error:UpdateError.Verification),
        new(UpdatePhase.Failed,Release,Error:UpdateError.Launch),
        new(UpdatePhase.InstallerLaunched,Release)
    ];
    private int index;
    private bool retrying;
    public UpdateState State { get; private set; }=Scenarios[0];
    public bool CanInstall => true;
    public event Action? Changed;
    internal void Show(int step) { index=Math.Clamp(step,0,Scenarios.Length-1);State=Scenarios[index];Changed?.Invoke(); }
    public Task Check() { Show(0);return Task.CompletedTask; }
    public Task Download() { Show(1);return Task.CompletedTask; }
    public Task Retry() { retrying=true;Show(1);return Task.CompletedTask; }
    internal void Move(int offset) { if(offset<0) retrying=false;Show(retrying && offset>0 && index==9 ? 13 : index+offset); }
    public void OpenPage() { }
    internal static Window Window(UpdateUiHarness flow,bool controls=true)
    {
        var window=new System.Windows.Window { Title="ScreenIt — updater UI verification",Width=780,Height=620,MinWidth=700,MinHeight=510,WindowStartupLocation=WindowStartupLocation.CenterScreen };
        // Match the width of the real Settings content column; unlike its Grid,
        // a bare Window can measure a StackPanel with infinite available width.
        var root=new StackPanel { Margin=new Thickness(28),Width=552,HorizontalAlignment=HorizontalAlignment.Left };
        void Build()
        {
            root.Children.Clear();window.Background=Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(20,25,34) : Brushes.White;root.Background=window.Background;
            root.Children.Add(new TextBlock { Text=L.T("About"),FontSize=20,FontWeight=FontWeights.SemiBold,Foreground=UtilityUi.Ink,Margin=new Thickness(0,0,0,22) });
            root.Children.Add(new TextBlock { Text="ScreenIt "+GithubUpdateSource.CurrentVersion,FontSize=18,Foreground=UtilityUi.Ink,Margin=new Thickness(0,0,0,12) });
            root.Children.Add(new TextBlock { Text=L.T("Updates"),FontSize=16,FontWeight=FontWeights.SemiBold,Foreground=UtilityUi.Ink,Margin=new Thickness(0,0,0,12) });
            root.Children.Add(new UpdateSection(flow,SettingsWindow.Action));
            if(controls)
            {
                var panel=new WrapPanel { Margin=new Thickness(0,24,0,0) };
                panel.Children.Add(SettingsWindow.Action("← Previous",()=>flow.Move(-1)));panel.Children.Add(SettingsWindow.Action("Next →",()=>flow.Move(1)));
                panel.Children.Add(SettingsWindow.Action("RU / EN",()=> { L.Select(L.Language=="ru" ? "en" : "ru");Build(); }));
                panel.Children.Add(SettingsWindow.Action("Light / Dark",()=> { Appearance.Select(Appearance.Current==UiTheme.Dark ? UiTheme.Light : UiTheme.Dark);Build(); }));root.Children.Add(panel);
                root.Children.Add(new TextBlock { Text="Developer harness · mock states only · no network / installer / shutdown.\nRetry resets to 0%; use Next to follow the successful handoff.",TextWrapping=TextWrapping.Wrap,Foreground=UtilityUi.Muted,FontSize=12 });
            }
        }
        Build();window.Content=root;return window;
    }
    internal static void Run(bool snapshot)
    {
        var app=new Application { ShutdownMode=ShutdownMode.OnMainWindowClose };L.Select("ru");Appearance.Choose(ThemePreference.Light);
        var flow=new UpdateUiHarness();var window=Window(flow);app.Startup+=(_,_)=> { if(snapshot) window.Dispatcher.BeginInvoke(()=> { Snapshot(window,"harness-run-check");window.Close(); }); };app.Run(window);
    }
    internal static void Snapshot(Window window,string name)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen()) { dc.DrawRectangle(window.Background,null,new Rect(0,0,content.ActualWidth,content.ActualHeight));dc.DrawRectangle(new VisualBrush(content),null,new Rect(0,0,content.ActualWidth,content.ActualHeight)); }
        var bitmap=new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/updater-ui"));Directory.CreateDirectory(folder);
        using var output=File.Create(Path.Combine(folder,name+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(output);
    }
}
