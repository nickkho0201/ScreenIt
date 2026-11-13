using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Reflection;
using ScreenIt.Core;

internal static class DesignChecks
{
    internal static void Run(Action<bool,string> check)
    {
        var design=Appearance.Design;Appearance.ChooseDesign(DesignPreference.Ember);
        const string sample="ЗАХВАТ ЭКРАНА / SCREEN CAPTURE Настройки приложения · Горячие клавиши · Аннотации ЁЖ ЙЦУКЕН ФЫВАПРОЛД 0123456789 Маркер A1 · Карандаш · Стрелка · Рамка Shift+Enter — новая строка · Ctrl+Enter — Готово Capture / Paste Session / Clear Session";
        foreach(var (family,weight,name) in new[]{(ConstructDesign.Body,FontWeights.Normal,"Commissioner"),(ConstructDesign.Body,FontWeights.SemiBold,"Commissioner"),(ConstructDesign.Display,FontWeights.Bold,"Unbounded")})
        {
            var face=new Typeface(family,FontStyles.Normal,weight,FontStretches.Normal);
            bool found=face.TryGetGlyphTypeface(out var glyph);
            check(found,"Bundled typeface resolves "+family.Source+weight);
            check(glyph.FontUri.ToString().Contains("ScreenIt.App;component",StringComparison.OrdinalIgnoreCase),"Font comes from app resource: "+glyph.FontUri);
            check(glyph.FamilyNames.Values.Any(n=>n.Contains(name,StringComparison.Ordinal)),"Expected font family "+name+weight);
            check(sample.Where(c=>!char.IsWhiteSpace(c)).All(c=>glyph.CharacterToGlyphMap.TryGetValue(c,out ushort index) && index!=0),"RU/EN regression strings have real glyphs "+name+weight);
        }
        double Luminance(SolidColorBrush brush)
        {
            double F(byte b) { double v=b/255d;return v<=.04045 ? v/12.92 : Math.Pow((v+.055)/1.055,2.4); }
            var c=brush.Color;return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);
        }
        double Contrast(SolidColorBrush a,SolidColorBrush b) { double x=Luminance(a),y=Luminance(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05); }
        check(Contrast(ConstructDesign.Ink,ConstructDesign.Signal)>=4.5,"Ink labels on Signal meet AA");
        foreach(var theme in new[]{UiTheme.Dark,UiTheme.Light})
        {
            Appearance.Select(theme);var p=Appearance.Palette;
            check(Contrast(p.Text,p.Surface)>=4.5 && Contrast(p.Muted,p.Surface)>=4.5,"Body and supporting text contrast "+theme);
            check(Contrast(ConstructDesign.Focus,p.Editor)>=3,"Focus boundary contrast "+theme);
        }
        check(Painter.AnnotationAccent.Color==Color.FromRgb(240,77,50) && Painter.AnnotationOutline.Color==Color.FromRgb(251,254,248) && Painter.AnnotationStrokeWidth==2.5 && Painter.AnnotationOutlineWidth==4.5,"Export annotation palette is Signal/Paper with unchanged widths");
        AnnotationPalette(check);
        Previews(check);
        Appearance.ChooseDesign(design);
    }
    internal static void PaletteOnly(Action<bool,string> check)
    {
        var design=Appearance.Design;Appearance.ChooseDesign(DesignPreference.Ember);
        try { AnnotationPalette(check); } finally { Appearance.ChooseDesign(design); }
    }
    internal static void ToolbarOnly(Action<bool,string> check)
    {
        string language=L.Language;var theme=Appearance.Preference;var design=Appearance.Design;Appearance.ChooseDesign(DesignPreference.Ember);
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        try
        {
            foreach(string lang in new[]{"ru","en"}) foreach(var selected in new[]{ThemePreference.Dark,ThemePreference.Light}) foreach(int width in new[]{640,960,1366,1920})
            {
                L.Select(lang);Appearance.Choose(selected);
                var image=BitmapSource.Create(width,600,96,96,PixelFormats.Bgr32,null,new byte[width*600*4],width*4);image.Freeze();
                var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Toolbar fixture",0,0,width,600,true,96),image));
                try
                {
                    overlay.ChooseRegion(new(0,0,width,600));
                    var root=(FrameworkElement)overlay.Content;
                    Save(root,width,600,"toolbar-top-"+width+"-"+lang+selected);
                    typeof(AnnotationOverlay).GetMethod("LayoutToolbar",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
                    Save(root,width,600,"toolbar-top-"+width+"-"+lang+selected);
                    var bar=(Border)typeof(AnnotationOverlay).GetField("bar",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    var done=(Border)typeof(AnnotationOverlay).GetField("doneDock",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(overlay)!;
                    var a=bar.TranslatePoint(new Point(),root);var b=done.TranslatePoint(new Point(),root);
                    check(Math.Abs(a.Y-b.Y)<.1 && b.Y==16 && b.X>=a.X+bar.ActualWidth+11.9,"Independent top blocks with gap "+width+lang+selected);
                    check(a.X>=0 && b.X+done.ActualWidth<=width,"Toolbar and Done fit viewport "+width+lang+selected);
                }
                finally { owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
            }
        }
        finally { L.Select(language);Appearance.Choose(theme);Appearance.ChooseDesign(design); }
    }
    private static void AnnotationPalette(Action<bool,string> check)
    {
        const int width=480,height=320;
        var signal=Color.FromRgb(240,77,50);
        var glyphs=new Glyph[]
        {
            new BadgeGlyph(Guid.NewGuid(),"A1",new P(60,60)),
            new ArrowGlyph(Guid.NewGuid(),new P(40,120),new P(440,120)),
            new BoxGlyph(Guid.NewGuid(),new Bounds(40,160,160,100)),
            new PencilGlyph(Guid.NewGuid(),new P[]{new(40,290),new(200,290),new(300,250),new(440,290)})
        };
        var lightBackground=Fill(width,height,251,254,248);
        var darkBackground=Fill(width,height,26,26,26);
        var previous=Appearance.Current;
        BitmapSource? lightSample=null,darkSample=null;
        try
        {
            foreach(var theme in new[]{UiTheme.Light,UiTheme.Dark})
            {
                Appearance.Select(theme);
                var light=Painter.Render(lightBackground,glyphs);var dark=Painter.Render(darkBackground,glyphs);
                var lightPixels=Pixels(light);var darkPixels=Pixels(dark);
                if(lightSample==null) { lightSample=light;darkSample=dark; }
                else check(lightPixels.SequenceEqual(Pixels(lightSample!)) && darkPixels.SequenceEqual(Pixels(darkSample!)),"Graphic annotation palette is theme-independent "+theme);
                check(Near(Pixel(lightPixels,width,240,120),signal) && Near(Pixel(darkPixels,width,240,120),signal),"Arrow core is Signal Red on light/dark "+theme);
                check(Near(Pixel(lightPixels,width,120,160),signal) && Near(Pixel(darkPixels,width,120,160),signal),"Rectangle core is Signal Red on light/dark "+theme);
                check(Near(Pixel(lightPixels,width,100,290),signal) && Near(Pixel(darkPixels,width,100,290),signal),"Pencil core is Signal Red on light/dark "+theme);
                check(HasHalo(darkPixels,width,240,120) && HasHalo(darkPixels,width,120,160) && HasHalo(darkPixels,width,100,290),"Paper halo keeps strokes legible on dark "+theme);
                check(!HasBlue(lightPixels) && !HasBlue(darkPixels),"No legacy blue annotation pixels remain "+theme);
            }
            string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/annotation-palette"));Directory.CreateDirectory(folder);
            Save(lightSample!,folder,"arrow-box-pencil-light");Save(darkSample!,folder,"arrow-box-pencil-dark");
        }
        finally { Appearance.Select(previous); }

        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Palette synthetic",0,0,width,height,true,96),Fill(width,height,26,26,26)));
        try
        {
            overlay.ChooseRegion(new(0,0,width,height));overlay.Switch(Tool.Arrow);
            overlay.BeginPointer(new(40,240));overlay.MovePointer(new(440,240));
            var live=overlay.VisibleGlyphs().OfType<ArrowGlyph>().Single();
            check(live.Start==new P(40,240) && live.End==new P(440,240),"Live arrow gesture keeps physical geometry");
            var transform=overlay.DeviceTransform;int sampleX=(int)Math.Round(240/transform.M11),sampleY=(int)Math.Round(240/transform.M22);
            var root=(FrameworkElement)overlay.Content;
            var gesture=Pixels(Render(root,width,height));
            check(HasNear(gesture,width,sampleX,sampleY,2,signal),"Gesture preview draws Signal Red arrow");
            overlay.EndPointer(new(440,240));
            var arrow=overlay.Model.Items.OfType<Arrow>().Single();
            check(arrow.Start==new P(40,240) && arrow.End==new P(440,240),"Pointer release commits unchanged arrow geometry");
            var draft=Pixels(Render(root,width,height));
            check(HasNear(draft,width,sampleX,sampleY,2,signal),"Committed draft preview draws the same Signal Red arrow");
            var export=Pixels(Painter.Render(overlay.Crop,Painter.Project(overlay.Model.Items)));
            check(HasNear(export,width,240,240,2,signal) && HasHalo(export,width,240,240),"Bitmap export uses the same Signal Red stroke and Paper halo");
        }
        finally { if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }

        // Synthetic fitted-window path: source larger than the monitor, no Windows.Graphics.Capture or desktop access.
        using var fittedOwner=new Coordinator(showTray:false,registerHotkey:false);
        var fitted=new AnnotationOverlay(fittedOwner,new Frame(new MonitorData("Palette fitted",0,0,width,height,true,96),Fill(width,height,26,26,26)));
        try
        {
            fitted.ChooseBitmap(Fill(960,640,26,26,26));
            check(fitted.SourceScale<1 && fitted.Model.Width==960 && fitted.Model.Height==640,"Fitted-window draft keeps full physical source");
            fitted.Switch(Tool.Arrow);fitted.BeginPointer(new(100,320));fitted.MovePointer(new(860,320));
            var fittedTransform=fitted.DeviceTransform;int fittedX=(int)Math.Round((fitted.Region.X+480*fitted.SourceScale)/fittedTransform.M11),fittedY=(int)Math.Round((fitted.Region.Y+320*fitted.SourceScale)/fittedTransform.M22);
            var fittedRoot=(FrameworkElement)fitted.Content;
            check(HasReddish(Pixels(Render(fittedRoot,width,height)),width,fittedX,fittedY,4),"Fitted-window gesture preview draws the Signal Red palette");
            fitted.EndPointer(new(860,320));
            var fittedArrow=fitted.Model.Items.OfType<Arrow>().Single();
            check(fittedArrow.Start==new P(100,320) && fittedArrow.End==new P(860,320),"Fitted-window gesture keeps full-source geometry");
            var fittedExport=Pixels(Painter.Render(fitted.Crop,Painter.Project(fitted.Model.Items)));
            check(HasNear(fittedExport,960,480,320,2,signal) && HasHalo(fittedExport,960,480,320),"Fitted-window bitmap keeps full resolution and the same palette");
        }
        finally { if(fitted.Model!=null) while(fitted.Model.CanUndo) fitted.Model.Undo();fittedOwner.Cancel();fitted.ClosingByApp=true;fitted.Close(); }
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
    private static double Luma(Color color) => (.2126*color.R+.7152*color.G+.0722*color.B)/255;
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
    private static bool HasHalo(byte[] pixels,int width,int x,int y)
    {
        var signal=Color.FromRgb(240,77,50);
        for(int dy=-8;dy<=8;dy++)
        {
            int py=y+dy;if(py<0||py>=pixels.Length/(width*4)) continue;
            var color=Pixel(pixels,width,x,py);
            if(!Near(color,signal) && Luma(color)>.6) return true;
        }
        return false;
    }
    private static bool HasReddish(byte[] pixels,int width,int x,int y,int radius)
    {
        int height=pixels.Length/(width*4);
        for(int dy=-radius;dy<=radius;dy++) for(int dx=-radius;dx<=radius;dx++)
        {
            int px=x+dx,py=y+dy;if(px<0||py<0||px>=width||py>=height) continue;
            var color=Pixel(pixels,width,px,py);
            if(color.R>80 && color.R>color.G+35 && color.R>color.B+35) return true;
        }
        return false;
    }
    private static bool HasBlue(byte[] pixels)
    {
        for(int i=0;i<pixels.Length;i+=4) if(pixels[i]>pixels[i+2]+40 && pixels[i]>pixels[i+1]+20) return true;
        return false;
    }
    private static BitmapSource Render(FrameworkElement root,int width,int height)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);bitmap.Freeze();return bitmap;
    }
    private static void Save(BitmapSource image,string folder,string name)
    {
        using var stream=File.Create(Path.Combine(folder,name+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));encoder.Save(stream);
    }
    private static void Save(FrameworkElement root,int width,int height,string name)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/construct"));Directory.CreateDirectory(folder);
        using var stream=File.Create(Path.Combine(folder,name+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));encoder.Save(stream);
    }
    private static void Previews(Action<bool,string> check)
    {
        string language=L.Language;var theme=Appearance.Preference;
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        try
        {
            foreach(string lang in new[]{"ru","en"}) foreach(var selected in new[]{ThemePreference.Dark,ThemePreference.Light})
            {
                owner.Preferences.Language=lang;owner.Preferences.Theme=selected;
                L.Select(lang);Appearance.Choose(selected);string id=lang+"-"+selected;
                var settings=new SettingsWindow(owner);
                try
                {
                    for(int page=0;page<3;page++)
                    {
                        settings.SelectPage(page);
                        Save((FrameworkElement)settings.Content,864,620,"settings-"+page+"-"+id);
                        Save((FrameworkElement)settings.Content,684,470,"settings-narrow-"+page+"-"+id);
                    }
                }
                finally { settings.Close(); }
                foreach(int width in new[]{640,1366})
                {
                    int height=width==640 ? 600 : 768;
                    var bytes=new byte[width*height*4];
                    for(int y=0;y<height;y++) for(int x=0;x<width;x++)
                    { int i=(y*width+x)*4;byte value=(byte)(((x/48+y/48)%2==0) ? 220 : 205);bytes[i]=bytes[i+1]=bytes[i+2]=value; }
                    var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgr32,null,bytes,width*4);image.Freeze();
                    var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Design fixture",0,0,width,height,true,96),image));
                    try
                    {
                        var captureRoot=(FrameworkElement)overlay.Content;
                        Save(captureRoot,width,height,"capture-region-"+width+"-"+id);
                        typeof(AnnotationOverlay).GetMethod("LayoutToolbar",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
                        Save(captureRoot,width,height,"capture-region-"+width+"-"+id);
                        var chrome=overlay.CaptureChrome!;
                        check(chrome.Visibility==Visibility.Visible && chrome.ModeButton.IsHitTestVisible && !chrome.Focusable,"Selection chrome preserves surface input "+id+width);
                        bool Under(DependencyObject? hit,DependencyObject parent)
                        {
                            for(var node=hit;node!=null;node=VisualTreeHelper.GetParent(node)) if(ReferenceEquals(node,parent)) return true;
                            return false;
                        }
                        DependencyObject? Hit(Point point)
                        {
                            DependencyObject? found=null;
                            VisualTreeHelper.HitTest(captureRoot,node=>node is UIElement { IsHitTestVisible:false } ? HitTestFilterBehavior.ContinueSkipSelfAndChildren : HitTestFilterBehavior.Continue,
                                result=> { found=result.VisualHit;return HitTestResultBehavior.Stop; },new PointHitTestParameters(point));
                            return found;
                        }
                        var togglePoint=chrome.ModeButton.TranslatePoint(new Point(chrome.ModeButton.ActualWidth/2,chrome.ModeButton.ActualHeight/2),captureRoot);
                        check(Under(Hit(togglePoint),chrome.ModeButton),"Pointer hits the real mode button "+id+width);
                        var passive=chrome.Sections[0];var passivePoint=passive.TranslatePoint(new Point(passive.ActualWidth/2,passive.ActualHeight/2),captureRoot);
                        check(Hit(passivePoint)!=null && !Under(Hit(passivePoint),chrome),"Passive capture guide passes pointer to selection surface "+id+width);
                        foreach(bool windowMode in new[]{false,true}) foreach(bool held in new[]{false,true})
                        {
                            chrome.Refresh(windowMode,AnnotationModifier.Shift,held);
                            Save(captureRoot,width,height,"capture-root-"+width+"-"+id);
                            foreach(var group in chrome.Sections)
                            {
                                var p=group.TranslatePoint(new Point(),captureRoot);
                                check(group.ActualWidth>0 && group.ActualHeight>0 && p.X>=0 && p.Y>=0 && p.X+group.ActualWidth<=width && p.Y+group.ActualHeight<=height,"Capture guide section fits "+id+width+windowMode+held);
                            }
                            check(chrome.WindowMode==windowMode && chrome.ModifierActive==held && overlay.ModifierHint=="Shift","Capture guide reflects mode and configured modifier "+id+width+windowMode+held);
                            Save(chrome,(int)Math.Ceiling(chrome.ActualWidth),(int)Math.Ceiling(chrome.ActualHeight),"capture-bar-"+width+"-"+id+"-"+windowMode+"-"+held);
                        }
                        overlay.Refresh();
                        chrome.ModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        check(owner.WindowMode && chrome.WindowMode && !overlay.RegionSelection.Active && owner.Session.Screenshots.Count==0,"Mode button switches to Window without selecting a screenshot "+id+width);
                        check(System.Windows.Automation.AutomationProperties.GetName(chrome.ModeButton)==L.T("Window")+" (W)","Mode button label follows Window "+id+width);
                        chrome.ModeButton.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
                        check(!owner.WindowMode && !chrome.WindowMode && System.Windows.Automation.AutomationProperties.GetName(chrome.ModeButton)==L.T("Region")+" (W)","Same mode button returns to Region "+id+width);
                        check(!overlay.IsVisible,"Mode button verification keeps synthetic window hidden "+id+width);
                        overlay.ChooseRegion(new(0,0,width,height));
                        check(chrome.Visibility==Visibility.Collapsed,"Selection guide is replaced by annotation tools "+id+width);
                        overlay.Model.CreateMarker(new(width*.45,height*.4),"Editable comment / Редактируемый комментарий");
                        overlay.Model.AddArrow(new(width*.2,height*.65),new(width*.4,height*.45));
                        overlay.Model.AddBox(new(width*.6,height*.35),new(width*.8,height*.6));
                        overlay.Model.AddPencil([new(width*.2,height*.35),new(width*.25,height*.3),new(width*.3,height*.4)]);
                        overlay.Refresh();var root=(FrameworkElement)overlay.Content;
                        Save(root,width,height,"annotation-"+width+"-"+id);
                        typeof(AnnotationOverlay).GetMethod("LayoutToolbar",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
                        Save(root,width,height,"annotation-"+width+"-"+id);
                        foreach(var button in overlay.ToolbarButtons.Where(b=>b.Visibility==Visibility.Visible))
                        {
                            var pos=button.TranslatePoint(new Point(),root);
                            check(pos.X>=0 && pos.Y>=0 && pos.X+button.ActualWidth<=width && pos.Y+button.ActualHeight<=height,"Redesigned action within synthetic monitor "+id+width+button.ToolTip);
                        }
                        check(overlay.ToolbarButtons.Single(b=>((ToolTip)b.ToolTip).Content.Equals(L.T("Done (Ctrl+Enter)"))).TranslatePoint(new Point(),root).Y<height/2,"Done occupies separate top dock "+id+width);
                        var marker=overlay.Model.Items.OfType<Marker>().Single();overlay.BeginEdit(marker.Anchor,marker.Id);
                        Save(root,width,height,"comment-"+width+"-"+id);overlay.CancelEdit();
                    }
                    finally { if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
                }
            }
        }
        finally { L.Select(language);Appearance.Choose(theme); }
    }
}
