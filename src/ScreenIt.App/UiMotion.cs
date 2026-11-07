using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

// Short, finite presentation-only transitions. Values are committed before any animation starts.
internal static class UiMotion
{
    internal static void Animate(Animatable target,DependencyProperty property,double from,double to,int milliseconds=140)
    {
        target.BeginAnimation(property,null);
        if(!SystemParameters.ClientAreaAnimation || Math.Abs(from-to)<.001) return;
        target.BeginAnimation(property,new DoubleAnimation(from,to,TimeSpan.FromMilliseconds(milliseconds))
        { EasingFunction=new QuadraticEase { EasingMode=EasingMode.EaseOut },FillBehavior=FillBehavior.Stop });
    }
    internal static void Enter(FrameworkElement element)
    {
        if(!element.IsLoaded)
        {
            RoutedEventHandler? ready=null;ready=(_,_)=> { element.Loaded-=ready;Enter(element); };
            element.Loaded+=ready;return;
        }
        if(!element.IsVisible || !SystemParameters.ClientAreaAnimation) return;
        var offset=new TranslateTransform();element.RenderTransform=offset;
        element.BeginAnimation(UIElement.OpacityProperty,new DoubleAnimation(.72,1,TimeSpan.FromMilliseconds(140)) { FillBehavior=FillBehavior.Stop });
        Animate(offset,TranslateTransform.YProperty,4,0);
        element.Unloaded+=(_,_)=> { element.BeginAnimation(UIElement.OpacityProperty,null);offset.BeginAnimation(TranslateTransform.YProperty,null); };
    }
}
