using System.Windows.Threading;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App;

/// <summary>Checks every hour and makes the daily automatic backup if it is due.</summary>
public static class BackupScheduler
{
    private static DispatcherTimer? _timer;

    public static void Start()
    {
        if (_timer is not null) return;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromHours(1) };
        _timer.Tick += (_, _) => _ = RunAsync();
        _timer.Start();
        _ = RunAsync();
    }

    private static async Task RunAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<BackupService>().RunDailyIfDueAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Automatic backup failed");
        }
    }
}
