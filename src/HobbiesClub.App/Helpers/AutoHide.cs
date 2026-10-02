using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace HobbiesClub.App.Helpers;

/// <summary>Hides a message TextBlock a few seconds after its text changes. Use: helpers:AutoHide.Seconds="6"</summary>
public static class AutoHide
{
    public static readonly DependencyProperty SecondsProperty = DependencyProperty.RegisterAttached(
        "Seconds", typeof(double), typeof(AutoHide), new PropertyMetadata(0.0, OnSecondsChanged));

    private static readonly DependencyProperty TimerProperty = DependencyProperty.RegisterAttached(
        "Timer", typeof(DispatcherTimer), typeof(AutoHide));

    public static double GetSeconds(DependencyObject o) => (double)o.GetValue(SecondsProperty);
    public static void SetSeconds(DependencyObject o, double v) => o.SetValue(SecondsProperty, v);

    private static void OnSecondsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not TextBlock tb) return;
        var dpd = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
        dpd.RemoveValueChanged(tb, OnTextChanged);
        if ((double)e.NewValue > 0)
        {
            dpd.AddValueChanged(tb, OnTextChanged);
            Apply(tb);
        }
    }

    private static void OnTextChanged(object? sender, EventArgs e)
    {
        if (sender is TextBlock tb) Apply(tb);
    }

    private static void Apply(TextBlock tb)
    {
        (tb.GetValue(TimerProperty) as DispatcherTimer)?.Stop();

        if (string.IsNullOrEmpty(tb.Text))
        {
            tb.Visibility = Visibility.Collapsed;
            return;
        }

        tb.Visibility = Visibility.Visible;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(GetSeconds(tb)) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            tb.Visibility = Visibility.Collapsed;
        };
        tb.SetValue(TimerProperty, timer);
        timer.Start();
    }
}
