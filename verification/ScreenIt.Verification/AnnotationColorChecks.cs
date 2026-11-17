using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ScreenIt.Core;

// Isolated annotation-color checks: synthetic frames/overlays only, no desktop capture,
// no clipboard, no global hotkeys and no user preferences file.
internal static class AnnotationColorChecks
{
    private static readonly AnnotationColor Yellow=new(245,197,66), Cyan=new(46,196,198), Blue=new(61,123,255);
    private static readonly AnnotationColor Ink=new(26,26,26), Paper=new(251,254,248), White=new(255,255,255);
    internal static async Task Run(Action<bool,string> check)
    {
        string language=L.Language;var theme=Appearance.Preference;var design=Appearance.Design;
        string folder=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/annotation-color"));Directory.CreateDirectory(folder);
        try
        {
            CoreMath(check);
            await PopupAndSync(check,folder);
            DefaultsAndExport(check,folder);
            IsolationAndLifecycle(check);
            RuntimeLifetime(check);
        }
        finally { L.Select(language);Appearance.Choose(theme);Appearance.ChooseDesign(design); }
    }
    private static void CoreMath(Action<bool,string> check)
    {
        check(AnnotationColor.FromHsv(0,1,1)==new AnnotationColor(255,0,0) && AnnotationColor.FromHsv(60,1,1)==new AnnotationColor(255,255,0) && AnnotationColor.FromHsv(120,1,1)==new AnnotationColor(0,255,0),"HSV primaries: red/yellow/green");
        check(AnnotationColor.FromHsv(180,1,1)==new AnnotationColor(0,255,255) && AnnotationColor.FromHsv(240,1,1)==new AnnotationColor(0,0,255) && AnnotationColor.FromHsv(300,1,1)==new AnnotationColor(255,0,255),"HSV primaries: cyan/blue/magenta");
        check(AnnotationColor.FromHsv(123,0,0)==new AnnotationColor(0,0,0) && AnnotationColor.FromHsv(123,0,1)==new AnnotationColor(255,255,255),"HSV black and white are achromatic");
        check(AnnotationColor.FromHsv(double.NaN,double.PositiveInfinity,2)==White,"Non-finite HSV input is clamped safely");
        check(AnnotationColor.FromRgb(-10,300,128)==new AnnotationColor(0,255,128),"RGB byte clamping 0/255");
        var gray=new AnnotationColor(128,128,128).ToHsv();
        check(gray.Saturation==0 && Math.Abs(gray.Value-128/255d)<.0001,"Neutral gray has zero saturation");
        int worst=0;
        for(int r=0;r<=255;r+=17) for(int g=0;g<=255;g+=17) for(int b=0;b<=255;b+=17)
        {
            var color=new AnnotationColor((byte)r,(byte)g,(byte)b);var round=AnnotationColor.FromHsv(color.ToHsv());
            worst=Math.Max(worst,Math.Max(Math.Abs(round.R-r),Math.Max(Math.Abs(round.G-g),Math.Abs(round.B-b))));
        }
        check(worst<=1,"RGB->HSV->RGB roundtrip stays within one byte: "+worst);
        check(new AnnotationColor(240,77,50).Hex=="#F04D32" && new AnnotationColor(0,0,0).Hex=="#000000","Hex formatting follows R/G/B bytes");
    }
    private static async Task PopupAndSync(Action<bool,string> check,string folder)
    {
        string language=L.Language;var theme=Appearance.Preference;var design=Appearance.Design;
        using var owner=Owner();
        try
        {
            foreach(var selectedDesign in new[]{DesignPreference.Classic,DesignPreference.Ember})
                foreach(var selectedTheme in new[]{UiTheme.Dark,UiTheme.Light})
                    foreach(string lang in new[]{"en","ru"})
                    {
                        Appearance.ChooseDesign(selectedDesign);Appearance.Select(selectedTheme);L.Select(lang);
                        foreach(int width in new[]{1366,640,440})
                        {
                            int height=width==1366 ? 768 : 600;
                            owner.ChooseAnnotationColor(null);
                            var overlay=Overlay(owner,width,height);
                            try
                            {
                                overlay.ChooseRegion(new(0,0,width,height));overlay.ApplyTheme();Arrange(overlay,width,height);
                                var root=(FrameworkElement)overlay.Content;
                                var bar=(Border)Field(overlay,"bar")!;
                                var done=Field(overlay,"doneDock") as Border;
                                var colorButton=overlay.ColorButton;
                                check(overlay.ToolbarButtons.Contains(colorButton) && AutomationProperties.GetName(colorButton)==L.T("Annotation color (C)"),"Palette button is part of the toolbar with localized name "+lang+selectedDesign+selectedTheme);
                                int index=overlay.ToolbarButtons.ToList().IndexOf(colorButton);
                                int undoIndex=overlay.ToolbarButtons.ToList().IndexOf((Button)Field(overlay,"undoButton")!);
                                check(index>0 && index<undoIndex && overlay.ToolbarButtons.Take(index).All(b=>b!=colorButton),"Palette button sits between the tools and Undo");
                                var p=colorButton.TranslatePoint(new Point(0,0),root);
                                check(p.X>=0 && p.Y>=0 && p.X+colorButton.ActualWidth<=width && p.Y+colorButton.ActualHeight<=height,"Palette button fits viewport "+width+lang+selectedDesign);
                                check(bar.Visibility==Visibility.Visible && (done==null || done.Visibility==Visibility.Collapsed || done.TranslatePoint(new Point(),root).X>=p.X),"Existing Done placement is preserved "+lang+selectedDesign);

                                overlay.ToggleColorPicker();
                                AnnotationColorPicker picker=overlay.ColorPicker ?? throw new InvalidOperationException("Color popup did not open.");
                                check(overlay.ColorPickerOpen && picker!=null,"Palette button opens the color popup "+width+lang+selectedDesign);
                                root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
                                typeof(AnnotationOverlay).GetMethod("LayoutColorPicker",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
                                root.UpdateLayout();
                                var pickerPos=picker!.TranslatePoint(new Point(0,0),root);
                                check(picker.ActualWidth>=Math.Min(320,width-24) && picker.ActualWidth<=420.01 && picker.ActualWidth<=width-24,"Popup width adapts within 320-420 DIP "+width);
                                check(pickerPos.X>=0 && pickerPos.X+picker.ActualWidth<=width && pickerPos.Y>=0 && pickerPos.Y+picker.ActualHeight<=height,"Popup fits viewport "+width+lang+selectedDesign);
                                check(pickerPos.Y>=bar.TranslatePoint(new Point(),root).Y+bar.ActualHeight-1 || pickerPos.Y<=bar.TranslatePoint(new Point(),root).Y,"Popup is anchored below the toolbar or clamped inside the viewport "+width+" pickerY="+pickerPos.Y+" barTop="+bar.TranslatePoint(new Point(),root).Y+" barH="+bar.ActualHeight+" pickerH="+picker.ActualHeight);
                                var buttonCenter=p.X+colorButton.ActualWidth/2;
                                check(Math.Abs(pickerPos.X+picker.NotchOffset-buttonCenter)<=1 || pickerPos.X<=8.01 || pickerPos.X+picker.ActualWidth>=width-8.01,"Notch points at the palette button "+width);
                                var texts=Descendants<TextBlock>(picker).Select(t=>t.Text).ToArray();
                                check(texts.Contains(L.T("Annotation color")) && texts.Contains(L.T("Presets")) && texts.Contains(L.T("Custom color")),"Popup headings are localized "+lang+" "+selectedDesign);
                                check(picker.Swatches.Count==8,"Popup shows eight presets");
                                foreach(var (presetIndex,preset) in AnnotationColorPicker.Presets.Select((v,i)=>(i,v)))
                                    check(AutomationProperties.GetName(picker.Swatches[presetIndex])==L.T(preset.Name),"Preset accessible name "+preset.Name+" "+lang);
                                check(picker.RedInput.Text==owner.EffectiveAnnotationColor.R.ToString(CultureInfo.InvariantCulture) && picker.GreenInput.Text==owner.EffectiveAnnotationColor.G.ToString(CultureInfo.InvariantCulture) && picker.BlueInput.Text==owner.EffectiveAnnotationColor.B.ToString(CultureInfo.InvariantCulture),"RGB fields start from the working color");
                                check(picker.Color==owner.EffectiveAnnotationColor && owner.AnnotationColor==null,"Untouched popup shows the design default without an explicit choice");
                                Save(root,width,height,Path.Combine(folder,"picker-"+selectedDesign+"-"+selectedTheme+"-"+lang+"-"+width+".png"));
                                if(width==1366) Save(picker,(int)Math.Ceiling(picker.ActualWidth),(int)Math.Ceiling(picker.ActualHeight),Path.Combine(folder,"popup-"+selectedDesign+"-"+selectedTheme+"-"+lang+".png"));

                                // Preset click applies immediately and dismisses; custom edits stay a local candidate.
                                var bluePreset=AnnotationColorPicker.Presets[4];
                                picker.Swatches[4].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==bluePreset.Color && !overlay.ColorPickerOpen,"Preset click applies the color and closes the popup");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                check(((Border)picker.Swatches[4].Content).BorderThickness.Left==2 && picker.Swatches.Count(b=>((Border)b.Content).BorderThickness.Left==2)==1,"Exactly one preset shows the selected contrast frame");
                                picker.Swatches[0].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==AnnotationColorPicker.Presets[0].Color,"Signal preset applies its exact value");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[1].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==Yellow,"Yellow preset applies its exact value");

                                // Custom RGB/SV/hue only edit the candidate; the working color is untouched.
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.RedInput.Text="12";picker.GreenInput.Text="34";picker.BlueInput.Text="56";
                                check(picker.Color==new AnnotationColor(12,34,56) && owner.AnnotationColor==Yellow,"RGB input edits only the candidate color");
                                overlay.CloseColorPicker();
                                check(owner.AnnotationColor==Yellow,"Escape/cancel keeps the working color");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                check(picker.Color==Yellow,"Reopening starts again from the applied working color");
                                Press(overlay,Key.C,owner);
                                check(overlay.ColorPickerOpen && owner.AnnotationColor==Yellow,"C inside the open picker is plain input, not a toggle");
                                picker.RedInput.Text="999";
                                check(picker.RedInput.Text=="255" && picker.Color.R==255 && owner.AnnotationColor==Yellow,"Typing above 255 clamps to 255 without truncation and stays a candidate");
                                picker.RedInput.Text="0000000000000000000000123";
                                check(picker.Color.R==123,"Leading-zero numeric input keeps its numeric value");
                                picker.RedInput.Text="999999999999999999999999";
                                check(picker.RedInput.Text=="255" && picker.Color.R==255,"Very long numeric/overflow input clamps safely to 255");
                                picker.GreenInput.Text="";picker.BlueInput.Text="abc";
                                check(owner.AnnotationColor==Yellow,"Empty/non-numeric channels do not apply a color");
                                picker.GreenInput.Text="0";picker.BlueInput.Text="255";
                                check(picker.Color==new AnnotationColor(255,0,255) && owner.AnnotationColor==Yellow,"Clamped 255/0 channels stay candidate-only");
                                picker.RedInput.Text="0";picker.GreenInput.Text="0";picker.BlueInput.Text="0";
                                check(picker.Color==new AnnotationColor(0,0,0) && picker.Plane.Value==0,"RGB 0 edits brightness in the candidate");
                                picker.RedInput.Text="255";picker.GreenInput.Text="255";picker.BlueInput.Text="255";
                                check(picker.Color==White && picker.Plane.Saturation==0 && picker.Plane.Value==1,"RGB 255 yields a white candidate");

                                // Each channel clamps independently even when a neighboring field is empty/invalid.
                                picker.RedInput.Text="10";picker.GreenInput.Text="20";picker.BlueInput.Text="30";
                                var candidateBeforeMatrix=picker.Color;
                                picker.RedInput.Text="";
                                picker.GreenInput.Text="999";
                                check(picker.GreenInput.Text=="255" && picker.Color==candidateBeforeMatrix && owner.AnnotationColor==Yellow,"Green overflow clamps with an empty Red and the candidate is unchanged");
                                picker.GreenInput.Text="100";
                                check(picker.Color==candidateBeforeMatrix,"A partially valid RGB keeps the previous candidate");
                                picker.RedInput.Text="999";
                                check(picker.RedInput.Text=="255" && picker.Color==new AnnotationColor(255,100,30),"Red overflow clamps and applies only once every channel is valid");
                                picker.BlueInput.Text="999";
                                check(picker.BlueInput.Text=="255" && picker.Color==new AnnotationColor(255,100,255),"Blue overflow clamps when the other channels are valid");
                                picker.BlueInput.Text="";
                                picker.RedInput.Text="999";
                                check(picker.RedInput.Text=="255" && picker.Color==new AnnotationColor(255,100,255),"Overflow still clamps when another channel becomes invalid");
                                picker.BlueInput.Text="200";
                                check(picker.Color==new AnnotationColor(255,100,200),"Completing the invalid channel returns the clamped candidate");
                                picker.ApplyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==new AnnotationColor(255,100,200) && !overlay.ColorPickerOpen,"Apply preview commits the fully clamped candidate");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);

                                // The square preview is the only custom-color confirmation and the 14th tab stop.
                                var applyButton=picker.ApplyButton;
                                check(AutomationProperties.GetName(applyButton)==L.T("Apply color") && applyButton.Focusable && applyButton.Cursor==Cursors.Hand,"Apply preview is an accessible focusable button");
                                check(applyButton.Content is Border sample && sample.Width>=40 && sample.Height>=40,"Apply preview keeps the square color sample");
                                check(picker.FocusOrder.Count==14 && ReferenceEquals(picker.FocusOrder[^1],applyButton) && picker.NextFocus(picker.BlueInput,false)==applyButton && picker.NextFocus(applyButton,false)==picker.Swatches[0],"Preview joins the cyclic Tab order as the fourteenth control");
                                check(applyButton is Button { Focusable:true, IsTabStop:true } && new System.Windows.Automation.Peers.ButtonAutomationPeer(applyButton).GetPattern(System.Windows.Automation.Peers.PatternInterface.Invoke) is System.Windows.Automation.Provider.IInvokeProvider,"Apply preview is a native keyboard-activatable Button with the UIA Invoke pattern");
                                applyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==new AnnotationColor(255,100,200) && !overlay.ColorPickerOpen,"Pointer activation of the preview applies the clamped candidate and closes the popup");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.RedInput.Text="10";picker.GreenInput.Text="20";picker.BlueInput.Text="30";
                                picker.ApplyButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==new AnnotationColor(10,20,30) && !overlay.ColorPickerOpen,"Pointer activation of the preview applies the candidate");
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[2].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                check(owner.AnnotationColor==AnnotationColorPicker.Presets[2].Color,"Preset still applies immediately after a candidate was applied");

                                // Actual WPF content alignment for every design/theme.
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                foreach(var box in new[]{picker.RedInput,picker.GreenInput,picker.BlueInput})
                                {
                                    var glyph=box.GetRectFromCharacterIndex(0,false);
                                    check(box.VerticalContentAlignment==VerticalAlignment.Center && glyph.Top>=4 && glyph.Bottom<=box.ActualHeight-4,"RGB digits are vertically centered in the TextBox");
                                }


                                // Saturated color, then keyboard and pointer synchronization.
                                overlay.CloseColorPicker();overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[5].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                var workingBeforeEdits=owner.AnnotationColor;
                                double hueBefore=picker.Color.ToHsv().Hue;
                                // Documented step is 2 degrees, 10 with Shift; derive it from the ambient
                                // modifier state instead of assuming no modifier is physically held.
                                double hueStep=(Keyboard.Modifiers & ModifierKeys.Shift)!=0 ? 10 : 2;
                                Press(picker.Hue,Key.Right,owner);
                                double hueAfter=picker.Color.ToHsv().Hue;
                                check(Math.Abs(((hueAfter-hueBefore+360)%360)-hueStep)<1 && picker.RedInput.Text==picker.Color.R.ToString(CultureInfo.InvariantCulture) && owner.AnnotationColor==workingBeforeEdits,"Hue keyboard edits only the candidate and RGB step="+hueStep+" hb="+hueBefore+" ha="+hueAfter+" text="+picker.RedInput.Text+" R="+picker.Color.R+" owner="+owner.AnnotationColor+" working="+workingBeforeEdits);
                                double satBefore=picker.Plane.Saturation;
                                Press(picker.Plane,Key.Left,owner);
                                check(picker.Plane.Saturation<satBefore && picker.Color==AnnotationColor.FromHsv(picker.Hue.Hue,picker.Plane.Saturation,picker.Plane.Value) && owner.AnnotationColor==workingBeforeEdits,"SV keyboard edits the candidate consistently");
                                picker.Hue.SetFromPointForTest(new Point(picker.Hue.ActualWidth/2,4));
                                check(Math.Abs(picker.Hue.Hue-180)<2 && picker.Color.ToHsv().Hue is >170 and <190,"Hue pointer maps the middle of the strip to cyan");
                                picker.Plane.SetFromPointForTest(new Point(0,0));
                                check(picker.Color==White,"SV top-left corner is white");
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,0));
                                var topRight=picker.Color.ToHsv();
                                check(topRight.Saturation==1 && topRight.Value==1,"SV top-right corner is the pure hue");
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,picker.Plane.ActualHeight));
                                check(picker.Color==new AnnotationColor(0,0,0),"SV bottom edge is black");

                                // Regression (Sol finding): achromatic RGB must not reset the editing hue/saturation.
                                overlay.CloseColorPicker();overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[7].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Hue.SetFromPointForTest(new Point(picker.Hue.ActualWidth*240/360,4));
                                check(picker.Color.R<30 && picker.Color.G<30 && picker.Color.B<30 && Math.Abs(picker.Hue.Hue-240)<2,"Hue edit on black keeps black and the hue thumb");
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,0));
                                var fromBlack=picker.Color.ToHsv();
                                check(fromBlack.Hue is >235 and <245 && picker.Color.B>200 && picker.Color.R<80,"Raising SV after a hue edit on black yields blue without a hue jump");

                                overlay.CloseColorPicker();overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[6].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Hue.SetFromPointForTest(new Point(picker.Hue.ActualWidth*240/360,4));
                                check(picker.Color==White && Math.Abs(picker.Hue.Hue-240)<2,"Hue edit on white keeps white and the hue thumb");
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,0));
                                check(picker.Color.ToHsv().Hue is >235 and <245 && picker.Color.B>200,"Raising SV after a hue edit on white yields blue");

                                overlay.CloseColorPicker();overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                picker.Swatches[4].RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
                                overlay.ToggleColorPicker();picker=overlay.ColorPicker!;Settle(overlay,width,height);
                                double saturatedHue=picker.Hue.Hue;
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,picker.Plane.ActualHeight));
                                check(picker.Color==new AnnotationColor(0,0,0) && Math.Abs(picker.Hue.Hue-saturatedHue)<1 && picker.Plane.Saturation>0.9,"SV bottom keeps the editing hue and saturation");
                                picker.Plane.SetFromPointForTest(new Point(picker.Plane.ActualWidth,picker.Plane.ActualHeight/2));
                                check(Math.Abs(picker.Hue.Hue-saturatedHue)<1 && picker.Plane.Saturation>0.9 && picker.Color.B>100,"Raising brightness restores the same hue and saturation");

                                var workingBeforeRapid=owner.AnnotationColor;
                                for(int i=0;i<250;i++) picker.SetColor(AnnotationColor.FromHsv(i*7.3,(i%10)/9d,((i*3)%11)/10d));
                                var last=AnnotationColor.FromHsv(249*7.3,(249%10)/9d,((249*3)%11)/10d);
                                check(picker.Color==last && picker.RedInput.Text==last.R.ToString(CultureInfo.InvariantCulture) && ((SolidColorBrush)picker.PreviewSwatch.Background).Color==Color.FromRgb(last.R,last.G,last.B) && owner.AnnotationColor==workingBeforeRapid,"Rapid candidate changes stay synchronized and unapplied");
                                overlay.CloseColorPicker();
                                check(!overlay.ColorPickerOpen && picker.HasActiveMotion==false && Mouse.Captured==null,"Closing the popup clears state, motion and mouse capture");
                            }
                            finally { if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
                        }
                    }
            await Task.CompletedTask;
        }
        finally { L.Select(language);Appearance.Choose(theme);Appearance.ChooseDesign(design); }
    }
    private static void DefaultsAndExport(Action<bool,string> check,string folder)
    {
        using var owner=Owner();
        foreach(var selectedDesign in new[]{DesignPreference.Classic,DesignPreference.Ember})
        {
            Appearance.ChooseDesign(selectedDesign);Appearance.Select(UiTheme.Dark);owner.ChooseAnnotationColor(null);
            var overlay=Overlay(owner,800,600);
            try
            {
                overlay.ChooseRegion(new(0,0,800,600));overlay.ApplyTheme();Arrange(overlay,800,600);
                overlay.Switch(Tool.Arrow);overlay.BeginPointer(new(40,120));overlay.MovePointer(new(440,120));
                check(overlay.VisibleGlyphs().OfType<ArrowGlyph>().Last().Color==null,"Gesture preview keeps the design default when no color was chosen "+selectedDesign);
                overlay.EndPointer(new(440,120));
                overlay.Switch(Tool.Rectangle);overlay.BeginPointer(new(40,180));overlay.EndPointer(new(240,300));
                overlay.Switch(Tool.Pencil);overlay.BeginPointer(new(40,360));overlay.MovePointer(new(200,360));overlay.EndPointer(new(300,420));
                overlay.Switch(Tool.Marker);overlay.BeginPointer(new(600,120));overlay.CommentInput.Text="default marker";overlay.CommitComment();
                check(overlay.Model.Items.Count==4 && overlay.Model.Items.All(a=>a.Color==null),"No explicit choice preserves every tool's design default");
                var defaultColor=Painter.DefaultAnnotationColor;
                var glyphs=Painter.Project(overlay.Model.Items);
                var explicitGlyphs=glyphs.Select(g=>g switch { BadgeGlyph b=>b with { Color=defaultColor },ArrowGlyph a=>a with { Color=defaultColor },BoxGlyph x=>x with { Color=defaultColor },PencilGlyph p=>p with { Color=defaultColor },_=>g }).ToArray();
                var image=Fill(800,600,251,254,248);
                check(Pixels(Painter.Render(image,glyphs)).SequenceEqual(Pixels(Painter.Render(image,explicitGlyphs))),"Explicit design-default color renders byte-identically to no choice "+selectedDesign);

                // Colored marker: geometry/label/body invariants and readability are unchanged.
                var colored=Yellow;
                owner.ChooseAnnotationColor(colored);
                int markerCount=overlay.Model.Items.OfType<Marker>().Count();
                overlay.BeginPointer(new(600,300));overlay.CommentInput.Text="secret body";overlay.CommitComment();
                var marker=overlay.Model.Items.OfType<Marker>().Last();
                check(marker.Color==colored && marker.Label=="A"+(markerCount+1),"Colored marker stores its color and unchanged label");
                var badge=Geo.Badge(marker.Anchor,marker.Label,800,600);
                check(new P(badge.X+badge.Width/2,badge.Y+badge.Height/2)==marker.Anchor,"Colored marker keeps exact physical badge geometry");
                var first=Pixels(Painter.Render(image,Painter.Project(overlay.Model.Items)));
                overlay.Model.Edit(marker.Id,"different body text");
                var second=Pixels(Painter.Render(image,Painter.Project(overlay.Model.Items)));
                check(first.SequenceEqual(second),"Comment body never reaches the bitmap, including colored markers");

                // New objects take the chosen color; existing objects remain untouched by later choices.
                overlay.Switch(Tool.Arrow);overlay.BeginPointer(new(60,500));overlay.MovePointer(new(460,500));
                check(overlay.VisibleGlyphs().OfType<ArrowGlyph>().Last().Color==colored,"Arrow gesture fixes the chosen color");
                owner.ChooseAnnotationColor(Cyan);
                check(overlay.VisibleGlyphs().OfType<ArrowGlyph>().Last().Color==colored,"Changing the working color mid-gesture does not alter the captured gesture color");
                overlay.EndPointer(new(460,500));
                overlay.Switch(Tool.Rectangle);overlay.BeginPointer(new(520,420));overlay.EndPointer(new(720,560));
                overlay.Switch(Tool.Pencil);overlay.BeginPointer(new(60,560));overlay.MovePointer(new(260,560));overlay.EndPointer(new(260,580));
                overlay.Switch(Tool.Marker);overlay.BeginPointer(new(300,300));overlay.CommentInput.Text="cyan";overlay.CommitComment();
                check(overlay.Model.Items.OfType<Arrow>().Last().Color==colored && overlay.Model.Items.OfType<Box>().Last().Color==Cyan && overlay.Model.Items.OfType<PencilStroke>().Last().Color==Cyan && overlay.Model.Items.OfType<Marker>().Last().Color==Cyan,"All four tools store their own creation color");
                check(overlay.Model.Items.OfType<Arrow>().First().Color==null && overlay.Model.Items.OfType<Marker>().First().Color==null,"Older objects keep their original/default color");
                int count=overlay.Model.Items.Count;
                overlay.History(false);overlay.History(false);overlay.History(false);overlay.History(false);
                check(overlay.Model.Items.Count==count-4 && overlay.Model.Items.OfType<Arrow>().All(a=>a.Color==null),"Undo removes colored objects while older annotations keep no explicit color");
                overlay.History(true);overlay.History(true);overlay.History(true);overlay.History(true);
                check(overlay.Model.Items.OfType<Marker>().Last().Color==Cyan && overlay.Model.Items.OfType<Arrow>().Last().Color==colored && overlay.Model.Items.OfType<Box>().Last().Color==Cyan && overlay.Model.Items.OfType<PencilStroke>().Last().Color==Cyan,"Redo restores every colored annotation unchanged");

                // One Draw path: export pixels carry the exact chosen colors.
                var export=Pixels(Painter.Render(image,Painter.Project(overlay.Model.Items)));
                check(HasNear(export,800,260,500,2,colored),"Export draws the yellow arrow with its own color");
                check(HasNear(export,800,620,420,2,Cyan),"Export draws the cyan rectangle with its own color");
                int accentX=(int)(badge.X+badge.Width/2),accentY=selectedDesign==DesignPreference.Ember ? (int)badge.Y+1 : (int)badge.Y+5;
                var markerRegion=BadgeRegion(export,800,marker,badge);
                check(markerRegion.Any(p=>Accentish(p,colored)),"Export carries the colored marker accent "+selectedDesign+" at "+accentX+","+accentY);
                if(selectedDesign==DesignPreference.Ember) check(markerRegion.Any(p=>Near(p,Paper)),"Ember colored marker keeps readable Paper numbers");
                else check(Painter.Contrast(ColorToColor(Painter.MarkerInk(colored)),colored)>=4.5 && markerRegion.Any(p=>Near(p,Ink)),"Classic colored marker keeps readable dark numbers on yellow");
                check(!Painter.IsLight(Cyan) && Painter.IsLight(colored) && Painter.IsLight(White),"Halo choice follows stroke lightness");
                check(Painter.Contrast(ColorToColor(Painter.MarkerInk(Blue)),Blue)>=4.5 && Painter.Contrast(ColorToColor(Painter.MarkerInk(White)),White)>=4.5,"Marker label contrast stays at least 4.5:1 for dark and white fills");

                var dark=Fill(800,600,26,26,26);
                Save(Painter.Render(dark,Painter.Project(overlay.Model.Items)),800,600,Path.Combine(folder,"raster-"+selectedDesign+"-Dark.png"));
                Save(Painter.Render(image,Painter.Project(overlay.Model.Items)),800,600,Path.Combine(folder,"raster-"+selectedDesign+"-Light.png"));
            }
            finally { if(overlay.Model!=null) while(overlay.Model.CanUndo) overlay.Model.Undo();owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }
        }
        check(true,"Defaults/export checks completed for both designs");
    }
    private static void IsolationAndLifecycle(Action<bool,string> check)
    {
        using var owner=Owner();
        Appearance.ChooseDesign(DesignPreference.Ember);Appearance.Select(UiTheme.Dark);owner.ChooseAnnotationColor(null);
        var overlay=Overlay(owner,800,600);
        try
        {
            overlay.ChooseRegion(new(0,0,800,600));overlay.ApplyTheme();Arrange(overlay,800,600);
            int windows=owner.OverlayCount;var tool=overlay.Tool;
            check(((ToolTip)overlay.ColorButton.ToolTip).Content.Equals(L.T("Annotation color (C)")) && AutomationProperties.GetName(overlay.ColorButton)==L.T("Annotation color (C)"),"Palette tooltip and automation name expose C");
            Press(overlay,Key.C,owner);
            check(overlay.ColorPickerOpen,"C opens the picker in editable annotation mode");
            Press(overlay,Key.C,owner);
            check(overlay.ColorPickerOpen,"C inside the open picker is not a toggle");
            foreach(var key in new[]{Key.M,Key.A,Key.R,Key.P,Key.E,Key.Delete,Key.Enter,Key.Space,Key.Tab}) Press(overlay,key,owner);
            check(overlay.Tool==tool && overlay.Model.Items.Count==0 && !overlay.Editing && owner.Session.Screenshots.Count==0 && owner.OverlayCount==windows,"Popup keyboard never switches tools, deletes, undoes or commits");
            check(overlay.ColorPickerOpen,"Popup survives unrelated keys");
            // Keyboard containment: Tab must stay inside the popup, never reach toolbar/Done.
            var nav=overlay.ColorPicker!;
            check(KeyboardNavigation.GetTabNavigation(nav)==KeyboardNavigationMode.Cycle && KeyboardNavigation.GetControlTabNavigation(nav)==KeyboardNavigationMode.Cycle && KeyboardNavigation.GetDirectionalNavigation(nav)==KeyboardNavigationMode.Contained,"Popup contains Tab/CtrlTab/directional navigation");
            var focusOrder=nav.FocusOrder;
            check(focusOrder.Count==14 && focusOrder[0] is Button && focusOrder[^1]==nav.ApplyButton && focusOrder.Contains(nav.Plane) && focusOrder.Contains(nav.Hue) && focusOrder.Contains(nav.RedInput),"Popup focus order covers presets, SV, hue, RGB and the apply preview");
            check(nav.NextFocus(nav.BlueInput,false)==nav.ApplyButton && nav.NextFocus(nav.ApplyButton,false)==focusOrder[0] && nav.NextFocus(focusOrder[0],true)==nav.ApplyButton && nav.NextFocus(focusOrder[2],false)==focusOrder[3],"Tab and Shift+Tab cycle inside the popup including the preview");
            check(focusOrder.All(e=>nav.IsAncestorOf(e)),"Every focus target belongs to the popup, not the toolbar or Done");
            var tabArgs=new KeyEventArgs(Keyboard.PrimaryDevice,HwndSource.FromHwnd(owner.ControlHandle),0,Key.Tab) { RoutedEvent=Keyboard.PreviewKeyDownEvent };
            nav.RaiseEvent(tabArgs);
            check(tabArgs.Handled,"Popup intercepts Tab traversal through the real input handler");
            Press(overlay,Key.Enter,owner);Press(overlay,Key.Space,owner);
            check(overlay.Tool==tool && overlay.Model.Items.Count==0 && owner.Session.Screenshots.Count==0 && owner.OverlayCount==windows && overlay.ToolbarButtons.All(b=>!b.IsKeyboardFocused),"Enter/Space with the popup open do not focus or activate toolbar/Done");
            var guarded=overlay.Model.CreateMarker(new(400,400),"guard");
            typeof(AnnotationOverlay).GetProperty("Selected")!.GetSetMethod(true)!.Invoke(overlay,[guarded.Id]);
            overlay.Refresh();
            Press(overlay,Key.Delete,owner);
            check(overlay.Model.Items.Contains(guarded) && !overlay.Editing,"Delete with the popup open cannot remove the selected annotation");
            overlay.Model.Delete(guarded.Id);
            Press(overlay,Key.Escape,owner);
            check(!overlay.ColorPickerOpen && overlay.Editable && owner.OverlayCount==windows && overlay.Model.Items.Count==0,"Escape closes only the popup and keeps the capture");
            Press(overlay,Key.Escape,owner);
            check(owner.OverlayCount==0 && owner.Session.Screenshots.Count==0,"Second Escape follows the existing capture-cancel hierarchy");
        }
        finally { owner.Cancel();overlay.ClosingByApp=true;overlay.Close(); }

        var second=Overlay(owner,800,600);
        try
        {
            second.ChooseRegion(new(0,0,800,600));Arrange(second,800,600);
            second.ToggleColorPicker();
            Settle(second,800,600);
            var root=(FrameworkElement)second.Content;
            var picker=second.ColorPicker!;
            var inside=picker.Swatches[0].TranslatePoint(new Point(picker.Swatches[0].ActualWidth/2,picker.Swatches[0].ActualHeight/2),root);
            check(!second.DismissColorPickerOutside(inside) && second.ColorPickerOpen,"Input inside the popup does not dismiss it: hit="+second.ColorPickerHitTest(inside)+" at "+inside);
            check(second.DismissColorPickerOutside(new Point(20,580)) && !second.ColorPickerOpen,"Outside click dismisses without creating a glyph");
            check(second.Model.Items.Count==0 && !second.Editing && owner.Session.Screenshots.Count==0,"Outside dismissal adds no annotations");
            second.ToggleColorPicker();second.ColorButton.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            check(!second.ColorPickerOpen,"Palette button toggles the popup closed");
            second.ToggleColorPicker();
            Settle(second,800,600);
            second.InterruptGesture();
            check(!second.ColorPickerOpen,"Gesture interruption/lost capture closes the popup");
            second.ToggleColorPicker();
            Settle(second,800,600);
            var pickerOpen=second.ColorPicker!;
            pickerOpen.SetColor(Blue);
            UiMotion.Reveal(pickerOpen,pickerOpen.MotionOffset,false);
            check(!pickerOpen.HasActiveMotion && pickerOpen.Opacity==1 && pickerOpen.MotionOffset.Y==0,"Reduced-motion reveal keeps the final static state without clocks");
            // A theme switch with the popup open must refresh small themed labels, not only surfaces.
            Appearance.Select(UiTheme.Light);second.ApplyTheme();
            check(pickerOpen.ChannelLabels.Count==3 && pickerOpen.ChannelLabels.All(t=>ReferenceEquals(t.Foreground,UtilityUi.Muted)) && ReferenceEquals(second.ColorChevron!.Stroke,UtilityUi.Muted),"Theme switch refreshes popup channel labels and palette chevron");
            Appearance.Select(UiTheme.Dark);second.ApplyTheme();
            for(int i=0;i<30;i++) { second.CloseColorPicker();second.ToggleColorPicker(); }
            second.CloseColorPicker();
            check(!second.ColorPickerOpen && Mouse.Captured==null,"Rapid open/close leaves no popup, capture or card behind");
            second.BeginEdit(new(300,300));
            check(!second.ColorPickerOpen && second.Editing,"Editor path remains intact without the popup");
            var commentArgs=new KeyEventArgs(Keyboard.PrimaryDevice,HwndSource.FromHwnd(owner.ControlHandle),0,Key.C) { RoutedEvent=Keyboard.PreviewKeyDownEvent };
            second.RaiseEvent(commentArgs);
            check(second.Editing && !second.ColorPickerOpen && !commentArgs.Handled,"C in the comment editor stays plain text input");
            second.CancelEdit();
        }
        finally { if(second.Model!=null) while(second.Model.CanUndo) second.Model.Undo();owner.Cancel();second.ClosingByApp=true;second.Close(); }
    }
    private static void RuntimeLifetime(Action<bool,string> check)
    {
        using var owner=Owner();
        Appearance.ChooseDesign(DesignPreference.Classic);
        string settings=owner.PreferencesPath=Path.Combine(Path.GetTempPath(),"ScreenItVerification",Guid.NewGuid().ToString("N"),"settings.json");
        owner.ChooseAnnotationColor(null);
        check(owner.EffectiveAnnotationColor==new AnnotationColor(66,85,197),"Classic without a choice keeps the 0.2.3 blue default");
        Appearance.ChooseDesign(DesignPreference.Ember);
        check(owner.AnnotationColor==null && owner.EffectiveAnnotationColor==new AnnotationColor(240,77,50),"Ember without a choice keeps Signal Red and stays unset");
        owner.ChooseAnnotationColor(Yellow);
        var overlay=Overlay(owner,640,600);
        try
        {
            overlay.ChooseRegion(new(0,0,640,600));Arrange(overlay,640,600);
            overlay.Switch(Tool.Arrow);overlay.BeginPointer(new(80,80));overlay.EndPointer(new(300,200));
            var committed=owner.Session.Commit(overlay.Model);
            check(committed.Annotations.OfType<Arrow>().Single().Color==Yellow && owner.AnnotationColor==Yellow,"Committed screenshot keeps its color and the working color stays in RAM");
        }
        finally { overlay.ClosingByApp=true;overlay.Close(); }
        var next=Overlay(owner,640,600);
        try
        {
            check(next.CurrentAnnotationColor==Yellow,"Working color survives the capture/session cycle in runtime memory");
            Appearance.ChooseDesign(DesignPreference.Classic);
            check(next.CurrentAnnotationColor==Yellow,"Explicit color does not follow a design switch");
        }
        finally { next.ClosingByApp=true;next.Close(); }
        string saved=Path.Combine(Path.GetTempPath(),"ScreenItVerification",Guid.NewGuid().ToString("N"),"settings.json");
        try
        {
            owner.Preferences.Save(saved);
            var json=File.ReadAllText(saved);
            check(!json.Contains("annotationColor",StringComparison.OrdinalIgnoreCase) && !json.Contains("color",StringComparison.OrdinalIgnoreCase),"Preferences schema contains no annotation color field");
        }
        finally { if(File.Exists(saved)) File.Delete(saved);var dir=Path.GetDirectoryName(saved)!;if(Directory.Exists(dir)) Directory.Delete(dir,true); }
        check(!File.Exists(settings) && owner.Preferences.Design==DesignPreference.Classic,"Color selection never writes the settings file");
        owner.Clear(true);
    }
    private static Coordinator Owner()
    {
        var preferences=new Preferences { Language="en",Theme=ThemePreference.Dark };
        var owner=new Coordinator(showTray:false,registerHotkey:false,preferences:preferences);
        owner.PreferencesPath=Path.Combine(Path.GetTempPath(),"ScreenItVerification",Guid.NewGuid().ToString("N"),"settings.json");
        return owner;
    }
    private static AnnotationOverlay Overlay(Coordinator owner,int width,int height) => new(owner,new Frame(new MonitorData("Color fixture",0,0,width,height,true,96),Fill(width,height,26,26,26)));
    private static void Settle(AnnotationOverlay overlay,int width,int height)
    {
        var root=(FrameworkElement)overlay.Content;
        typeof(AnnotationOverlay).GetMethod("LayoutColorPicker",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
    }
    private static void Arrange(AnnotationOverlay overlay,int width,int height)
    {
        var root=(FrameworkElement)overlay.Content;root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        // Hidden fixtures do not receive the visible layout callback; run the production positioning method.
        typeof(AnnotationOverlay).GetMethod("LayoutToolbar",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(overlay,null);
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
    }
    private static object? Field(object target,string name) => target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target);
    private static void Press(UIElement target,Key key,Coordinator owner)
    {
        var args=new KeyEventArgs(Keyboard.PrimaryDevice,HwndSource.FromHwnd(owner.ControlHandle),0,key) { RoutedEvent=target is AnnotationOverlay ? Keyboard.PreviewKeyDownEvent : Keyboard.KeyDownEvent };
        target.RaiseEvent(args);
    }
    private static BitmapSource Fill(int width,int height,byte r,byte g,byte b)
    {
        var pixels=new byte[width*height*4];
        for(int i=0;i<pixels.Length;i+=4) { pixels[i]=b;pixels[i+1]=g;pixels[i+2]=r;pixels[i+3]=255; }
        var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);image.Freeze();return image;
    }
    private static byte[] Pixels(BitmapSource image) { var pixels=new byte[image.PixelWidth*image.PixelHeight*4];image.CopyPixels(pixels,image.PixelWidth*4,0);return pixels; }
    private static AnnotationColor Pixel(byte[] pixels,int width,int x,int y) { int i=(y*width+x)*4;return new(pixels[i+2],pixels[i+1],pixels[i]); }
    private static AnnotationColor ColorToColor(Brush brush) { var c=((SolidColorBrush)brush).Color;return new(c.R,c.G,c.B); }
    // Thin marker accents are antialiased; accept a nearby shade of the same hue, not Paper/Ink chrome.
    private static bool Accentish(AnnotationColor actual,AnnotationColor expected) => Math.Abs(actual.R-expected.R)<=60 && Math.Abs(actual.G-expected.G)<=60 && Math.Abs(actual.B-expected.B)<=60;
    private static bool Near(AnnotationColor actual,AnnotationColor expected) => Math.Abs(actual.R-expected.R)<=2 && Math.Abs(actual.G-expected.G)<=2 && Math.Abs(actual.B-expected.B)<=2;
    private static bool HasNear(byte[] pixels,int width,int x,int y,int radius,AnnotationColor expected)
    {
        int height=pixels.Length/(width*4);
        for(int dy=-radius;dy<=radius;dy++) for(int dx=-radius;dx<=radius;dx++)
        {
            int px=x+dx,py=y+dy;if(px<0||py<0||px>=width||py>=height) continue;
            if(Near(Pixel(pixels,width,px,py),expected)) return true;
        }
        return false;
    }
    private static AnnotationColor[] BadgeRegion(byte[] pixels,int width,Marker marker,Bounds badge)
    {
        var result=new List<AnnotationColor>();
        for(int y=(int)badge.Y;y<(int)(badge.Y+badge.Height);y++)
            for(int x=(int)badge.X;x<(int)(badge.X+badge.Width);x++)
                result.Add(Pixel(pixels,width,x,y));
        return result.ToArray();
    }
    private static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject
    {
        for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)
        {
            var child=VisualTreeHelper.GetChild(root,i);
            if(child is T typed) yield return typed;
            foreach(var nested in Descendants<T>(child)) yield return nested;
        }
    }
    private static void Save(FrameworkElement root,int width,int height,string path)
    {
        root.Measure(new Size(width,height));root.Arrange(new Rect(0,0,width,height));root.UpdateLayout();
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
        Save(bitmap,width,height,path);
    }
    private static void Save(BitmapSource image,int width,int height,string path)
    {
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(path);encoder.Save(output);
    }
}
