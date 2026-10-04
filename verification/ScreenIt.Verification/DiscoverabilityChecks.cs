using System;
using ScreenIt.Core;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

internal static class DiscoverabilityChecks
{
    internal static void Run(Action<bool,string> check,Coordinator coordinator,AnnotationOverlay overlay)
    {
        check(coordinator.PrimaryCommands.Select(x=>x.Text).SequenceEqual(new[]{"Capture","Paste Session","Clear Session"}),"Tray primary action names without inline debug shortcut text");
        check(coordinator.PrimaryCommands.Select(x=>x.ShortcutKeyDisplayString).SequenceEqual(new[]{"Ctrl+Alt+S","Ctrl+Alt+V","Ctrl+Alt+X"}) && coordinator.PrimaryCommands.All(x=>x.ShowShortcutKeys),"Native tray right-column S/V/X shortcut display");
        var expected=new[]{"Marker (M)","Arrow (A)","Rectangle (R)","Edit comment (E)","Delete selected (Delete)","Undo (Ctrl+Z)","Redo (Ctrl+Y)","Done (Ctrl+Enter)"};
        foreach(var theme in new[]{UiTheme.Dark,UiTheme.Light})
        {
            Appearance.Select(theme);
            check(overlay.ToolbarButtons.Select(b=>((ToolTip)b.ToolTip).Content).SequenceEqual(expected),"All toolbar action tooltips "+theme);
            check(overlay.ToolbarButtons.All(b=>b.ToolTip is ToolTip tip && tip.Foreground==Appearance.Palette.Text && tip.Background==Appearance.Palette.Surface && ToolTipService.GetShowOnDisabled(b)),"Themed readable tooltips including disabled Undo/Redo "+theme);
            foreach(var (key,tool) in new[]{(Key.M,Tool.Marker),(Key.A,Tool.Arrow),(Key.R,Tool.Rectangle)})
            {
                overlay.RaiseEvent(new KeyEventArgs(Keyboard.PrimaryDevice,PresentationSource.FromVisual(overlay),0,key) { RoutedEvent=Keyboard.PreviewKeyDownEvent });
                check(overlay.Tool==tool && overlay.ToolHighlighted(tool),"Existing tool key still selects active tool "+key+" "+theme);
            }
            foreach(var (hint,key) in new[]{("Marker (M)","M"),("Arrow (A)","A"),("Rectangle (R)","R"),("Done (Ctrl+Enter)","Ctrl+Enter")})
            {
                var button=overlay.ToolbarButtons.Single(b=>((ToolTip)b.ToolTip).Content.Equals(hint));
                var chip=(Border)((StackPanel)button.Content).Children[1];var text=(TextBlock)chip.Child;
                check(text.Text==key && text.FontWeight==FontWeights.Normal && chip.Opacity==1,"Visible secondary shortcut chip "+key+" "+theme);
                var background=(SolidColorBrush)(button.Background==Brushes.Transparent ? Appearance.Palette.Surface : button.Background);
                check(Contrast(((SolidColorBrush)text.Foreground).Color,background.Color)>=4.5,"Shortcut text contrast "+key+" "+theme);
            }
        }
        overlay.Switch(Tool.Marker);
    }
    private static double Contrast(Color a,Color b)
    {
        static double Channel(byte c) { double x=c/255.0;return x<=.04045 ? x/12.92 : Math.Pow((x+.055)/1.055,2.4); }
        static double L(Color c)=>.2126*Channel(c.R)+.7152*Channel(c.G)+.0722*Channel(c.B);
        return (Math.Max(L(a),L(b))+.05)/(Math.Min(L(a),L(b))+.05);
    }
}
