using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ArcadiaWeather.Presentation;

/// <summary>Short, replaceable transitions. Idle and hidden windows have no animation loop.</summary>
public static class Motion
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(Motion), new FrameworkPropertyMetadata(true, FrameworkPropertyMetadataOptions.Inherits, OnEnabledChanged));
    public static bool GetEnabled(DependencyObject target) => (bool)target.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject target, bool value) => target.SetValue(EnabledProperty, value);

    public static readonly DependencyProperty OffsetXProperty = DependencyProperty.RegisterAttached(
        "OffsetX", typeof(double), typeof(Motion), new PropertyMetadata(0d, OnOffsetXChanged));
    public static double GetOffsetX(DependencyObject target) => (double)target.GetValue(OffsetXProperty);
    public static void SetOffsetX(DependencyObject target, double value) => target.SetValue(OffsetXProperty, value);

    private static void OnOffsetXChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is FrameworkElement element) SlideTo(element, (double)e.NewValue);
    }

    private static void OnEnabledChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        // Finish an in-flight switch immediately when Windows turns animation off.
        if (e.NewValue is false && target is FrameworkElement { RenderTransform: TranslateTransform { IsFrozen: false } offset })
            offset.BeginAnimation(TranslateTransform.XProperty, null);
    }

    public static void SlideTo(FrameworkElement element, double to, bool animate = true)
    {
        if (element.RenderTransform is not TranslateTransform offset)
        {
            offset = new TranslateTransform();
            element.RenderTransform = offset;
        }
        // Read the current animated position so repeated clicks reverse smoothly.
        double from = offset.X;
        offset.BeginAnimation(TranslateTransform.XProperty, null);
        offset.X = to;
        if (animate && element.IsLoaded && CanAnimate(element) && Math.Abs(from - to) > 0.01)
            offset.BeginAnimation(TranslateTransform.XProperty, Transition(from, to, 200));
    }

    public static readonly DependencyProperty LiftProperty = DependencyProperty.RegisterAttached(
        "Lift", typeof(double), typeof(Motion), new PropertyMetadata(0d, OnLiftChanged));
    public static double GetLift(DependencyObject target) => (double)target.GetValue(LiftProperty);
    public static void SetLift(DependencyObject target, double value) => target.SetValue(LiftProperty, value);

    public static bool CanAnimate(UIElement element) => GetEnabled(element) && SystemParameters.ClientAreaAnimation && element.IsVisible;

    public static DoubleAnimation Transition(double from, double to, int milliseconds) => new(from, to, TimeSpan.FromMilliseconds(milliseconds))
    {
        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        FillBehavior = FillBehavior.Stop
    };

    public static void Reveal(FrameworkElement element, int delay = 0)
    {
        element.BeginAnimation(UIElement.OpacityProperty, null);
        element.Opacity = 1;
        var offset = new TranslateTransform();
        element.RenderTransform = offset;
        if (!CanAnimate(element)) return;
        var fade = Transition(0, 1, 300);
        var slide = Transition(10, 0, 360);
        fade.BeginTime = slide.BeginTime = TimeSpan.FromMilliseconds(delay);
        element.BeginAnimation(UIElement.OpacityProperty, fade);
        offset.BeginAnimation(TranslateTransform.YProperty, slide);
    }

    private static void OnLiftChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
    {
        if (target is not FrameworkElement element) return;
        element.MouseEnter -= LiftEnter; element.MouseLeave -= LiftLeave; element.Unloaded -= LiftLeave;
        if ((double)e.NewValue == 0) return;
        element.RenderTransform = new TranslateTransform();
        element.MouseEnter += LiftEnter; element.MouseLeave += LiftLeave; element.Unloaded += LiftLeave;
    }
    private static void LiftEnter(object sender, System.Windows.Input.MouseEventArgs e) => Move((FrameworkElement)sender, -GetLift((DependencyObject)sender));
    private static void LiftLeave(object sender, RoutedEventArgs e) => Move((FrameworkElement)sender, 0);
    private static void Move(FrameworkElement element, double to)
    {
        if (element.RenderTransform is not TranslateTransform offset) return;
        double from = offset.Y;
        offset.BeginAnimation(TranslateTransform.YProperty, null);
        offset.Y = CanAnimate(element) ? to : 0;
        if (CanAnimate(element)) offset.BeginAnimation(TranslateTransform.YProperty, Transition(from, to, 180));
    }
}
