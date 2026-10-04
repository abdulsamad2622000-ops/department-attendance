using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using HobbiesClub.Core;
using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.App.Views;

/// <summary>Pick any fingers on both hands and scan each one. Returns position (0-9) to template.</summary>
public partial class FingerEnrollDialog : Window
{
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5));

    private readonly IFingerprintService _fp;
    private readonly Dictionary<int, byte[]> _captured = new();
    private bool _busy;

    public FingerEnrollDialog(IFingerprintService fp, string title, string subtitle)
    {
        InitializeComponent();
        _fp = fp;
        TitleText.Text = title;
        SubtitleText.Text = subtitle;
        Hands.Clickable = true;
        Hands.FingerClicked += async i => await OnFingerAsync(i);
        UpdateUi();
    }

    public static Dictionary<int, byte[]>? Enroll(IFingerprintService fp, string title, string subtitle)
    {
        var d = new FingerEnrollDialog(fp, title, subtitle) { Owner = Application.Current?.MainWindow };
        return d.ShowDialog() == true ? new Dictionary<int, byte[]>(d._captured) : null;
    }

    private void UpdateUi()
    {
        SaveButton.IsEnabled = !_busy && _captured.Count > 0;
        SaveButton.Content = $"Save ({_captured.Count})";
        CancelButton.IsEnabled = !_busy;
        CountText.Text = _captured.Count == 0
            ? "You can register up to 10 fingers. At least 2 are recommended."
            : $"Registered: {string.Join(", ", _captured.Keys.Order().Select(FingerNames.Name))}";
    }

    private async Task OnFingerAsync(int index)
    {
        if (_busy) return;
        Hands.Selected = index;
        if (_captured.ContainsKey(index))
        {
            StatusText.Foreground = Brushes.White;
            StatusText.Text = $"{FingerNames.Name(index)} is registered. Scan it again or remove it.";
            RetryButton.Visibility = Visibility.Visible;
            RemoveButton.Visibility = Visibility.Visible;
            return;
        }
        await ScanAsync(index);
    }

    private void StartScanLine(bool on)
    {
        ScanLine.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) ScanShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(4, 222, TimeSpan.FromSeconds(1.4)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else ScanShift.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private async Task ScanAsync(int index)
    {
        _busy = true;
        RetryButton.Visibility = Visibility.Collapsed;
        RemoveButton.Visibility = Visibility.Collapsed;
        PrintImage.Visibility = Visibility.Collapsed;
        PrintIcon.Visibility = Visibility.Visible;
        QualityBar.Width = 0;
        QualityText.Text = "Quality —";
        StatusText.Foreground = Brushes.White;
        StatusText.Text = $"Place your {FingerNames.Name(index).ToUpperInvariant()} finger on the scanner…";
        UpdateUi();
        StartScanLine(true);


        FingerCapture? cap = null;
        try { cap = await _fp.CaptureAsync(); }
        catch (Exception ex) { Serilog.Log.Error(ex, "Fingerprint capture failed"); }

        StartScanLine(false);
        _busy = false;

        if (cap is null)
        {
            StatusText.Foreground = Red;
            StatusText.Text = string.IsNullOrWhiteSpace(_fp.LastError) ? "Scan failed. Please try again." : _fp.LastError;
            RetryButton.Content = "Retry";
            RetryButton.Visibility = Visibility.Visible;
            UpdateUi();
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
            QualityBar.Width = Math.Min(1, cap.Quality / 100.0) * ((FrameworkElement)QualityBar.Parent).ActualWidth;
            QualityText.Text = $"Quality {cap.Quality}%";
        }

        var dup = _captured.Where(kv => kv.Key != index && _fp.IsMatch(cap.Template, kv.Value)).Select(kv => (int?)kv.Key).FirstOrDefault();
        if (dup is int d)
        {
            StatusText.Foreground = Red;
            StatusText.Text = $"This looks like your {FingerNames.Name(d)}. Use the {FingerNames.Name(index)}.";
            RetryButton.Content = "Retry";
            RetryButton.Visibility = Visibility.Visible;
            UpdateUi();
            return;
        }

        _captured[index] = cap.Template;
        Hands.Selected = -1;
        Hands.RegisteredFingers = _captured.Keys.ToList();
        StatusText.Foreground = Green;
        StatusText.Text = $"✓  {FingerNames.Name(index)} captured. Tap another finger, or press Save.";
        RetryButton.Content = "Scan again";
        UpdateUi();
    }

    private async void OnRetry(object sender, RoutedEventArgs e)
    {
        if (Hands.Selected >= 0) await ScanAsync(Hands.Selected);
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        var i = Hands.Selected;
        if (i < 0 || !_captured.Remove(i)) return;
        Hands.Selected = -1;
        Hands.RegisteredFingers = _captured.Keys.ToList();
        RetryButton.Visibility = Visibility.Collapsed;
        RemoveButton.Visibility = Visibility.Collapsed;
        StatusText.Foreground = Brushes.White;
        StatusText.Text = $"{FingerNames.Name(i)} removed.";
        UpdateUi();
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (_captured.Count > 0) DialogResult = true;
    }

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        if (_captured.Count > 0 && MessageBox.Show("Discard the fingers you scanned?", "Cancel", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;
        DialogResult = false;
    }
}

