using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using HobbiesClub.Core;

namespace HobbiesClub.App.Views;

/// <summary>Left and right hand. Shows registered fingers in green; can be clickable to pick a finger.</summary>
public class HandsControl : UserControl
{
    private static readonly Brush NormalFill = Make(0x1E, 0x4A, 0x80), NormalEdge = Make(0x4A, 0x6F, 0xA5);
    private static readonly Brush RegFill = Make(0x16, 0xA3, 0x4A), RegEdge = Make(0x86, 0xEF, 0xAC);
    private static readonly Brush SelFill = Make(0xC9, 0xA2, 0x27);
    private static Brush Make(byte r, byte g, byte b) { var x = new SolidColorBrush(Color.FromRgb(r, g, b)); x.Freeze(); return x; }

    public static readonly DependencyProperty RegisteredFingersProperty = DependencyProperty.Register(
        nameof(RegisteredFingers), typeof(IEnumerable<int>), typeof(HandsControl),
        new PropertyMetadata(null, (d, _) => ((HandsControl)d).Refresh()));

    public IEnumerable<int>? RegisteredFingers
    {
        get => (IEnumerable<int>?)GetValue(RegisteredFingersProperty);
        set => SetValue(RegisteredFingersProperty, value);
    }

    private readonly Border[] _fingers = new Border[10];
    private readonly List<TextBlock> _labels = new();
    private bool _clickable;
    private int _selected = -1;

    public event Action<int>? FingerClicked;

    public bool Clickable
    {
        get => _clickable;
        set { _clickable = value; foreach (var f in _fingers) f.Cursor = value ? Cursors.Hand : Cursors.Arrow; }
    }

    public int Selected
    {
        get => _selected;
        set { _selected = value; Refresh(); }
    }

    public Brush LabelBrush
    {
        set { foreach (var l in _labels) l.Foreground = value; }
    }

    public HandsControl()
    {
        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());
        var left = BuildHand(true);
        var right = BuildHand(false);
        Grid.SetColumn(right, 2);
        grid.Children.Add(left);
        grid.Children.Add(right);
        Content = grid;
        Refresh();
    }

    private FrameworkElement BuildHand(bool left)
    {
        var canvas = new Canvas { Width = 220, Height = 240 };
        (double X, double Y, double W, double H)[] pos = { (44, 72, 26, 62), (74, 40, 29, 90), (107, 22, 30, 106), (141, 40, 29, 90) };
        int[] leftIdx = { 9, 8, 7, 6 };   // little, ring, middle, index
        int[] rightIdx = { 4, 3, 2, 1 };
        for (var i = 0; i < 4; i++)
        {
            var (x, y, w, h) = pos[i];
            if (!left) x = 220 - x - w;
            AddFinger(canvas, left ? leftIdx[i] : rightIdx[i], x, y, w, h, 0);
        }
        if (left) AddFinger(canvas, 5, 168, 118, 27, 66, -35);
        else AddFinger(canvas, 0, 25, 118, 27, 66, 35);

        var palm = new Border { Width = 134, Height = 116, CornerRadius = new CornerRadius(34), Background = NormalFill, BorderBrush = NormalEdge, BorderThickness = new Thickness(1.5), IsHitTestVisible = false };
        Canvas.SetLeft(palm, left ? 40 : 46);
        Canvas.SetTop(palm, 112);
        canvas.Children.Add(palm);

        var label = new TextBlock { Text = left ? "LEFT HAND" : "RIGHT HAND", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Make(0x9F, 0xB3, 0xD1), HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(0, 6, 0, 0) };
        _labels.Add(label);
        var sp = new StackPanel();
        sp.Children.Add(new Viewbox { Child = canvas });
        sp.Children.Add(label);
        return sp;
    }

    private void AddFinger(Canvas canvas, int index, double x, double y, double w, double h, double angle)
    {
        var b = new Border
        {
            Width = w, Height = h, CornerRadius = new CornerRadius(w / 2), BorderThickness = new Thickness(1.5),
            ToolTip = FingerNames.Name(index), Background = NormalFill, BorderBrush = NormalEdge
        };
        if (angle != 0) b.RenderTransform = new RotateTransform(angle);
        Canvas.SetLeft(b, x);
        Canvas.SetTop(b, y);
        b.MouseLeftButtonUp += (_, _) => { if (_clickable) FingerClicked?.Invoke(index); };
        _fingers[index] = b;
        canvas.Children.Add(b);
    }

    private void Refresh()
    {
        var reg = RegisteredFingers?.ToHashSet() ?? new HashSet<int>();
        for (var i = 0; i < 10; i++)
        {
            var b = _fingers[i];
            if (b is null) continue;
            b.BeginAnimation(OpacityProperty, null);
            b.Opacity = 1;
            if (i == _selected)
            {
                b.Background = SelFill;
                b.BorderBrush = Brushes.White;
                b.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.45, TimeSpan.FromSeconds(0.8)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
            }
            else if (reg.Contains(i)) { b.Background = RegFill; b.BorderBrush = RegEdge; }
            else { b.Background = NormalFill; b.BorderBrush = NormalEdge; }
        }
    }
}
