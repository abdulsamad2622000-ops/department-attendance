using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.App.Views;

/// <summary>Guided fingerprint capture: highlighted finger, scan animation, live image and quality.</summary>
public partial class FingerScanDialog : Window
{
    public enum Finger { Index, Middle, Any }

    private static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5));

    private readonly IFingerprintService _fp;
    private bool _busy;

    public byte[]? Template { get; private set; }

    public FingerScanDialog(IFingerprintService fp, string title, string subtitle, Finger finger)
    {
        InitializeComponent();
        _fp = fp;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;

        var target = finger switch { Finger.Middle => F_Middle, Finger.Index => F_Index, _ => null };
        HandHint.Text = finger switch
        {
            Finger.Index => "Use your INDEX finger",
            Finger.Middle => "Use your MIDDLE finger",
            _ => "Use a registered finger"
        };
        if (target is not null)
        {
            target.Background = Gold;
            target.BorderBrush = Brushes.White;
            target.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        }
        else
        {
            foreach (var b in new[] { F_Index, F_Middle }) { b.Background = Gold; b.BorderBrush = Brushes.White; }
        }

        Loaded += async (_, _) => await ScanAsync();
    }

    /// <summary>Shows the dialog and returns the template, or null if cancelled.</summary>
    public static byte[]? Scan(IFingerprintService fp, string title, string subtitle, Finger finger)
    {
        var d = new FingerScanDialog(fp, title, subtitle, finger) { Owner = Application.Current?.MainWindow };
        return d.ShowDialog() == true ? d.Template : null;
    }

    private void StartScanLine(bool on)
    {
        ScanLine.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) ScanShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 222, TimeSpan.FromSeconds(1.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else ScanShift.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private async Task ScanAsync()
    {
        if (_busy) return;
        _busy = true;
        RetryButton.Visibility = Visibility.Collapsed;
        PrintImage.Visibility = Visibility.Collapsed;
        PrintIcon.Visibility = Visibility.Visible;
        QualityBar.Width = 0;
        QualityText.Text = "Quality —";
        StatusText.Foreground = Brushes.White;
        StatusText.Text = "Place your finger flat on the scanner…";
        StartScanLine(true);

        FingerCapture? cap = null;
        try { cap = await _fp.CaptureAsync(); }
        catch (Exception ex) { Serilog.Log.Error(ex, "Fingerprint capture failed"); }

        StartScanLine(false);
        _busy = false;
        if (!IsLoaded) return;

        if (cap is null)
        {
            StatusText.Foreground = Red;
            StatusText.Text = string.IsNullOrWhiteSpace(_fp.LastError) ? "Scan failed. Please try again." : _fp.LastError;
            RetryButton.Visibility = Visibility.Visible;
            return;
        }

        if (cap.Image is { Length: > 0 } img && cap.Width > 0 && cap.Height > 0)
        {
            PrintImage.Source = BitmapSource.Create(cap.Width, cap.Height, 96, 96, PixelFormats.Gray8, null, img, cap.Width);
            PrintImage.Visibility = Visibility.Visible;
            PrintIcon.Visibility = Visibility.Collapsed;
        }
        if (cap.Quality > 0)
        {
            QualityBar.Width = Math.Max(0, Math.Min(1, cap.Quality / 100.0)) * ((FrameworkElement)QualityBar.Parent).ActualWidth;
            QualityText.Text = $"Quality {cap.Quality}%";
        }

        StatusText.Foreground = Green;
        StatusText.Text = "✓  Fingerprint captured";
        Template = cap.Template;
        CancelButton.IsEnabled = false;
        await Task.Delay(1100);
        DialogResult = true;
    }

    private async void OnRetry(object sender, RoutedEventArgs e) => await ScanAsync();

    private void OnCancel(object sender, RoutedEventArgs e) => DialogResult = false;
}

