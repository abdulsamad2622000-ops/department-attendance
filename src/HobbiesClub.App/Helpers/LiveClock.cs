using System.ComponentModel;
using System.Windows.Threading;

namespace HobbiesClub.App.Helpers;

/// <summary>Ticks every second so XAML can show a live clock without code-behind.</summary>
public sealed class LiveClock : INotifyPropertyChanged
{
    public static LiveClock Instance { get; } = new();

    private readonly DispatcherTimer _timer;

    private LiveClock()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Now)));
        _timer.Start();
    }

    public DateTime Now => DateTime.Now;

    public event PropertyChangedEventHandler? PropertyChanged;
}
