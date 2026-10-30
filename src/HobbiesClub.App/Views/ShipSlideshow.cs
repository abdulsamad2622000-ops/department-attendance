using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace HobbiesClub.App.Views;

/// <summary>Full-screen slideshow of ship photos from Assets\Ships, with a slow zoom and cross-fade. Shows the ship name.</summary>
public class ShipSlideshow : Grid
{
    private readonly Image _back = new() { Stretch = Stretch.UniformToFill, RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly Image _front = new() { Stretch = Stretch.UniformToFill, Opacity = 0, RenderTransformOrigin = new Point(0.5, 0.5) };
    private readonly TextBlock _caption = new() { FontSize = 15, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
    private readonly Border _captionBox;
    private readonly List<(BitmapImage Image, string Name)> _ships = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(7) };
    private int _index;

    public ShipSlideshow()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x0B, 0x2A, 0x55));
        Children.Add(_back);
        Children.Add(_front);
        _captionBox = new Border
        {
            Background = new SolidColorBrush(Color.FromArgb(0x99, 0x07, 0x1A, 0x33)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0xC9, 0xA2, 0x27)),
            BorderThickness = new Thickness(3, 0, 0, 0),
            Padding = new Thickness(14, 8, 16, 8),
            CornerRadius = new CornerRadius(4),
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(40, 40, 0, 0),
            Child = _caption,
            Visibility = Visibility.Collapsed
        };
        Panel.SetZIndex(_captionBox, 10);
        Children.Add(_captionBox);
        _timer.Tick += (_, _) => Next();
        Loaded += (_, _) => Start();
        Unloaded += (_, _) => _timer.Stop();
    }

    private void Start()
    {
        _ships.Clear();
        var dir = Path.Combine(AppContext.BaseDirectory, "Assets", "Ships");
        if (Directory.Exists(dir))
        {
            foreach (var f in Directory.GetFiles(dir).Where(f => new[] { ".jpg", ".jpeg", ".png" }.Contains(Path.GetExtension(f).ToLowerInvariant())).OrderBy(f => f))
            {
                try
                {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                    bmp.DecodePixelWidth = 1920;
                    bmp.UriSource = new Uri(f);
                    bmp.EndInit();
                    bmp.Freeze();
                    var name = Path.GetFileNameWithoutExtension(f).Replace('_', ' ').Replace('-', ' ');
                    _ships.Add((bmp, name));
                }
                catch { /* skip a broken file */ }
            }
        }
        if (_ships.Count == 0) return;

        _index = 0;
        Show(_back, _ships[0]);
        if (_ships.Count > 1) _timer.Start();
    }

    private void Show(Image target, (BitmapImage Image, string Name) ship)
    {
        target.Source = ship.Image;
        var zoom = new ScaleTransform(1, 1);
        target.RenderTransform = zoom;
        var grow = new DoubleAnimation(1, 1.08, TimeSpan.FromSeconds(9));
        zoom.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
        zoom.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        _caption.Text = ship.Name;
        _captionBox.Visibility = Visibility.Collapsed;
    }

    private void Next()
    {
        _index = (_index + 1) % _ships.Count;
        var ship = _ships[_index];
        Show(_front, ship);
        var fade = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(1.5));
        fade.Completed += (_, _) =>
        {
            _back.Source = _front.Source;
            _back.RenderTransform = _front.RenderTransform;
            _front.BeginAnimation(OpacityProperty, null);
            _front.Opacity = 0;
        };
        _front.BeginAnimation(OpacityProperty, fade);
    }
}



