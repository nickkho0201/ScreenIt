using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenIt.Core;

// Synthetic hidden overlay. No global input, desktop acquisition or clipboard writes.
internal static class MarkerEditorChecks
{
    private static void ReferencePreview(Action<bool,string> check,string language,ThemePreference theme)
    {
        L.Select(language);Appearance.Choose(theme);
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        const int width=1366,height=800;
        var pixels=new byte[width*height*4];
        for(int y=0;y<height;y++) for(int x=0;x<width;x++) { int i=(y*width+x)*4;byte shade=(byte)((theme==ThemePreference.Dark ? 44 : 215)+(x/64+y/64)%2*6);pixels[i]=pixels[i+1]=pixels[i+2]=shade; }
        var source=BitmapSource.Create(width,height,96,96,PixelFormats.Bgr32,null,pixels,width*4);source.Freeze();
        var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Comment composition",0,0,width,height,true,96),source));
        try
        {
            overlay.ChooseRegion(new(0,0,width,height));overlay.BeginEdit(new(300,190));
            var root=(FrameworkElement)overlay.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
            var input=overlay.CommentInput;input.ApplyTemplate();
            var frame=(Border)input.Template.FindName("InputFrame",input);
            check(input.Template.FindName("PART_ContentHost",input) is ScrollViewer,"Custom input keeps native text content host "+language+theme);
            var normal=(SolidColorBrush)frame.BorderBrush;
            var focusKey=(DependencyPropertyKey)typeof(UIElement).GetField("IsKeyboardFocusWithinPropertyKey",BindingFlags.Static|BindingFlags.NonPublic)!.GetValue(null)!;
            string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/marker-editor"));Directory.CreateDirectory(folder);
            foreach(bool focused in new[]{false,true})
            {
                input.SetValue(focusKey,focused);root.UpdateLayout();
                check(focused ? ((SolidColorBrush)frame.BorderBrush).Color==UtilityUi.Accent.Color : ((SolidColorBrush)frame.BorderBrush).Color==normal.Color,"Input normal/focused CONSTRUCT border "+language+theme+focused);
                var full=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);full.Render(root);
                var card=overlay.EditorBounds;var badge=overlay.MarkerUiBounds(new(300,190),"A1");
                var union=Rect.Union(new Rect(card.X,card.Y,card.Width,card.Height),badge);union.Inflate(24,24);
                var crop=new CroppedBitmap(full,new Int32Rect((int)union.X,(int)union.Y,(int)Math.Ceiling(union.Width),(int)Math.Ceiling(union.Height)));
                using var output=File.Create(Path.Combine(folder,"reference-"+language+theme+"-"+(focused ? "focused" : "normal")+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(crop));encoder.Save(output);
            }
            check(normal.Color!=UtilityUi.Accent.Color && input.FocusVisualStyle==null,"Focus is distinct and contains no system TextBox chrome "+language+theme);
            input.SetValue(focusKey,false);
            input.Text="Unicode / Русский\nSecond line";input.Select(0,7);input.SelectedText="Edited";
            check(input.Text.StartsWith("Edited",StringComparison.Ordinal) && input.AcceptsReturn && input.TextWrapping==TextWrapping.Wrap,"Selection replacement and multiline text services survive template "+language+theme);
            input.Undo();check(input.Text.StartsWith("Unicode",StringComparison.Ordinal),"Text input Undo survives template "+language+theme);
            input.Redo();check(input.Text.StartsWith("Edited",StringComparison.Ordinal),"Text input Redo survives template "+language+theme);
            overlay.CancelEdit();
        }
        finally { overlay.CancelEdit();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
    }
    private static void EdgeInteraction(Action<bool,string> check,bool fitted,bool tiny=false)
    {
        using var owner=new Coordinator(showTray:false,registerHotkey:false);
        BitmapSource Image(int w,int h) { var data=new byte[w*h*4];var result=BitmapSource.Create(w,h,96,96,PixelFormats.Bgr32,null,data,w*4);result.Freeze();return result; }
        var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("UI badge edges",0,0,640,600,true,96),Image(640,600)));
        try
        {
            if(fitted) overlay.ChooseBitmap(Image(1280,960));else overlay.ChooseRegion(tiny ? new(160,140,12,8) : new(160,140,360,300));
            bool outsideSeen=false;
            foreach(var anchor in new[]{new P(1,1),new P(overlay.Model.Width-2,1),new P(1,overlay.Model.Height-2),new P(overlay.Model.Width-2,overlay.Model.Height-2)})
            {
                var marker=overlay.Model.CreateMarker(anchor,"Edge comment");
                var face=overlay.MarkerUiBounds(anchor,marker.Label);var t=overlay.DeviceTransform;
                var click=new P(((face.Left+face.Width/2)*t.M11-overlay.Region.X)/overlay.SourceScale,((face.Top+face.Height/2)*t.M22-overlay.Region.Y)/overlay.SourceScale);
                outsideSeen|=click.X<0 || click.Y<0 || click.X>=overlay.Model.Width || click.Y>=overlay.Model.Height;
                overlay.BeginPointer(click);overlay.EndPointer(click);
                check(overlay.Editing && overlay.CommentInput.Text==marker.Comment && overlay.Model.Items.OfType<Marker>().Single().Anchor==anchor,"Badge face click outside crop does not move marker "+fitted+anchor);
                overlay.CancelEdit();
                var end=new P(click.X+20,click.Y+15);overlay.BeginPointer(click);overlay.MovePointer(end);overlay.EndPointer(end);
                var expected=Geo.Clamp(new P(anchor.X+20,anchor.Y+15),overlay.Model.Width,overlay.Model.Height);
                check(!overlay.Editing && overlay.Model.Items.OfType<Marker>().Single().Anchor.Distance(expected)<.001,"UI badge drag uses raw pointer delta and clamps final anchor "+fitted+anchor);
                overlay.Model.Delete(marker.Id);overlay.Refresh();
            }
            check(outsideSeen==tiny,tiny ? "Tiny source fixture exercises readable badge outside crop" : "Normal/fitted badge remains within visible source "+fitted);
        }
        finally { overlay.CancelEdit();if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
    }
    internal static async System.Threading.Tasks.Task Live(Action<bool,string> check)
    {
        var originalTheme=Appearance.Preference;string originalLanguage=L.Language;var originalDesign=Appearance.Design;Appearance.ChooseDesign(DesignPreference.Ember);
        try
        {
            L.Select("ru");Appearance.Choose(ThemePreference.Dark);
            using var owner=new Coordinator(showTray:false,registerHotkey:false);
            string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/floating-comment"));Directory.CreateDirectory(folder);
            foreach(var monitor in Native.Monitors())
            {
                var pixels=new byte[monitor.Width*monitor.Height*4];
                for(int i=0;i<pixels.Length;i+=4) pixels[i]=pixels[i+1]=pixels[i+2]=48;
                var image=BitmapSource.Create(monitor.Width,monitor.Height,96,96,PixelFormats.Bgr32,null,pixels,monitor.Width*4);image.Freeze();
                var overlay=new AnnotationOverlay(owner,new Frame(monitor,image));
                try
                {
                    overlay.Show();await overlay.Rendered.Task.WaitAsync(TimeSpan.FromSeconds(5));overlay.Placement();
                    overlay.ChooseRegion(new(100,100,Math.Min(1000,monitor.Width-140),Math.Min(700,monitor.Height-140)));
                    overlay.Model.CreateMarker(new(200,160),"Соседняя метка");
                    overlay.BeginEdit(new(280,220));
                    await System.Threading.Tasks.Task.Delay(220);
                    check(overlay.CommentInput.IsKeyboardFocused,"Live editor receives immediate real keyboard focus "+monitor.EffectiveDpi);
                    var badge=overlay.MarkerUiBounds(new(280,220),"A2");var t=overlay.DeviceTransform;
                    check(badge.Height>=22 && badge.Height<=28 && overlay.EditorBounds.Width/t.M11<=400,"Live compact badge/card at actual HWND DPI "+monitor.EffectiveDpi);
                    overlay.CommentInput.Text="Проверка ввода и переноса строки.\nКомментарий остаётся редактируемым текстом.";
                    await System.Threading.Tasks.Task.Delay(60);
                    Native.Flush();var actual=Native.Capture(monitor).Image;
                    var card=overlay.EditorBounds;var union=Rect.Union(new Rect(card.X,card.Y,card.Width,card.Height),new Rect(badge.X*t.M11,badge.Y*t.M22,badge.Width*t.M11,badge.Height*t.M22));union.Inflate(24*t.M11,24*t.M22);
                    int x=(int)Math.Max(0,union.Left),y=(int)Math.Max(0,union.Top),w=(int)Math.Min(monitor.Width-x,Math.Ceiling(union.Width)),h=(int)Math.Min(monitor.Height-y,Math.Ceiling(union.Height));
                    var crop=new CroppedBitmap(actual,new Int32Rect(x,y,w,h));
                    using(var stream=File.Create(Path.Combine(folder,"live-"+monitor.EffectiveDpi+".png"))) { var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(crop));png.Save(stream); }
                    overlay.CommentInput.Clear();await System.Threading.Tasks.Task.Delay(60);
                    Native.Flush();var empty=Native.Capture(monitor).Image;
                    using(var stream=File.Create(Path.Combine(folder,"live-empty-"+monitor.EffectiveDpi+".png"))) { var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(new CroppedBitmap(empty,new Int32Rect(x,y,w,h))));png.Save(stream); }
                    overlay.CommentInput.Text=string.Join("\n",Enumerable.Range(1,24).Select(n=>"Строка "+n+" — проверка прокрутки комментария"));
                    overlay.CommentInput.CaretIndex=overlay.CommentInput.Text.Length;overlay.CommentInput.ScrollToEnd();
                    await System.Threading.Tasks.Task.Delay(80);
                    check(overlay.CommentInput.VerticalOffset>0,"Live long comment scrolls correctly "+monitor.EffectiveDpi);
                    var host=(ScrollViewer)overlay.CommentInput.Template.FindName("PART_ContentHost",overlay.CommentInput);host.ApplyTemplate();
                    var scroll=(System.Windows.Controls.Primitives.ScrollBar)host.Template.FindName("PART_VerticalScrollBar",host);
                    check(scroll.Width==8 && scroll.Visibility==Visibility.Visible,"Live scrollbar uses compact themed presentation "+monitor.EffectiveDpi);
                    Native.Flush();var scrolled=Native.Capture(monitor).Image;
                    using(var stream=File.Create(Path.Combine(folder,"live-scroll-"+monitor.EffectiveDpi+".png"))) { var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(new CroppedBitmap(scrolled,new Int32Rect(x,y,w,h))));png.Save(stream); }
                    check(overlay.CommitComment() && !overlay.Editing,"Live Enter-equivalent saves without waiting for fade "+monitor.EffectiveDpi);
                    await System.Threading.Tasks.Task.Delay(150);
                    overlay.BeginEdit(new(280,220),overlay.Model.Items.OfType<Marker>().Last().Id);overlay.CancelEdit();
                    await System.Threading.Tasks.Task.Delay(140);
                    check(!overlay.Editing,"Live close/reopen/cancel completes "+monitor.EffectiveDpi);
                }
                finally { overlay.CancelEdit();while(overlay.Model!=null && overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
            }
        }
        finally { L.Select(originalLanguage);Appearance.Choose(originalTheme);Appearance.ChooseDesign(originalDesign); }
    }
    internal static void Run(Action<bool,string> check)
    {
        var originalTheme=Appearance.Preference;var originalLanguage=L.Language;var originalDesign=Appearance.Design;Appearance.ChooseDesign(DesignPreference.Ember);
        try
        {
            foreach(string language in new[]{"ru","en"}) foreach(var theme in new[]{ThemePreference.Dark,ThemePreference.Light})
            {
                L.Select(language);Appearance.Choose(theme);
                using var owner=new Coordinator(showTray:false,registerHotkey:false);
                var pixels=Enumerable.Repeat((byte)220,640*600*4).ToArray();
                var source=BitmapSource.Create(640,600,96,96,PixelFormats.Bgr32,null,pixels,640*4);source.Freeze();
                var overlay=new AnnotationOverlay(owner,new Frame(new MonitorData("Marker editor fixture",0,0,640,600,true,96),source));
                void Key(Key key)
                {
                    // Enter/Escape are routed through the actual overlay handler; modifiers
                    // are tested by the existing Core/production suite, not faked here.
                    overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,HwndSource.FromHwnd(owner.ControlHandle),0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
                }
                try
                {
                    overlay.ChooseRegion(new(0,0,640,600));
                    foreach(var anchor in new[]{new P(1,1),new P(638,1),new P(1,598),new P(638,598),new P(300,250)})
                    {
                        overlay.BeginEdit(anchor);var r=overlay.EditorBounds;
                        check(overlay.Editing && r.X>=0 && r.Y>=0 && r.X+r.Width<=640 && r.Y+r.Height<=600,"Comment card remains on monitor "+language+theme+anchor);
                        overlay.CancelEdit();check(!overlay.Editing && overlay.Model.NextMarker==1 && overlay.Model.Items.Count==0,"Cancel removes provisional editor without spending marker number "+language+theme+anchor);
                    }
                    overlay.BeginEdit(new(250,220));
                    check(!overlay.CommitComment() && overlay.Editing && overlay.Model.Items.Count==0,"Empty comment remains editable without orphan marker "+language+theme);
                    overlay.CommentInput.Text="Комментарий / comment\n第二行 🌍";Key(System.Windows.Input.Key.Enter);
                    var marker=overlay.Model.Items.OfType<Marker>().Single();
                    check(!overlay.Editing && marker.Label=="A1" && marker.Comment.Contains("🌍",StringComparison.Ordinal),"Enter stores editable Unicode and closes editor "+language+theme);
                    var uiFace=overlay.MarkerUiBounds(marker.Anchor,marker.Label);
                    var facePoint=new P(uiFace.Left+uiFace.Width/2,uiFace.Top+uiFace.Height/2);
                    check(uiFace.Width>=32 && uiFace.Width<70 && uiFace.Height>=22 && uiFace.Height<=28,"UI badge retains compact projected geometry "+language+theme);
                    overlay.BeginPointer(facePoint);overlay.EndPointer(facePoint);
                    check(overlay.Editing && overlay.CommentInput.Text==marker.Comment,"Clicking existing marker opens its saved comment "+language+theme);
                    Key(System.Windows.Input.Key.Escape);
                    overlay.BeginPointer(marker.Anchor);overlay.MovePointer(new(marker.Anchor.X+30,marker.Anchor.Y+20));overlay.EndPointer(new(marker.Anchor.X+30,marker.Anchor.Y+20));
                    check(!overlay.Editing && overlay.Model.Items.OfType<Marker>().Single().Anchor!=marker.Anchor,"Dragging marker moves it without opening editor "+language+theme);
                    overlay.History(false);marker=overlay.Model.Items.OfType<Marker>().Single();
                    var rendered=Painter.Render(overlay.Crop,Painter.Project(overlay.Model.Items));
                    byte[] Bytes(BitmapSource bitmap) { var data=new byte[bitmap.PixelWidth*bitmap.PixelHeight*4];bitmap.CopyPixels(data,bitmap.PixelWidth*4,0);return data; }
                    byte[] before=Bytes(rendered);
                    check(Enumerable.Range(0,before.Length/4).Any(i=>before[i*4+2]>before[i*4+1]+50),"Exported badge has new Signal contour "+language+theme);
                    overlay.BeginEdit(marker.Anchor,marker.Id);overlay.CommentInput.Text="Uncommitted replacement";
                    check(before.SequenceEqual(Bytes(Painter.Render(overlay.Crop,Painter.Project(overlay.Model.Items)))),"Editor/link/draft text never enters export "+language+theme);
                    Key(System.Windows.Input.Key.Escape);
                    check(!overlay.Editing && overlay.Model.Items.OfType<Marker>().Single().Comment==marker.Comment,"Escape preserves saved marker/comment "+language+theme);
                    overlay.BeginEdit(marker.Anchor,marker.Id);overlay.CommentInput.Text="Changed body only";Key(System.Windows.Input.Key.Enter);
                    check(before.SequenceEqual(Bytes(Painter.Render(overlay.Crop,Painter.Project(overlay.Model.Items)))) && overlay.Model.Items.Single().Id==marker.Id,"Editing body preserves bitmap/marker identity "+language+theme);
                    overlay.BeginEdit(new(400,300));overlay.CancelEdit();overlay.BeginEdit(new(410,320));
                    overlay.CommentInput.Text="Reopened";Key(System.Windows.Input.Key.Enter);
                    check(!overlay.Editing && overlay.Model.Items.OfType<Marker>().Last().Label=="A2","Rapid reopen retains correct marker number and text "+language+theme);
                    overlay.History(false);check(overlay.Model.Items.Count==1,"Undo removes whole new marker "+language+theme);
                    overlay.History(true);check(overlay.Model.Items.Count==2,"Redo restores marker and comment "+language+theme);
                    var folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/marker-editor"));Directory.CreateDirectory(folder);
                    using var output=File.Create(Path.Combine(folder,"marker-output-"+language+theme+".png"));var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(rendered));encoder.Save(output);
                }
                finally { overlay.CancelEdit();while(overlay.Model!=null && overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
            }
            foreach(string lang in new[]{"ru","en"}) foreach(var theme in new[]{ThemePreference.Dark,ThemePreference.Light}) ReferencePreview(check,lang,theme);
            foreach(var physical in new[]{new Size(1366,768),new Size(1920,1080),new Size(2560,1440)}) foreach(double scale in new[]{1d,1.25,1.5,2d})
            {
                var viewport=new Size(physical.Width/scale,physical.Height/scale);
                foreach(var anchor in new[]{new Point(1,1),new Point(viewport.Width-1,1),new Point(1,viewport.Height-1),new Point(viewport.Width-1,viewport.Height-1),new Point(viewport.Width/2,viewport.Height/2)})
                {
                    var badge=MarkerBadgeUi.Bounds(anchor,"AA1000",viewport);var card=CommentEditorPlacement.Place(badge,viewport);
                    check(card.X>=0 && card.Y>=0 && card.Right<=viewport.Width && card.Bottom<=viewport.Height,"Card clamps to simulated viewport/DPI "+physical+scale+anchor);
                    check(!card.IntersectsWith(badge),"Card avoids UI badge in supported simulated viewport "+physical+scale+anchor);
                }
            }
            EdgeInteraction(check,false);EdgeInteraction(check,true);EdgeInteraction(check,false,true);
            foreach(var size in new[]{new Size(2,2),new Size(24,16),new Size(200,120),new Size(1200,600)}) foreach(double dpi in new[]{1d,1.25,1.5,2d}) foreach(double fit in new[]{1d,.45}) foreach(string label in new[]{"A1","A10","AA1","AA1000"})
            {
                var original=Geo.Badge(new P(size.Width/2,size.Height/2),label,(int)size.Width,(int)size.Height);
                var sourceRect=new Rect(80,80,size.Width*fit/dpi,size.Height*fit/dpi);
                var projected=new Rect(80+original.X*fit/dpi,80+original.Y*fit/dpi,original.Width*fit/dpi,original.Height*fit/dpi);
                var badge=MarkerBadgeUi.Bounds(projected,label,sourceRect,new Size(1600,900));
                check(badge.Height>=22 && badge.Height<=28 && badge.Width<110,"Compact/readable badge across source/fitted/DPI/ID "+size+dpi+fit+label);
                if(sourceRect.Width<badge.Width || sourceRect.Height<badge.Height) check(!badge.IntersectsWith(sourceRect),"Tiny fragment is not covered by UI badge "+size+dpi+fit+label);
            }
            foreach(string label in new[]{"A1","AA1","AA1000"}) foreach(int size in new[]{2,32,80})
            {
                var bytes=new byte[size*size*4];var source=BitmapSource.Create(size,size,96,96,PixelFormats.Bgr32,null,bytes,size*4);source.Freeze();
                var result=Painter.Render(source,[new BadgeGlyph(Guid.NewGuid(),label,new P(0,0))]);
                check(result.IsFrozen && result.PixelWidth==size && result.PixelHeight==size,"Chamfered badge handles edge/long ID/tiny source "+label+size);
            }
        }
        finally { L.Select(originalLanguage);Appearance.Choose(originalTheme);Appearance.ChooseDesign(originalDesign); }
    }
}
