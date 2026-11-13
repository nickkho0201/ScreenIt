using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using ScreenIt.Core;

// Isolated Classic/Ember selection checks. No desktop capture, clipboard, real settings,
// global hotkeys or receiver automation: only synthetic windows and temp preference files.
internal static class DesignSelectionChecks
{
    private static readonly Color ClassicBlue=Color.FromRgb(66,85,197);
    private static readonly Color EmberSignal=Color.FromRgb(240,77,50);
    private static readonly Color EmberPaper=Color.FromRgb(251,254,248);

    internal static async Task Run(Action<bool,string> check)
    {
        string folder=Path.Combine(Path.GetTempPath(),"ScreenItDesignSelection",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        var language=L.Language;var theme=Appearance.Preference;var design=Appearance.Design;
        try
        {
            Persistence(check,folder);
            Tokens(check);
            PainterDesigns(check);
            await SettingsDesigns(check,folder);
            OverlayDesigns(check);
            PromptAndToastDesigns(check);
            await FirstFrame(check);
        }
        finally
        {
            L.Select(language);Appearance.ChooseDesign(design);Appearance.Choose(theme);
            try { foreach(var file in Directory.GetFiles(folder)) File.Delete(file);Directory.Delete(folder,true); } catch(IOException) { }
        }
    }

    private static void Persistence(Action<bool,string> check,string folder)
    {
        string path=Path.Combine(folder,"settings.json");
        File.WriteAllText(path,"{\"theme\":\"light\",\"language\":\"ru\",\"annotationModifier\":\"shift\",\"disableClearConfirmation\":true,\"future\":42,\"hotkeys\":{\"capture\":{\"modifiers\":6,\"key\":81}}}");
        var old=Preferences.Load(path);
        check(old.Design==DesignPreference.Classic,"Old file without design defaults Classic");
        check(old.Theme==ThemePreference.Light && old.Language=="ru" && old.AnnotationModifier==AnnotationModifier.Shift && old.DisableClearConfirmation,"Old preferences still load beside design");
        old.Save(path);
        var saved=System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement;
        check(saved.GetProperty("design").GetString()=="classic","Classic persists as lowercase design field");
        check(saved.GetProperty("future").GetInt32()==42 && saved.GetProperty("annotationModifier").GetString()=="shift","Unknown and old fields preserved after design save");
        check(Preferences.Load(path).Hotkeys[GlobalAction.Capture]==new Hotkey(6,0x51),"Old custom hotkey preserved");
        File.WriteAllText(path,"{\"design\":\"ember\"}");check(Preferences.Load(path).Design==DesignPreference.Ember,"Explicit ember loads");
        File.WriteAllText(path,"{\"design\":\"neon\"}");check(Preferences.Load(path).Design==DesignPreference.Classic,"Unknown design normalizes to Classic");
        File.WriteAllText(path,"{\"design\":42}");check(Preferences.Load(path).Design==DesignPreference.Classic,"Non-string design normalizes to Classic");
        File.WriteAllText(path,"bad-json");check(Preferences.Load(path).Design==DesignPreference.Classic,"Malformed file defaults Classic");

        string valid=Path.Combine(folder,"valid.json");File.WriteAllText(valid,"{\"design\":\"classic\",\"theme\":\"dark\"}");
        using var c=new Coordinator(showTray:false,registerHotkey:false,preferences:Preferences.Load(valid)){PreferencesPath=valid};
        using(var guard=new FileStream(valid,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
        {
            bool applied=true;bool closed=ClosePromptDuring(()=> { applied=c.ChangeDesign(DesignPreference.Ember); });
            check(closed,"Save failure reports through the existing information prompt");
            check(!applied && c.Preferences.Design==DesignPreference.Classic && Appearance.Design==DesignPreference.Classic,"Save failure rolls back design and does not apply it");
        }
        check(Preferences.Load(valid).Design==DesignPreference.Classic,"Save failure leaves persisted design unchanged");
        check(c.ChangeDesign(DesignPreference.Ember) && c.Preferences.Design==DesignPreference.Ember && Preferences.Load(valid).Design==DesignPreference.Ember && Appearance.Design==DesignPreference.Ember,"Successful design change persists and applies");
        check(c.ChangeDesign(DesignPreference.Ember),"Selecting the same design is a successful no-op");
        check(c.ChangeDesign(DesignPreference.Classic) && Preferences.Load(valid).Design==DesignPreference.Classic,"Switch back to Classic persists");
    }

    private static void Tokens(Action<bool,string> check)
    {
        var previousReader=Appearance.ReadSystem;var preference=Appearance.Preference;var design=Appearance.Design;
        try
        {
            Appearance.ReadSystem=()=>UiTheme.Dark;Appearance.Choose(ThemePreference.System);Appearance.ChooseDesign(DesignPreference.Classic);
            var dark=Appearance.Palette;
            check(dark.Surface.Color==Color.FromRgb(32,39,51) && dark.Text.Color==Color.FromRgb(243,245,248) && dark.AccentText.Color==Color.FromRgb(166,181,255),"Classic dark palette matches 0.2.3");
            check(UtilityUi.Accent.Color==ClassicBlue && UiDesign.OnAccent.Color==Colors.White,"Classic accent and on-accent");
            Appearance.ReadSystem=()=>UiTheme.Light;Appearance.Choose(ThemePreference.System);
            var light=Appearance.Palette;
            check(light.Surface.Color==Colors.White && light.Text.Color==Color.FromRgb(30,41,59) && light.Border.Color==Color.FromRgb(203,213,225) && light.AccentText.Color==ClassicBlue,"Classic light palette matches 0.2.3");
            Appearance.ChooseDesign(DesignPreference.Ember);
            check(UtilityUi.Accent.Color==EmberSignal && UiDesign.OnAccent.Color==ConstructDesign.Ink.Color,"Ember accent is TASK-1 Signal");
            var emberLight=Appearance.Palette;
            check(emberLight.Surface.Color==Color.FromRgb(241,242,236) && emberLight.Text.Color==Color.FromRgb(26,26,26),"Ember light palette preserved");
            Appearance.ReadSystem=()=>UiTheme.Dark;Appearance.Choose(ThemePreference.System);
            check(Appearance.Palette.Surface.Color==Color.FromRgb(35,35,35),"Ember dark palette preserved");
            Appearance.Choose(ThemePreference.Light);Appearance.ChooseDesign(DesignPreference.Classic);
            check(Appearance.Current==UiTheme.Light && Appearance.Palette.AccentText.Color==ClassicBlue,"Explicit theme remains independent of design");
        }
        finally { Appearance.ReadSystem=previousReader;Appearance.ChooseDesign(design);Appearance.Choose(preference); }
    }

    private static void PainterDesigns(Action<bool,string> check)
    {
        const int width=360,height=240;
        var glyphs=new Glyph[]
        {
            new BadgeGlyph(Guid.NewGuid(),"A1",new P(50,50)),
            new ArrowGlyph(Guid.NewGuid(),new P(30,110),new P(330,110)),
            new BoxGlyph(Guid.NewGuid(),new Bounds(30,150,120,60)),
            new PencilGlyph(Guid.NewGuid(),new P[]{new(190,200),new(250,180),new(330,200)})
        };
        var lightBackground=Fill(width,height,251,254,248);var darkBackground=Fill(width,height,26,26,26);
        var design=Appearance.Design;var theme=Appearance.Current;
        try
        {
            Appearance.ChooseDesign(DesignPreference.Classic);Appearance.Select(UiTheme.Light);
            var classic=Painter.Render(lightBackground,glyphs);var classicDark=Painter.Render(darkBackground,glyphs);
            var pixels=Pixels(classic);
            check(HasNear(pixels,width,180,110,2,ClassicBlue) && HasNear(pixels,width,90,150,2,ClassicBlue) && HasNear(pixels,width,250,180,2,ClassicBlue),"Classic arrow/rectangle/pencil use 0.2.3 blue");
            check(!HasNear(pixels,width,180,110,2,EmberSignal) && !HasColor(pixels,EmberSignal),"Classic export has no Signal Red");
            check(HasLightHalo(Pixels(classicDark),width,180,110),"Classic strokes keep the white halo on dark");
            check(Pixels(classic).SequenceEqual(Pixels(Painter.Render(lightBackground,glyphs))),"Classic export deterministic");
            Save(classic,"painter-classic-light");Save(classicDark,"painter-classic-dark");
            Appearance.ChooseDesign(DesignPreference.Ember);
            var ember=Painter.Render(lightBackground,glyphs);var emberDark=Painter.Render(darkBackground,glyphs);
            var emberPixels=Pixels(ember);
            check(HasNear(emberPixels,width,180,110,2,EmberSignal) && HasNear(emberPixels,width,90,150,2,EmberSignal) && HasNear(emberPixels,width,250,180,2,EmberSignal),"Ember arrow/rectangle/pencil keep Signal Red");
            check(HasPaperHalo(Pixels(emberDark),width,180,110),"Ember strokes keep Paper halo on dark");
            check(!HasColor(emberPixels,ClassicBlue),"Ember export has no legacy blue");
            Save(ember,"painter-ember-light");Save(emberDark,"painter-ember-dark");
        }
        finally { Appearance.ChooseDesign(design);Appearance.Select(theme); }
    }

    private static async Task SettingsDesigns(Action<bool,string> check,string folder)
    {
        string path=Path.Combine(folder,"settings-ui.json");File.WriteAllText(path,"{\"design\":\"classic\",\"theme\":\"dark\",\"language\":\"en\",\"future\":7}");
        using var owner=new Coordinator(showTray:false,registerHotkey:false,preferences:Preferences.Load(path)){PreferencesPath=path};
        foreach(string lang in new[]{"en","ru"}) foreach(var selected in new[]{ThemePreference.Dark,ThemePreference.Light})
        {
            L.Select(lang);Appearance.Choose(selected);Appearance.ChooseDesign(DesignPreference.Classic);owner.Preferences.Design=DesignPreference.Classic;owner.Preferences.Language=lang;owner.Preferences.Theme=selected;
            var window=new SettingsWindow(owner);window.Show();
            try
            {
                window.SelectPage(0);await Dispatcher.Yield(DispatcherPriority.Render);
                var root=(DependencyObject)window.Content;var combos=Descendants<ComboBox>(root).ToArray();
                check(combos.Length==3,"General has theme, design and language lists "+lang+selected);
                var designCombo=combos.Single(combo=>AutomationProperties.GetName(combo)==L.T("Design"));
                check(designCombo.Items.Count==2 && designCombo.Items.Cast<ComboBoxItem>().Select(i=>i.Content).SequenceEqual(new object[]{L.T("Classic"),"Ember"}),"Design list offers Classic/Ember "+lang);
                check(combos.Single(combo=>AutomationProperties.GetName(combo)==L.T("Appearance")).Items.Count==3 && combos.Single(combo=>AutomationProperties.GetName(combo)==L.T("Language")).Items.Count==2,"Theme/language lists remain independent "+lang+selected);
                check(window.Width==780,"Classic Settings uses 0.2.3 width "+lang+selected);
                SaveWindow(window,"settings-classic-"+lang+"-"+selected.ToString().ToLowerInvariant());
            }
            finally { window.Close(); }
        }

        // Fake confirmation seam: cancel, no-op, accept and save failure.
        owner.Preferences.Design=DesignPreference.Classic;Appearance.ChooseDesign(DesignPreference.Classic);
        var settings=new SettingsWindow(owner);settings.Show();
        try
        {
            settings.SelectPage(0);await Dispatcher.Yield(DispatcherPriority.Render);
            int calls=0;settings.ConfirmDesign=(w,d)=> { calls++;return false; };
            SelectDesign(settings,DesignPreference.Ember);
            check(calls==1 && owner.Preferences.Design==DesignPreference.Classic && Appearance.Design==DesignPreference.Classic,"Cancel keeps the old design");
            check(Preferences.Load(path).Design==DesignPreference.Classic,"Cancel does not persist a design change");
            SelectDesign(settings,DesignPreference.Classic);
            check(calls==1,"Selecting the current design does not ask for confirmation");
            settings.ConfirmDesign=(w,d)=> { calls++;return true; };
            SelectDesign(settings,DesignPreference.Ember);
            check(calls==2 && owner.Preferences.Design==DesignPreference.Ember && Appearance.Design==DesignPreference.Ember,"Confirmed design applies");
            check(Preferences.Load(path).Design==DesignPreference.Ember && settings.Width==880,"Confirmed design persists and rebuilds Ember Settings");
            check(JsonField(path,"future")==7,"Design change preserves unknown settings fields");
            CheckSettingsFonts(check,owner,settings,"Ember");
            SaveWindow(settings,"settings-ember-after-confirm");
            // The live window must switch back to the classic system font, not retain Commissioner.
            SelectDesign(settings,DesignPreference.Classic);
            check(settings.Width==780 && settings.FontFamily.Source==SystemFonts.MessageFontFamily.Source,"Classic restores the system base font in the open Settings window");
            CheckSettingsFonts(check,owner,settings,"Classic");
            // Return to Ember for the save-failure scenario.
            SelectDesign(settings,DesignPreference.Ember);
            CheckSettingsFonts(check,owner,settings,"Ember again");
            // Save failure through the real coordinator path.
            using(var guard=new FileStream(path,FileMode.Open,FileAccess.ReadWrite,FileShare.None))
            {
                bool closed=ClosePromptDuring(()=>SelectDesign(settings,DesignPreference.Classic));
                check(closed,"Settings save failure reports through the existing information prompt");
                check(owner.Preferences.Design==DesignPreference.Ember && Appearance.Design==DesignPreference.Ember,"Save failure keeps applied Ember design");
            }
            check(Preferences.Load(path).Design==DesignPreference.Ember,"Save failure leaves persisted Ember design");
            check(Descendants<TextBlock>((DependencyObject)settings.Content).Any(t=>t.Text==L.T("Settings could not be saved. Please retry.")),"Save failure surfaces a Settings error");
        }
        finally { settings.Close(); }

        // Actual modal confirmation through the production delegate, closed by its safe default.
        owner.Preferences.Design=DesignPreference.Ember;Appearance.ChooseDesign(DesignPreference.Ember);
        check(owner.ChangeDesign(DesignPreference.Classic) && Preferences.Load(path).Design==DesignPreference.Classic,"Live confirmation starts from a persisted Classic baseline");
        var live=new SettingsWindow(owner);live.Show();
        try
        {
            live.SelectPage(0);await Dispatcher.Yield(DispatcherPriority.Render);
            bool promptSeen=false,cancelDefault=false;
            var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
            timer.Tick+=(_,_)=> {
                var prompt=Application.Current.Windows.OfType<PromptWindow>().FirstOrDefault(w=>w.IsVisible);
                if(prompt==null) return;
                promptSeen=true;cancelDefault=prompt.CancelButton.IsDefault && prompt.CancelButton.IsCancel && !prompt.AcceptButton.IsDefault;
                timer.Stop();prompt.Complete(false);
            };
            timer.Start();
            SelectDesign(live,DesignPreference.Ember);
            timer.Stop();
            check(promptSeen && cancelDefault,"Real design confirmation is modal with Cancel as safe default");
            check(owner.Preferences.Design==DesignPreference.Classic && Preferences.Load(path).Design==DesignPreference.Classic,"Real confirmation Cancel keeps design");
        }
        finally { live.Close(); }
    }

    private static void CheckSettingsFonts(Action<bool,string> check,Coordinator owner,SettingsWindow window,string state)
    {
        string expected=ConstructDesign.Body.Source;
        var root=(DependencyObject)window.Content;
        var label=Descendants<TextBlock>(root).FirstOrDefault(t=>t.Text==L.T("Start with Windows"));
        var combo=Descendants<ComboBox>(root).FirstOrDefault(c=>AutomationProperties.GetName(c)==L.T("Design"));
        check(window.FontFamily.Source==expected,"Settings base font follows "+state);
        check(label!=null && label.FontFamily.Source==expected,"Settings TextBlock inherits "+state);
        check(combo!=null && combo.FontFamily.Source==expected,"Settings ComboBox inherits "+state);
        // A freshly constructed window in the same design is the reference; a stale live window must not match its old design.
        var fresh=new SettingsWindow(owner);
        try
        {
            var freshRoot=(DependencyObject)fresh.Content;
            var freshLabel=Descendants<TextBlock>(freshRoot).FirstOrDefault(t=>t.Text==L.T("Start with Windows"));
            var freshCombo=Descendants<ComboBox>(freshRoot).FirstOrDefault(c=>AutomationProperties.GetName(c)==L.T("Design"));
            check(fresh.FontFamily.Source==expected && freshLabel!=null && freshLabel.FontFamily.Source==expected && freshCombo!=null && freshCombo.FontFamily.Source==expected,"Fresh Settings uses the current design font "+state);
            check(window.FontFamily.Source==fresh.FontFamily.Source && label!=null && freshLabel!=null && label.FontFamily.Source==freshLabel.FontFamily.Source && combo!=null && freshCombo!=null && combo.FontFamily.Source==freshCombo.FontFamily.Source,"Live Settings font equals fresh Settings "+state);
        }
        finally { fresh.Close(); }
    }
    private static void SelectDesign(SettingsWindow window,DesignPreference target)
    {
        var combo=Descendants<ComboBox>((DependencyObject)window.Content).Single(c=>AutomationProperties.GetName(c)==L.T("Design"));
        combo.SelectedItem=combo.Items.Cast<ComboBoxItem>().Single(item=>Equals(item.Tag,target));
    }

    private static void OverlayDesigns(Action<bool,string> check)
    {
        var previousDesign=Appearance.Design;var previousTheme=Appearance.Current;
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        foreach(var design in new[]{DesignPreference.Classic,DesignPreference.Ember}) foreach(var theme in new[]{UiTheme.Dark,UiTheme.Light})
        {
            Appearance.ChooseDesign(design);Appearance.Select(theme);
            var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Design selection",0,0,640,480,true,96),Fill(640,480,40,40,40)));
            try
            {
                overlay.Refresh();
                if(design==DesignPreference.Ember) check(overlay.CaptureChrome is { Visibility: Visibility.Visible },"Ember selection guide is visible before annotation");
                overlay.ChooseRegion(new(0,0,640,480));
                overlay.Model.CreateMarker(new(120,120),"synthetic comment");
                overlay.Model.AddArrow(new(180,300),new(420,340));overlay.Model.AddBox(new(300,180),new(460,260));
                overlay.Refresh();
                var root=(FrameworkElement)overlay.Content;
                Save(root,640,480,"overlay-"+design.ToString().ToLowerInvariant()+"-"+theme.ToString().ToLowerInvariant());
                var bar=(Border)Field(overlay,"bar")!;
                var done=overlay.ToolbarButtons.Single(b=>((ToolTip)b.ToolTip).Content.Equals(L.T("Done (Ctrl+Enter)")));
                if(design==DesignPreference.Classic)
                {
                    check(overlay.CaptureChrome==null,"Classic overlay has no CONSTRUCT capture bar");
                    check(Field(overlay,"doneDock")==null && Field(overlay,"editorLayer")==null && Field(overlay,"markerSurface")==null,"Classic overlay omits Ember-only chrome");
                    check(bar.Visibility==Visibility.Visible && bar.ActualWidth>100 && bar.ActualHeight>30,$"Classic toolbar is visible with real size {bar.Visibility} {bar.ActualWidth}x{bar.ActualHeight}");
                    check(Inside(done,bar),"Classic Done lives inside the toolbar");
                    check(bar.CornerRadius.TopLeft==10 && bar.Effect is System.Windows.Media.Effects.DropShadowEffect { BlurRadius:16 },"Classic toolbar keeps 0.2.3 surface");
                    check(overlay.FontFamily.Source==SystemFonts.MessageFontFamily.Source,"Classic overlay uses system type");
                }
                else
                {
                    check(overlay.CaptureChrome!=null && overlay.CaptureChrome.Visibility==Visibility.Collapsed,"Ember overlay keeps capture bar and hides it in annotation");
                    check(Field(overlay,"doneDock") is Border && Field(overlay,"editorLayer") is Canvas && Field(overlay,"markerSurface")!=null,"Ember overlay keeps split Done dock and marker surface");
                    check(Inside(done,(DependencyObject)Field(overlay,"doneDock")!),"Ember Done occupies the separate dock");
                    check(bar.CornerRadius.TopLeft==4,"Ember toolbar keeps CONSTRUCT surface");
                }
                overlay.BeginEdit(new(200,200));overlay.CommentInput.Text="Редактируемый комментарий";
                var editor=(Border)Field(overlay,"editorHost")!;
                check(editor.Visibility==Visibility.Visible,"Comment editor is visible in both designs");
                check(design==DesignPreference.Classic ? editor is not CommentEditorCard && editor.Child is StackPanel : editor is CommentEditorCard,"Comment editor chrome follows design");
                Save(root,640,480,"comment-"+design.ToString().ToLowerInvariant()+"-"+theme.ToString().ToLowerInvariant());
                overlay.CancelEdit();
            }
            finally { if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
        }
        Appearance.ChooseDesign(previousDesign);Appearance.Select(previousTheme);
    }

    private static void PromptAndToastDesigns(Action<bool,string> check)
    {
        var previousDesign=Appearance.Design;
        try
        {
            Appearance.ChooseDesign(DesignPreference.Classic);
            var classic=new PromptWindow("Switch design?","The interface will switch immediately and the choice will be saved. Your screenshot session is not affected.","Switch",modal:false);
            try
            {
                check(classic.Width==390 && classic.WindowStyle==WindowStyle.SingleBorderWindow && classic.Content is StackPanel,"Classic prompt keeps 0.2.3 standard window and plain panel");
                check(classic.CancelButton.IsDefault && classic.CancelButton.IsCancel && !classic.AcceptButton.IsDefault,"Classic prompt keeps safe Cancel default");
                classic.Complete(false);check(!classic.Accepted,"Classic prompt Cancel does not accept");
                Save((FrameworkElement)classic.Content,"prompt-classic");
            }
            finally { classic.Close(); }
            Appearance.ChooseDesign(DesignPreference.Ember);
            var ember=new PromptWindow("Switch design?","The interface will switch immediately and the choice will be saved. Your screenshot session is not affected.","Switch",modal:false);
            try
            {
                check(ember.Width==520 && ember.WindowStyle==WindowStyle.None && ember.Content is NoticeSurface,"Ember prompt keeps CONSTRUCT chrome");
                check(ember.CancelButton.IsDefault && ember.CancelButton.IsCancel && !ember.AcceptButton.IsDefault,"Ember prompt keeps safe Cancel default");
                ember.Complete(false);check(!ember.Accepted,"Ember prompt Cancel does not accept");
                Save((FrameworkElement)ember.Content,"prompt-ember");
            }
            finally { ember.Close(); }
            foreach(var design in new[]{DesignPreference.Classic,DesignPreference.Ember})
            {
                Appearance.ChooseDesign(design);var toast=new ToastWindow(ToastMessage.Added("A",1));
                try
                {
                    var surface=(Border)toast.Content;
                    check(design==DesignPreference.Classic ? surface.CornerRadius.TopLeft==15 && toast.Content is not NoticeSurface : surface.CornerRadius.TopLeft==4 && toast.Content is NoticeSurface,"Toast surface follows design");
                    Save(surface,"toast-"+design.ToString().ToLowerInvariant());
                }
                finally { toast.Close(); }
            }
        }
        finally { Appearance.ChooseDesign(previousDesign); }
    }

    // TASK-2 first-frame positioning for both designs, including the classic standard chrome.
    private static async Task FirstFrame(Action<bool,string> check)
    {
        var host=new Window{ShowInTaskbar=false,Width=1,Height=1,WindowStyle=WindowStyle.None,Background=Brushes.Transparent};
        host.Show();
        try
        {
            foreach(var design in new[]{DesignPreference.Classic,DesignPreference.Ember})
            {
                Appearance.ChooseDesign(design);
                var prompt=new PromptWindow("Switch design?","The interface will switch immediately and the choice will be saved. Your screenshot session is not affected.","Switch",modal:false);
                try
                {
                    prompt.Prepare(host);prompt.Show();
                    var work=ToastNative.Area(null,PasteInput.Foreground().Hwnd);
                    var rect=Native.WindowRect(new WindowInteropHelper(prompt).Handle);
                    var expected=PromptWindow.Centered(work,rect.Right-rect.Left,rect.Bottom-rect.Top);
                    check(rect.Left==expected.X && rect.Top==expected.Y && rect.Right-rect.Left==expected.Width && rect.Bottom-rect.Top==expected.Height,$"First visible frame is already centered {design} rect={rect.Left},{rect.Top},{rect.Right-rect.Left}x{rect.Bottom-rect.Top} expected={expected.X},{expected.Y},{expected.Width}x{expected.Height} chrome={SystemParameters.WindowNonClientFrameThickness}");
                }
                finally { prompt.Complete(false);prompt.Close(); }
                await Dispatcher.Yield(DispatcherPriority.Render);
            }
        }
        finally { host.Close(); }
    }

    private static bool ClosePromptDuring(Action action)
    {
        bool closed=false;
        var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};
        timer.Tick+=(_,_)=> {
            var prompt=Application.Current.Windows.OfType<PromptWindow>().FirstOrDefault(w=>w.IsVisible);
            if(prompt==null) return;
            closed=true;timer.Stop();prompt.Complete(false);
        };
        timer.Start();
        try { action(); } finally { timer.Stop(); }
        return closed;
    }
    private static object? Field(object instance,string name) => instance.GetType().GetField(name,System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic)?.GetValue(instance);

    private static bool Inside(DependencyObject child,DependencyObject parent)
    {
        for(DependencyObject? node=child;node!=null;node=LogicalTreeHelper.GetParent(node)) if(ReferenceEquals(node,parent)) return true;
        return false;
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        if(root is T value) yield return value;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>()) foreach(var descendant in Descendants<T>(child)) yield return descendant;
    }
    private static BitmapSource Fill(int width,int height,byte r,byte g,byte b)
    {
        var pixels=new byte[width*height*4];
        for(int i=0;i<pixels.Length;i+=4) { pixels[i]=b;pixels[i+1]=g;pixels[i+2]=r;pixels[i+3]=255; }
        var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();return image;
    }
    private static byte[] Pixels(BitmapSource image) { var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);return pixels; }
    private static Color Pixel(byte[] pixels,int width,int x,int y) { int i=(y*width+x)*4;return Color.FromRgb(pixels[i+2],pixels[i+1],pixels[i]); }
    private static bool Near(Color actual,Color expected) => Math.Abs(actual.R-expected.R)<=2 && Math.Abs(actual.G-expected.G)<=2 && Math.Abs(actual.B-expected.B)<=2;
    private static bool HasNear(byte[] pixels,int width,int x,int y,int radius,Color expected)
    {
        int height=pixels.Length/(width*4);
        for(int dy=-radius;dy<=radius;dy++) for(int dx=-radius;dx<=radius;dx++)
        {
            int px=x+dx,py=y+dy;if(px<0||py<0||px>=width||py>=height) continue;
            if(Near(Pixel(pixels,width,px,py),expected)) return true;
        }
        return false;
    }
    private static bool HasColor(byte[] pixels,Color expected) => Enumerable.Range(0,pixels.Length/4).Any(i=>Near(Color.FromRgb(pixels[i*4+2],pixels[i*4+1],pixels[i*4]),expected));
    private static bool HasPaperHalo(byte[] pixels,int width,int x,int y) => Enumerable.Range(-8,17).Select(dy=>y+dy).Where(py=>py>=0 && py<pixels.Length/(width*4)).Any(py=>Luma(Pixel(pixels,width,x,py))>.6);
    private static bool HasLightHalo(byte[] pixels,int width,int x,int y) => Enumerable.Range(-8,17).Select(dy=>y+dy).Where(py=>py>=0 && py<pixels.Length/(width*4)).Any(py=>Luma(Pixel(pixels,width,x,py))>.6 && !Near(Pixel(pixels,width,x,py),ClassicBlue));
    private static double Luma(Color color) => (.2126*color.R+.7152*color.G+.0722*color.B)/255;
    private static int JsonField(string path,string name) => System.Text.Json.JsonDocument.Parse(File.ReadAllText(path)).RootElement.GetProperty(name).GetInt32();
    private static void Save(BitmapSource image,string name)
    {
        string folder=Artifacts("design-selection");using var stream=File.Create(Path.Combine(folder,name+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
    }
    private static void Save(FrameworkElement root,string name)
    {
        if(root.ActualWidth<=0 || root.ActualHeight<=0) { root.Measure(new Size(double.PositiveInfinity,double.PositiveInfinity));root.Arrange(new Rect(0,0,root.DesiredSize.Width,root.DesiredSize.Height));root.UpdateLayout(); }
        Save(root,(int)Math.Ceiling(root.ActualWidth),(int)Math.Ceiling(root.ActualHeight),name);
    }
    private static void Save(FrameworkElement root,int width,int height,string name)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);Save(bitmap,name);
    }
    private static void SaveWindow(Window window,string name)
    {
        var content=(FrameworkElement)window.Content;content.UpdateLayout();Save(content,(int)Math.Ceiling(content.ActualWidth),(int)Math.Ceiling(content.ActualHeight),name);
    }
    private static string Artifacts(string child)
    {
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts",child));Directory.CreateDirectory(folder);return folder;
    }
}
