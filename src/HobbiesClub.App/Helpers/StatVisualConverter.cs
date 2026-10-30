using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace HobbiesClub.App.Helpers;

/// <summary>Dashboard stat card visuals by label: parameter "fg", "bg" or "icon".</summary>
public sealed class StatVisualConverter : IValueConverter
{
    private static readonly (string Key, string Fg, string Bg, string Icon)[] Map =
    {
        ("missed",    "#D97706", "#FEF3C7", "M12,3 A9,9 0 1 0 12,21 A9,9 0 1 0 12,3 M12,7 V12 L15,14"),
        ("terminat",  "#DC2626", "#FEE2E2", "M9,11 A3.5,3.5 0 1 0 9,4 A3.5,3.5 0 1 0 9,11 M2.5,20 C2.5,16 5.5,14 9,14 C12.5,14 15.5,16 15.5,20 M17,8 L21,12 M21,8 L17,12"),
        ("check-out", "#2563EB", "#DBEAFE", "M9,21 H5 A2,2 0 0 1 3,19 V5 A2,2 0 0 1 5,3 H9 M16,17 L21,12 L16,7 M21,12 H9"),
        ("check-in",  "#16A34A", "#DCFCE7", "M15,3 H19 A2,2 0 0 1 21,5 V19 A2,2 0 0 1 19,21 H15 M10,17 L15,12 L10,7 M15,12 H3"),
        ("inside",    "#16A34A", "#DCFCE7", "M3,11 L12,4 L21,11 M5,10 V20 H19 V10"),
        ("visit",     "#7C3AED", "#EDE9FE", "M4,6 H20 V20 H4 Z M4,10 H20 M8,3 V7 M16,3 V7"),
        ("active",    "#0284C7", "#E0F2FE", "M9,11 A3.5,3.5 0 1 0 9,4 A3.5,3.5 0 1 0 9,11 M2.5,20 C2.5,16 5.5,14 9,14 C12.5,14 15.5,16 15.5,20 M16,4.5 A3,3 0 0 1 16,10.5 M18,14.5 C20,15.2 21.5,17 21.5,20"),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value?.ToString() ?? string.Empty).ToLowerInvariant();
        (string Key, string Fg, string Bg, string Icon) pick = ("", "#0B2A55", "#DCE4F2", "M4,20 V10 M10,20 V4 M16,20 V13 M2,20 H22");
        foreach (var m in Map) if (s.Contains(m.Key)) { pick = m; break; }
        var p = parameter as string;
        if (p == "icon") { var g = Geometry.Parse(pick.Icon); g.Freeze(); return g; }
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(p == "bg" ? pick.Bg : pick.Fg)); b.Freeze(); return b;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
