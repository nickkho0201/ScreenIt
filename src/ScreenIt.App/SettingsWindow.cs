using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

internal sealed class SettingsWindow : Window
{
    private readonly Coordinator owner;
    private readonly UpdateFlow updates;
    private int page;
    private GlobalAction? recording;
    private ShortcutRecordingHook? recorder;
    private HwndSource? recorderSource;
    private int recordingGeneration;
    internal bool RecorderInstalled => recorder!=null;
    internal bool RecordInjectedInput { get; set; } // Native verification only; production ignores synthetic input.
    internal bool Recording => recording!=null;
    internal string ValidationError => L.T(error);
    private string error="";
    internal UiTheme AppliedTheme { get; private set; }
    internal int Page => page;
    internal void SelectPage(int value) { page=value;StopRecording();Refresh(); }
    private void StopRecording() { recording=null;recordingGeneration++;recorder?.Dispose();recorder=null; }
    private void BeginRecording(GlobalAction action)
    {
        StopRecording();error="";
        recording=action;
        try
        {
            var hwnd=new WindowInteropHelper(this).EnsureHandle();
            if(recorderSource==null) { recorderSource=HwndSource.FromHwnd(hwnd)!;recorderSource.AddHook(RecorderMessage); }
            recorder=new ShortcutRecordingHook(hwnd,recordingGeneration,RecordInjectedInput);
        }
        catch(System.ComponentModel.Win32Exception ex)
        {
            System.Diagnostics.Trace.TraceError("ScreenIt shortcut recorder installation failed: {0}",ex.NativeErrorCode);
            StopRecording();error="Shortcut recording could not start. Previous shortcuts remain active.";
        }
        Refresh();
    }
    internal SettingsWindow(Coordinator owner,IUpdateSource? updates=null,UpdateFlow? updateFlow=null)
    {
        this.owner=owner;
        this.updates=updateFlow ?? new(updates ?? new GithubUpdateSource(),UpdateTransfer.Installed,
            ()=>Task.FromResult(IsVisible && IsActive && UtilityUi.Confirm(this,"Install update?",L.T("ScreenIt will close to install the update. Your current screenshot session is stored only in memory and will be lost. Continue?")+"\n\n"+L.T("The installer is unsigned. Windows SmartScreen may show a warning."),"Install")),
            installer=> { if(System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(installer) { UseShellExecute=true })==null) throw new IOException("Installer did not start."); },owner.ExitForUpdate,UpdateTransfer.OpenPage);
        Title="ScreenIt";Icon=UtilityUi.WindowIcon();Width=780;Height=620;MinWidth=700;MinHeight=510;WindowStartupLocation=WindowStartupLocation.CenterScreen;
        SourceInitialized+=(_,_)=>Refresh();
        Closed+=(_,_)=> { StopRecording();recorderSource?.RemoveHook(RecorderMessage);recorderSource=null;this.updates.Dispose();Content=null; };
        PreviewKeyDown+=Record;
        Deactivated+=(_,_)=> { if(recording!=null) { StopRecording();Refresh(); } };
        IsVisibleChanged+=(_,_)=> { if(!IsVisible && recording!=null) { StopRecording();Refresh(); } };
        Refresh();
    }
    private static TextBlock Text(string value,double size=14,bool bold=false) => new() { Text=L.T(value),FontSize=size,FontWeight=bold ? FontWeights.SemiBold : FontWeights.Normal,Foreground=UtilityUi.Ink,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,0,0,12) };
    internal static Button Action(string label,Action action)
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
    private static string? StartupNote(AutoStartStatus status) => status switch
    {
        { ExternallyDisabled: true } => L.T("Windows is currently blocking this startup entry. Enable it in Windows startup apps."),
        { ApprovalUnknown: true } => L.T("Windows manages this startup entry; ScreenIt cannot confirm its state."),
        { Registration: AutoStartRegistration.Unreadable } => L.T("The Windows startup setting could not be read."),
        _ => null
    };
    // ScreenIt-styled toggle: a real ToggleButton whose checked state only reflects AutoStartStatus.
    // The template keeps the whole 48x28 surface hit-testable and animates nothing, so no reduced-motion state applies.
    private static ToggleButton Switch(bool on,bool enabled,Action<bool> changed)
    {
        bool dark=Appearance.Current==UiTheme.Dark;
        var toggle=new ToggleButton { IsChecked=on,IsEnabled=enabled,Width=48,Height=28,Cursor=Cursors.Hand,Focusable=true,
            HorizontalAlignment=HorizontalAlignment.Right,VerticalAlignment=VerticalAlignment.Center,Background=Brushes.Transparent,
            BorderThickness=new Thickness(0),FocusVisualStyle=null };
        AutomationProperties.SetName(toggle,L.T("Start with Windows"));
        AutomationProperties.SetHelpText(toggle,L.T("Launch ScreenIt automatically when you sign in."));
        toggle.Resources["SwitchOff"]=dark ? UtilityUi.Brush(71,85,105) : UtilityUi.Brush(148,163,184);
        toggle.Resources["SwitchOffHover"]=dark ? UtilityUi.Brush(88,102,122) : UtilityUi.Brush(134,150,171);
        toggle.Resources["SwitchOffPressed"]=dark ? UtilityUi.Brush(58,70,86) : UtilityUi.Brush(120,137,159);
        toggle.Resources["SwitchOn"]=UtilityUi.Accent;
        toggle.Resources["SwitchOnHover"]=UtilityUi.Brush(84,104,216);
        toggle.Resources["SwitchOnPressed"]=UtilityUi.Brush(52,68,166);
        toggle.Resources["SwitchBorder"]=dark ? UtilityUi.Brush(59,71,87) : UtilityUi.Brush(203,213,225);
        toggle.Resources["SwitchThumb"]=UtilityUi.Brush(255,255,255);
        toggle.Resources["SwitchAccent"]=UtilityUi.Accent;
        toggle.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("""
            <ControlTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ToggleButton">
              <Grid Width="48" Height="28">
                <Border x:Name="SwitchFocus" CornerRadius="14" BorderThickness="2" BorderBrush="Transparent" Background="Transparent"/>
                <Border x:Name="SwitchTrack" Width="40" Height="22" CornerRadius="11" HorizontalAlignment="Center" VerticalAlignment="Center" Background="{DynamicResource SwitchOff}" BorderBrush="{DynamicResource SwitchBorder}" BorderThickness="1">
                  <Border x:Name="SwitchThumb" Width="16" Height="16" CornerRadius="8" HorizontalAlignment="Left" VerticalAlignment="Center" Background="{DynamicResource SwitchThumb}" Margin="3,0,0,0" IsHitTestVisible="False">
                    <Border.RenderTransform><TranslateTransform X="0"/></Border.RenderTransform>
                  </Border>
                </Border>
              </Grid>
              <ControlTemplate.Triggers>
                <Trigger Property="IsChecked" Value="True">
                  <Setter TargetName="SwitchTrack" Property="Background" Value="{DynamicResource SwitchOn}"/>
                  <Setter TargetName="SwitchTrack" Property="BorderBrush" Value="{DynamicResource SwitchOn}"/>
                  <Setter TargetName="SwitchThumb" Property="RenderTransform">
                    <Setter.Value><TranslateTransform X="16"/></Setter.Value>
                  </Setter>
                </Trigger>
                <MultiTrigger>
                  <MultiTrigger.Conditions><Condition Property="IsChecked" Value="False"/><Condition Property="IsMouseOver" Value="True"/></MultiTrigger.Conditions>
                  <Setter TargetName="SwitchTrack" Property="Background" Value="{DynamicResource SwitchOffHover}"/>
                </MultiTrigger>
                <MultiTrigger>
                  <MultiTrigger.Conditions><Condition Property="IsChecked" Value="True"/><Condition Property="IsMouseOver" Value="True"/></MultiTrigger.Conditions>
                  <Setter TargetName="SwitchTrack" Property="Background" Value="{DynamicResource SwitchOnHover}"/>
                </MultiTrigger>
                <MultiTrigger>
                  <MultiTrigger.Conditions><Condition Property="IsChecked" Value="False"/><Condition Property="IsPressed" Value="True"/></MultiTrigger.Conditions>
                  <Setter TargetName="SwitchTrack" Property="Background" Value="{DynamicResource SwitchOffPressed}"/>
                </MultiTrigger>
                <MultiTrigger>
                  <MultiTrigger.Conditions><Condition Property="IsChecked" Value="True"/><Condition Property="IsPressed" Value="True"/></MultiTrigger.Conditions>
                  <Setter TargetName="SwitchTrack" Property="Background" Value="{DynamicResource SwitchOnPressed}"/>
                </MultiTrigger>
                <Trigger Property="IsKeyboardFocused" Value="True">
                  <Setter TargetName="SwitchFocus" Property="BorderBrush" Value="{DynamicResource SwitchAccent}"/>
                </Trigger>
                <Trigger Property="IsEnabled" Value="False"><Setter Property="Opacity" Value="0.4"/></Trigger>
              </ControlTemplate.Triggers>
            </ControlTemplate>
            """);
        toggle.Click+=(_,_)=>changed(toggle.IsChecked==true);
        return toggle;
    }
    // Compact settings row: title and explanation on the left, toggle on the right, on an existing ScreenIt surface.
    private static Border StartupRow(AutoStartStatus status,ToggleButton toggle)
    {
        var text=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text=L.T("Start with Windows"),FontSize=14,FontWeight=FontWeights.SemiBold,Foreground=UtilityUi.Ink,TextWrapping=TextWrapping.Wrap });
        text.Children.Add(new TextBlock { Text=L.T("Launch ScreenIt automatically when you sign in."),FontSize=12,Foreground=UtilityUi.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,3,0,0) });
        if(StartupNote(status) is { } note) text.Children.Add(new TextBlock { Text=note,FontSize=12,Foreground=UtilityUi.Muted,TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,0) });
        var grid=new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(1,GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
        grid.Children.Add(text);
        toggle.Margin=new Thickness(16,0,0,0);Grid.SetColumn(toggle,1);grid.Children.Add(toggle);
        return new Border { Background=Appearance.Palette.Surface,BorderBrush=Appearance.Palette.Border,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(6),Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,0,0,12),Child=grid };
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
        scroll.Resources["ScrollThumb"]=Appearance.Palette.Border;scroll.Resources["ScrollThumbActive"]=UtilityUi.Accent;
        scroll.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)]=System.Windows.Markup.XamlReader.Parse("""
            <Style xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" TargetType="ScrollBar">
              <Setter Property="Width" Value="10"/><Setter Property="MinWidth" Value="0"/><Setter Property="Background" Value="Transparent"/>
              <Setter Property="Template"><Setter.Value><ControlTemplate TargetType="ScrollBar">
                <Track x:Name="PART_Track" Orientation="Vertical" IsDirectionReversed="True" Minimum="{TemplateBinding Minimum}" Maximum="{TemplateBinding Maximum}" ViewportSize="{TemplateBinding ViewportSize}" Value="{Binding Value, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}">
                  <Track.DecreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageUpCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
                  <Track.Thumb><Thumb MinHeight="24"><Thumb.Template><ControlTemplate TargetType="Thumb">
                    <Border x:Name="thumb" Background="{DynamicResource ScrollThumb}" CornerRadius="3" Margin="2,0"/>
                    <ControlTemplate.Triggers><Trigger Property="IsMouseOver" Value="True"><Setter TargetName="thumb" Property="Background" Value="{DynamicResource ScrollThumbActive}"/></Trigger><Trigger Property="IsDragging" Value="True"><Setter TargetName="thumb" Property="Background" Value="{DynamicResource ScrollThumbActive}"/></Trigger></ControlTemplate.Triggers>
                  </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
                  <Track.IncreaseRepeatButton><RepeatButton Command="{x:Static ScrollBar.PageDownCommand}" Focusable="False"><RepeatButton.Template><ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
                </Track>
              </ControlTemplate></Setter.Value></Setter>
            </Style>
            """);
        scroll.Loaded+=(_,_)=>
        {
            // The Windows ScrollViewer template supplies local scrollbar values;
            // explicitly apply our fallback style to its vertical template part.
            scroll.ApplyTemplate();
            if(scroll.Template.FindName("PART_VerticalScrollBar",scroll) is System.Windows.Controls.Primitives.ScrollBar vertical)
            {
                vertical.Style=(Style)scroll.Resources[typeof(System.Windows.Controls.Primitives.ScrollBar)];vertical.MinWidth=0;vertical.Width=10;
            }
        };
        var pageTitle=Text(pages[page],20,true);pageTitle.Margin=new Thickness(0,0,0,22);content.Children.Add(pageTitle);
        if(page==0)
        {
            var themeLabel=Text("Appearance",14,true);themeLabel.Margin=new Thickness(0,0,0,8);content.Children.Add(themeLabel);
            content.Children.Add(Choice("Appearance",Enum.GetValues<ThemePreference>().Select(t=>((object)t,L.T(t.ToString()))),owner.Preferences.Theme,value=>owner.ChangePreferences(theme:(ThemePreference)value)));
            var languageLabel=Text("Language",14,true);languageLabel.Margin=new Thickness(0,0,0,8);content.Children.Add(languageLabel);
            content.Children.Add(Choice("Language",new[]{((object)"en","English"),((object)"ru","Русский")},owner.Preferences.Language,value=>owner.ChangePreferences(language:(string)value)));
            var status=owner.AutoStartStatus;
            // The toggle owns the real state: every Refresh rebuilds its appearance from AutoStartStatus.
            var startup=Switch(status.Enabled,status.Registration!=AutoStartRegistration.Unreadable,value=> { error=owner.ChangeAutoStart(value) ? "" : owner.LastAutoStartFailure!.Message;Refresh(); });
            content.Children.Add(StartupRow(status,startup));
        }
        else if(page==1)
        {
            content.Children.Add(Text("Use Ctrl, Alt, Shift or Win with a letter, number, F1–F12 or navigation key."));
            foreach(var action in Enum.GetValues<GlobalAction>())
            {
                var row=new Grid { Margin=new Thickness(0,0,0,12) };row.ColumnDefinitions.Add(new());row.ColumnDefinitions.Add(new() { Width=new GridLength(220) });
                var caption=Text(action==GlobalAction.Paste ? "Paste Session" : action==GlobalAction.Clear ? "Clear Session" : "Capture");caption.VerticalAlignment=VerticalAlignment.Center;caption.Margin=new();row.Children.Add(caption);
                var button=Action(recording==action ? "Press shortcut…" : owner.Preferences.Hotkeys[action].ToString(),()=>BeginRecording(action));button.HorizontalAlignment=HorizontalAlignment.Stretch;button.Margin=new();Grid.SetColumn(button,1);row.Children.Add(button);content.Children.Add(row);
                if(recording==action) Dispatcher.BeginInvoke(()=> { if(recording==action && IsVisible) button.Focus(); });
            }
            content.Children.Add(Action("Reset defaults",()=> { StopRecording();error=owner.ChangeHotkeys(Hotkey.Defaults()) ? "" : owner.LastHotkeyFailure!.Message;Refresh(); }));
            var modifierLabel=Text("Annotation modifier",14,true);modifierLabel.Margin=new Thickness(0,0,0,8);content.Children.Add(modifierLabel);
            content.Children.Add(Choice("Annotation modifier",Enum.GetValues<AnnotationModifier>().Select(m=>((object)m,m.ToString())),owner.Preferences.AnnotationModifier,value=> {
                error=owner.ChangeAnnotationModifier((AnnotationModifier)value) ? "" : "Settings could not be saved. Please retry.";Refresh();
            }));
            content.Children.Add(Text("Hold on release or with Space to annotate. Otherwise the screenshot is added immediately."));
        }
        else
        {
            content.Children.Add(Text("ScreenIt "+GithubUpdateSource.CurrentVersion,18,true));content.Children.Add(Text("Local screenshot annotation utility for visual feedback."));
            content.Children.Add(Action("Open repository",()=>Open(new Uri(GithubUpdateSource.Repository))));
            content.Children.Add(Text("Updates",16,true));content.Children.Add(new UpdateSection(updates,Action));
        }
        if(error.Length>0) { var failure=Text(error);failure.Foreground=Appearance.Palette.Warning;failure.Margin=new Thickness(0,16,0,0);content.Children.Add(failure);if(page==1) failure.Loaded+=(_,_)=>failure.BringIntoView(); }
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
        if(recording!=null) { e.Handled=true;return; } // The recorder owns chord input; WPF is not a second source.
        if(e.Key==Key.Escape) { e.Handled=true;Close(); }
    }
    private IntPtr RecorderMessage(IntPtr hwnd,int message,IntPtr wp,IntPtr lp,ref bool handled)
    {
        if(message!=ShortcutRecordingHook.Message) return IntPtr.Zero;
        handled=true;
        if(recording==null || wp.ToInt64()!=recordingGeneration || !IsVisible || !IsActive) return IntPtr.Zero;
        if(lp==IntPtr.Zero) { StopRecording();Refresh(); }
        else RecordShortcut(new((uint)((long)lp&15),(uint)(((long)lp>>16)&0xFFFF)));
        return IntPtr.Zero;
    }
    internal void RecordShortcut(Hotkey key)
    {
        if(recording is not { } action) return;
        var proposed=new Dictionary<GlobalAction,Hotkey>(owner.Preferences.Hotkeys) { [action]=key };
        error=owner.ChangeHotkeys(proposed) ? "" : owner.LastHotkeyFailure!.Message;StopRecording();Refresh();
    }
    internal Task CheckUpdates() => updates.Check();
    private void Open(Uri uri) { try { UpdateTransfer.OpenPage(uri); }catch(Exception) { error="Could not open the page.";Refresh(); } }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd,int attribute,ref int value,int length);
}
