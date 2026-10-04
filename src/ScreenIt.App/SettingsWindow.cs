using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Automation;

internal sealed class SettingsWindow : Window
{
    private readonly Coordinator owner;
    private readonly IUpdateSource updates;
    private readonly CancellationTokenSource cancellation=new();
    private int page;
    private GlobalAction? recording;
    private bool updating;
    internal bool Recording => recording!=null;
    private string updateStatus="",error="";
    private UpdateRelease? release;
    internal UiTheme AppliedTheme { get; private set; }
    internal int Page => page;
    internal void SelectPage(int value) { page=value;recording=null;Refresh(); }
    internal SettingsWindow(Coordinator owner,IUpdateSource? updates=null)
    {
        this.owner=owner;this.updates=updates ?? new GithubUpdateSource();
        Title="ScreenIt";Icon=UtilityUi.WindowIcon();Width=780;Height=540;MinWidth=700;MinHeight=510;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SourceInitialized+=(_,_)=>Refresh();
        Closed+=(_,_)=> { cancellation.Cancel();cancellation.Dispose();Content=null; };
        PreviewKeyDown+=Record;
        Deactivated+=(_,_)=> { if(recording!=null) { recording=null;Refresh(); } };
        Refresh();
    }
    private static TextBlock Text(string value,double size=14,bool bold=false) => new() { Text=L.T(value),FontSize=size,FontWeight=bold ? FontWeights.SemiBold : FontWeights.Normal,Foreground=UtilityUi.Ink,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) };
    private static Button Action(string label,Action action)
    {
        var button=UtilityUi.Button(label,label,action);button.Background=Appearance.Palette.Surface;button.BorderBrush=Appearance.Palette.Border;
        button.Height=42;button.HorizontalContentAlignment=HorizontalAlignment.Center;button.VerticalContentAlignment=VerticalAlignment.Center;button.Cursor=Cursors.Hand;button.HorizontalAlignment=HorizontalAlignment.Left;button.Margin=new Thickness(0,0,0,24);
        button.Resources["SettingsAccent"]=UtilityUi.Accent;
        button.Resources["SettingsHover"]=Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(53,65,83) : UtilityUi.Brush(229,235,246);
        button.Resources["SettingsPressed"]=Appearance.Current==UiTheme.Dark ? UtilityUi.Brush(13,19,31) : UtilityUi.Brush(202,214,235);
        button.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="Button">
              <Border x:Name="ButtonSurface" CornerRadius="6" Background="{TemplateBinding Background}" BorderBrush="{TemplateBinding BorderBrush}" BorderThickness="{TemplateBinding BorderThickness}" Padding="18,7">
                <ContentPresenter HorizontalAlignment="{TemplateBinding HorizontalContentAlignment}" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"/>
              </Border>
              <ControlTemplate.Triggers>
                <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="ButtonSurface" Property="Background" Value="{DynamicResource SettingsHover}"/><Setter TargetName="ButtonSurface" Property="BorderBrush" Value="{DynamicResource SettingsAccent}"/></Trigger>
                <Trigger Property="IsKeyboardFocused" Value="True"><Setter TargetName="ButtonSurface" Property="BorderBrush" Value="{DynamicResource SettingsAccent}"/><Setter TargetName="ButtonSurface" Property="BorderThickness" Value="2"/></Trigger>
                <Trigger Property="IsPressed" Value="True"><Setter TargetName="ButtonSurface" Property="Background" Value="{DynamicResource SettingsPressed}"/><Setter TargetName="ButtonSurface" Property="BorderBrush" Value="{DynamicResource SettingsAccent}"/><Setter TargetName="ButtonSurface" Property="BorderThickness" Value="2"/></Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        return button;
    }
    private static readonly Style ChoiceCursorStyle = new(typeof(ComboBox))
    {
        Setters = { new Setter(FrameworkElement.CursorProperty, Cursors.Hand) }
    };
    private static ComboBox Choice(string name,IEnumerable<(object Value,string Label)> choices,object selected,Action<object> changed)
    {
        var combo=new ComboBox { Style=ChoiceCursorStyle,Width=300,Height=38,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,0,0,28),FontSize=14,
            Background=Appearance.Palette.Surface,Foreground=UtilityUi.Ink,BorderBrush=Appearance.Palette.Border,BorderThickness=new Thickness(1),FocusVisualStyle=FocusStyle() };
        AutomationProperties.SetName(combo,L.T(name));
        combo.Resources["ChoiceSurface"]=Appearance.Palette.Surface;combo.Resources["ChoiceInk"]=UtilityUi.Ink;combo.Resources["ChoiceBorder"]=Appearance.Palette.Border;combo.Resources["ChoiceAccent"]=UtilityUi.Accent;
        combo.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBox">
              <Grid>
                <ToggleButton Focusable="False" IsChecked="{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <ToggleButton.Template><ControlTemplate TargetType="ToggleButton">
                    <Border x:Name="surface" Background="{DynamicResource ChoiceSurface}" BorderBrush="{DynamicResource ChoiceBorder}" BorderThickness="1" CornerRadius="6"/>
                    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="{DynamicResource ChoiceAccent}"/></Trigger><Trigger Property="IsChecked" Value="True"><Setter TargetName="surface" Property="BorderBrush" Value="{DynamicResource ChoiceAccent}"/></Trigger></ControlTemplate.Triggers>
                  </ControlTemplate></ToggleButton.Template>
                </ToggleButton>
                <ContentPresenter Margin="12,0,36,0" VerticalAlignment="Center" IsHitTestVisible="False" Content="{TemplateBinding SelectionBoxItem}" ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"/>
                <TextBlock Text="⌄" Foreground="{DynamicResource ChoiceInk}" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,0,12,3" IsHitTestVisible="False"/>
                <Popup x:Name="PART_Popup" IsOpen="{TemplateBinding IsDropDownOpen}" Placement="Bottom" AllowsTransparency="True" Focusable="False">
                  <Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}" Background="{DynamicResource ChoiceSurface}" BorderBrush="{DynamicResource ChoiceBorder}" BorderThickness="1" CornerRadius="6" Padding="4"><ScrollViewer MaxHeight="240"><ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/></ScrollViewer></Border>
                </Popup>
              </Grid>
            </ControlTemplate>
            """);
        combo.Resources[typeof(ComboBoxItem)]=System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ComboBoxItem">
              <Setter Property="Foreground" Value="{DynamicResource ChoiceInk}"/><Setter Property="Padding" Value="10,8"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ComboBoxItem"><Border x:Name="item" Background="Transparent" CornerRadius="4" Padding="{TemplateBinding Padding}"><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property="IsHighlighted" Value="True"><Setter TargetName="item" Property="Background" Value="{DynamicResource ChoiceAccent}"/><Setter Property="Foreground" Value="White"/></Trigger><Trigger Property="IsSelected" Value="True"><Setter Property="FontWeight" Value="SemiBold"/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        foreach(var (value,label) in choices) { var item=new ComboBoxItem { Content=label,Tag=value };combo.Items.Add(item);if(Equals(value,selected)) combo.SelectedItem=item; }
        combo.SelectionChanged+=(_,_)=> { if(combo.SelectedItem is ComboBoxItem item && item.Tag is { } value) changed(value); };
        return combo;
    }
    internal void Refresh()
    {
        string? focusName=Keyboard.FocusedElement is FrameworkElement focused && Window.GetWindow(focused)==this ? AutomationProperties.GetName(focused) : null;
        AppliedTheme=Appearance.Current;Background=Appearance.Palette.Editor;
        // Header belongs to the sidebar, not to a shared row above both columns.
        var root=new Grid { Margin=new Thickness(22),Background=Appearance.Palette.Editor };
        root.ColumnDefinitions.Add(new() { Width=new GridLength(170) });root.ColumnDefinitions.Add(new());
        var sidebar=new StackPanel { Margin=new Thickness(0,0,20,0) };root.Children.Add(sidebar);
        var header=new StackPanel { Orientation=Orientation.Horizontal,Margin=new Thickness(0,0,0,24) };
        header.Children.Add(new Image { Source=UtilityUi.WindowIcon(),Width=36,Height=36,Margin=new Thickness(0,0,10,0) });
        var title=new StackPanel();var appTitle=Text("ScreenIt",20,true);appTitle.Margin=new Thickness(0,0,0,4);title.Children.Add(appTitle);var subtitle=Text("Settings");subtitle.Foreground=UtilityUi.Muted;subtitle.Margin=new();title.Children.Add(subtitle);header.Children.Add(title);sidebar.Children.Add(header);
        var nav=new StackPanel();sidebar.Children.Add(nav);
        string[] pages=["General","Hotkeys","About"];
        for(int i=0;i<pages.Length;i++)
        {
            int index=i;var button=Action(pages[i],()=>SelectPage(index));button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new Thickness(0,0,0,8);
            if(page==i) { button.BorderBrush=UtilityUi.Accent;button.BorderThickness=new Thickness(2);button.Foreground=Appearance.Palette.AccentText;button.FontWeight=FontWeights.SemiBold;AutomationProperties.SetItemStatus(button,"Current page"); }
            nav.Children.Add(button);
        }
        var content=new StackPanel { VerticalAlignment=VerticalAlignment.Top };var scroll=new ScrollViewer { Content=content,VerticalContentAlignment=VerticalAlignment.Top,HorizontalContentAlignment=HorizontalAlignment.Stretch,VerticalScrollBarVisibility=ScrollBarVisibility.Auto };Grid.SetColumn(scroll,1);root.Children.Add(scroll);
        var pageTitle=Text(pages[page],20,true);pageTitle.Margin=new Thickness(0,0,0,22);content.Children.Add(pageTitle);
        if(page==0)
        {
            var themeLabel=Text("Appearance",14,true);themeLabel.Margin=new Thickness(0,0,0,8);content.Children.Add(themeLabel);
            content.Children.Add(Choice("Appearance",Enum.GetValues<ThemePreference>().Select(t=>((object)t,L.T(t.ToString()))),owner.Preferences.Theme,value=>owner.ChangePreferences(theme:(ThemePreference)value)));
            var languageLabel=Text("Language",14,true);languageLabel.Margin=new Thickness(0,0,0,8);content.Children.Add(languageLabel);
            content.Children.Add(Choice("Language",new[]{((object)"en","English"),((object)"ru","Русский")},owner.Preferences.Language,value=>owner.ChangePreferences(language:(string)value)));
        }
        else if(page==1)
        {
            content.Children.Add(Text("Use Ctrl, Alt, Shift or Win with a letter, number, F1–F12 or navigation key."));
            foreach(var action in Enum.GetValues<GlobalAction>())
            {
                var row=new Grid { Margin=new Thickness(0,0,0,12) };row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new() { Width=new GridLength(220) });
                var caption=Text(action==GlobalAction.Paste ? "Paste Session" : action==GlobalAction.Clear ? "Clear Session" : "Capture");caption.VerticalAlignment=VerticalAlignment.Center;caption.Margin=new();row.Children.Add(caption);
                var button=Action(recording==action ? "Press shortcut…" : owner.Preferences.Hotkeys[action].ToString(),()=> { recording=action;error="";Refresh(); });button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new();Grid.SetColumn(button,1);row.Children.Add(button);content.Children.Add(row);
                if(recording==action) Dispatcher.BeginInvoke(()=> { if(recording==action && IsVisible) button.Focus(); });
            }
            content.Children.Add(Action("Reset defaults",()=> { error=owner.ChangeHotkeys(Hotkey.Defaults()) ? "" : "Shortcut unavailable or invalid. Previous shortcuts remain active.";Refresh(); }));
        }
        else
        {
            content.Children.Add(Text("ScreenIt "+GithubUpdateSource.CurrentVersion,18,true));content.Children.Add(Text("Local screenshot annotation utility for visual feedback."));
            content.Children.Add(Action("Open repository",()=>Open(new Uri(GithubUpdateSource.Repository))));
            content.Children.Add(Text("Updates",16,true));var check=Action("Check for updates",async()=>await CheckUpdates());check.IsEnabled=!updating;content.Children.Add(check);
            if(updateStatus.Length>0) content.Children.Add(Text(updateStatus));
            if(release!=null)
            {
                bool installed=UpdateTransfer.Installed();var download=Action(installed ? "Download and install" : "Open release page",async()=> { if(installed) await Download();else Open(release.Page); });download.IsEnabled=!updating;content.Children.Add(download);
                if(!installed) content.Children.Add(Text("Portable copy: install updates manually from the release page."));
            }
        }
        if(error.Length>0) { var failure=Text(error);failure.Foreground=Appearance.Palette.Warning;failure.Margin=new Thickness(0,16,0,0);content.Children.Add(failure); }
        Content=root;
        if(!string.IsNullOrEmpty(focusName)) Dispatcher.BeginInvoke(()=> { if(IsVisible && IsActive && ReferenceEquals(Content,root)) FindFocus(root,L.T(focusName))?.Focus(); });
        var hwnd=new WindowInteropHelper(this).Handle;if(hwnd!=IntPtr.Zero) { int dark=Appearance.Current==UiTheme.Dark ? 1 : 0;DwmSetWindowAttribute(hwnd,20,ref dark,4); }
    }
    private static FrameworkElement? FindFocus(DependencyObject root,string name)
    {
        if(root is FrameworkElement element && AutomationProperties.GetName(element)==name) return element;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) if(FindFocus(child,name) is { } found) return found;return null;
    }
    private static Style FocusStyle()
    {
        var border=new FrameworkElementFactory(typeof(Border));border.SetValue(Border.BorderBrushProperty,UtilityUi.Accent);border.SetValue(Border.BorderThicknessProperty,new Thickness(1));border.SetValue(Border.CornerRadiusProperty,new CornerRadius(4));
        var style=new Style(typeof(Control));style.Setters.Add(new Setter(Control.TemplateProperty,new ControlTemplate(typeof(Control)) { VisualTree=border }));return style;
    }
    private void Record(object sender,KeyEventArgs e)
    {
        if(e.Key==Key.Escape) { e.Handled=true;if(recording!=null) { recording=null;Refresh(); }else Close();return; }
        if(recording==null) return;e.Handled=true;
        var key=e.Key==Key.System ? e.SystemKey : e.Key;
        if(key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin) return;
        var mods=Keyboard.Modifiers;uint flags=(mods.HasFlag(ModifierKeys.Control) ? 2u : 0)|(mods.HasFlag(ModifierKeys.Alt) ? 1u : 0)|(mods.HasFlag(ModifierKeys.Shift) ? 4u : 0)|(mods.HasFlag(ModifierKeys.Windows) ? 8u : 0);
        RecordShortcut(new(flags,(uint)KeyInterop.VirtualKeyFromKey(key)));
    }
    internal void RecordShortcut(Hotkey key)
    {
        if(recording is not { } action) return;
        var proposed=new Dictionary<GlobalAction,Hotkey>(owner.Preferences.Hotkeys) { [action]=key };
        if(owner.ChangeHotkeys(proposed)) { recording=null;error=""; }else error="Shortcut unavailable or invalid. Previous shortcuts remain active.";Refresh();
    }
    internal async Task CheckUpdates()
    {
        if(updating) return;updating=true;error="";release=null;updateStatus="Checking…";Refresh();
        try { release=await updates.Check(cancellation.Token);updateStatus=release==null ? "You are up to date." : $"Version {release.Version} is available."; }
        catch(OperationCanceledException) { return; }catch(Exception) { error="Update check failed. Please retry.";updateStatus=""; }
        finally { updating=false;if(!cancellation.IsCancellationRequested) Refresh(); }
    }
    private async Task Download()
    {
        if(updating || release==null || !UpdateTransfer.Installed()) return;updating=true;error="";updateStatus="Downloading and verifying…";Refresh();
        try
        {
            var path=await new UpdateTransfer(updates).Prepare(release,cancellation.Token);
            if(cancellation.IsCancellationRequested) return;
            // Recheck after download: installation identity and file integrity cannot change silently.
            if(!UpdateTransfer.Installed()) throw new InvalidOperationException("Installation changed.");
            await UpdateTransfer.Verify(path,Path.Combine(Path.GetDirectoryName(path)!,"SHA256SUMS.txt"),cancellation.Token);
            try
            {
                UpdateTransfer.LaunchVerified(path,()=>UtilityUi.Confirm(this,"Install update?",L.T("ScreenIt will close to install the update. Your current screenshot session is stored only in memory and will be lost. Continue?")+"\n\n"+L.T("The installer is unsigned. Windows SmartScreen may show a warning."),"Install"),
                    installer=> { if(System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer) { UseShellExecute=true })==null) throw new IOException("Installer did not start."); },owner.ExitForUpdate);
            }
            catch(Exception) { error="Could not start the installer. Your session is unchanged."; }
            updateStatus="";
        }
        catch(OperationCanceledException) { return; }catch(Exception) { error="Update download or verification failed. Nothing was installed.";updateStatus=""; }
        finally { updating=false;if(!cancellation.IsCancellationRequested) Refresh(); }
    }
    private void Open(Uri uri) { try { UpdateTransfer.OpenPage(uri); }catch(Exception) { error="Could not open the page.";Refresh(); } }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int length);
}
