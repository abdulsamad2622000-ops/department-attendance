using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace HobbiesClub.App.Helpers;

/// <summary>
/// Colours a registration progress step. Value = current Step (int).
/// Parameter = "from-to|part", e.g. "3-4|fill". Parts: fill, fg, label, weight.
/// </summary>
public class StepStateConverter : IValueConverter
{
    private static readonly Brush Done = Freeze(new SolidColorBrush(Color.FromRgb(0x16, 0xA3, 0x4A)));
    private static readonly Brush Current = Freeze(new SolidColorBrush(Color.FromRgb(0x25, 0x63, 0xEB)));
    private static readonly Brush Todo = Freeze(new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)));
    private static readonly Brush TodoText = Freeze(new SolidColorBrush(Color.FromRgb(0x47, 0x55, 0x69)));
    private static readonly Brush Dark = Freeze(new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF)));
    private static readonly Brush Muted = Freeze(new SolidColorBrush(Color.FromRgb(0x9F, 0xB3, 0xD1)));

    private static Brush Freeze(SolidColorBrush b) { b.Freeze(); return b; }

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var step = value is int i ? i : 0;
        var parts = (parameter as string ?? "0-0|fill").Split('|');
        var range = parts[0].Split('-');
        int from = int.Parse(range[0]), to = int.Parse(range[1]);
        var state = step > to ? 2 : step >= from ? 1 : 0; // 2 done, 1 current, 0 todo

        return (parts.Length > 1 ? parts[1] : "fill") switch
        {
            "fill" => state == 2 ? Done : state == 1 ? Current : Todo,
            "fg" => state == 0 ? TodoText : Brushes.White,
            "label" => state == 0 ? Muted : Dark,
            "weight" => state == 1 ? FontWeights.SemiBold : FontWeights.Normal,
            _ => Binding.DoNothing
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}


