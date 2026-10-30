using System;
using System.Globalization;
using System.Linq;
using System.Windows.Data;
using System.Windows.Media;

namespace HobbiesClub.App.Helpers;

/// <summary>Picks a pill colour from its text: red = problem, amber = warning, green = good, blue = neutral.</summary>
public sealed class PillColorConverter : IValueConverter
{
    public bool Foreground { get; set; }

    private static readonly (string[] Keys, string Bg, string Fg)[] Map =
    {
        (new[] { "wrong", "fail", "locked", "absent", "terminat", "inactive", "error", "denied", "delete", "removed" }, "#FEE2E2", "#B91C1C"),
        (new[] { "late", "missed", "warn", "reset", "unlock", "pending", "leave" }, "#FEF3C7", "#92400E"),
        (new[] { "logged in", "active", "present", "check-in", "checked in", "success", "created", "saved", "restored", "enrolled" }, "#DCFCE7", "#166534"),
    };

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var s = (value?.ToString() ?? string.Empty).ToLowerInvariant();
        string bg = "#DBEAFE", fg = "#1E40AF";
        foreach (var m in Map)
            if (m.Keys.Any(k => s.Contains(k))) { bg = m.Bg; fg = m.Fg; break; }
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(Foreground ? fg : bg));
        brush.Freeze();
        return brush;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
