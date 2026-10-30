using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using HobbiesClub.Core.Interfaces;

namespace HobbiesClub.App.Views;

/// <summary>Guided face scan with live camera, face points and look-straight / turn-left / turn-right steps.</summary>
public partial class FaceScanDialog : Window
{
    private static readonly Brush Gold = new SolidColorBrush(Color.FromRgb(0x3B, 0x82, 0xF6));
    private static readonly Brush Green = new SolidColorBrush(Color.FromRgb(0x22, 0xC5, 0x5E));
    private static readonly Brush LightGreen = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));
    private static readonly Brush Red = new SolidColorBrush(Color.FromRgb(0xFC, 0xA5, 0xA5));
    private static readonly Brush Muted = new SolidColorBrush(Color.FromRgb(0x9F, 0xB3, 0xD1));
    private static readonly Brush Blue = new SolidColorBrush(Color.FromRgb(0x4F, 0xA3, 0xE0));

    private readonly IFaceService _face;
    private readonly bool _liveness;
    private CancellationTokenSource _cts = new();
    private bool _busy;

    public byte[]? Result { get; private set; }

    public FaceScanDialog(IFaceService face, string title, bool liveness)
    {
        InitializeComponent();
        _face = face;
        _liveness = liveness;
        TitleText.Text = title;
        SubtitleText.Text = liveness ? "3 quick steps: look straight, turn left, turn right" : "Look straight at the camera";
        if (!liveness) { Step2.Visibility = Visibility.Collapsed; Step3.Visibility = Visibility.Collapsed; }
        Loaded += async (_, _) => await RunAsync();
        Closed += (_, _) => _cts.Cancel();
    }

    /// <summary>Shows the dialog and returns the face template, or null.</summary>
    public static byte[]? Scan(IFaceService face, string title, bool liveness)
    {
        var d = new FaceScanDialog(face, title, liveness) { Owner = Application.Current?.MainWindow };
        return d.ShowDialog() == true ? d.Result : null;
    }

    private void ScanLineOn(bool on)
    {
        ScanLine.Visibility = on ? Visibility.Visible : Visibility.Collapsed;
        if (on) ScanShift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, 376, TimeSpan.FromSeconds(1.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        else ScanShift.BeginAnimation(TranslateTransform.YProperty, null);
    }

    private async Task RunAsync()
    {
        if (_busy) return;
        _busy = true;
        _cts = new CancellationTokenSource();
        RetryButton.Visibility = Visibility.Collapsed;
        DoneBadge.Visibility = Visibility.Collapsed;
        Ring.Stroke = Brushes.White;
        StatusText.Foreground = Brushes.White;
        StatusText.Text = "Starting camera…";
        SetSteps(0);
        ScanLineOn(true);

        byte[]? template = null;
        try { template = await _face.CaptureGuidedAsync(_liveness, new Progress<FaceScanUpdate>(OnUpdate), _cts.Token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { Serilog.Log.Error(ex, "Face scan failed"); }

        _busy = false;
        ScanLineOn(false);
        ArrowLeft.Visibility = ArrowRight.Visibility = Visibility.Collapsed;
        if (!IsLoaded) return;

        if (template is null)
        {
            Ring.Stroke = Red;
            StatusText.Foreground = Red;
            if (StatusText.Text is "Starting camera…" or "Scanning…") StatusText.Text = "Face not captured. Please try again.";
            RetryButton.Visibility = Visibility.Visible;
            return;
        }

        Result = template;
        SetSteps(3);
        Ring.Stroke = Green;
        DoneBadge.Visibility = Visibility.Visible;
        StatusText.Foreground = LightGreen;
        StatusText.Text = "✓  Face captured";
        CancelButton.IsEnabled = false;
        await Task.Delay(1000);
        DialogResult = true;
    }

    private void SetSteps(int current)
    {
        var marks = new[] { Step1Mark, Step2Mark, Step3Mark };
        var pills = new[] { Step1, Step2, Step3 };
        for (var i = 0; i < 3; i++)
        {
            if (i < current) { marks[i].Text = "✓"; marks[i].Foreground = LightGreen; pills[i].BorderBrush = Green; }
            else if (i == current) { marks[i].Text = (i + 1).ToString(); marks[i].Foreground = Gold; pills[i].BorderBrush = Gold; }
            else { marks[i].Text = (i + 1).ToString(); marks[i].Foreground = Muted; pills[i].BorderBrush = new SolidColorBrush(Color.FromRgb(0x1E, 0x4A, 0x80)); }
        }
    }

    private void OnUpdate(FaceScanUpdate u)
    {
        if (u.Jpeg is { Length: > 0 })
        {
            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(u.Jpeg))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
            }
            bmp.Freeze();
            CameraImage.Source = bmp;
            if (u.Width > 0 && (FrameGrid.Width != u.Width || FrameGrid.Height != u.Height)) { FrameGrid.Width = u.Width; FrameGrid.Height = u.Height; }
        }

        Overlay.Children.Clear();
        if (u.Box is { Length: 4 } b)
        {
            var rect = new Rectangle { Width = b[2], Height = b[3], Stroke = Blue, StrokeThickness = 2, RadiusX = 12, RadiusY = 12, Opacity = 0.8 };
            Canvas.SetLeft(rect, b[0]); Canvas.SetTop(rect, b[1]);
            Overlay.Children.Add(rect);
        }
        if (u.Landmarks is { Length: 10 } lm)
        {
            for (var i = 0; i < 5; i++)
            {
                var dot = new Ellipse { Width = 10, Height = 10, Fill = Gold, Stroke = Brushes.White, StrokeThickness = 1.5 };
                Canvas.SetLeft(dot, lm[i * 2] - 5); Canvas.SetTop(dot, lm[i * 2 + 1] - 5);
                Overlay.Children.Add(dot);
            }
        }

        SetSteps(u.Done ? 3 : u.Step);
        ArrowLeft.Visibility = !u.Done && _liveness && u.Step == 1 ? Visibility.Visible : Visibility.Collapsed;
        ArrowRight.Visibility = !u.Done && _liveness && u.Step == 2 ? Visibility.Visible : Visibility.Collapsed;
        Ring.Stroke = u.Failed ? Red : u.FaceCount == 1 ? Gold : Brushes.White;
        StatusText.Foreground = u.Failed ? Red : Brushes.White;
        StatusText.Text = u.Hint;
    }

    private async void OnRetry(object sender, RoutedEventArgs e) => await RunAsync();

    private void OnCancel(object sender, RoutedEventArgs e)
    {
        _cts.Cancel();
        DialogResult = false;
    }
}

