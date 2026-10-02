using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public class LogRow
{
    public string When { get; init; } = string.Empty;
    public string TypeText { get; init; } = string.Empty;
    public Brush TypeBrush { get; init; } = Brushes.Gray;
    public string Extra { get; init; } = string.Empty;
}

/// <summary>A member's full performance in one club.</summary>
public partial class ClubDetailViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly int _memberId;
    private readonly int _clubId;
    private readonly DispatcherTimer _idle;

    public ClubDetailViewModel(ShellViewModel shell, int memberId, int clubId)
    {
        _shell = shell;
        _memberId = memberId;
        _clubId = clubId;
        _idle = new DispatcherTimer { Interval = MemberClubsViewModel.IdleTimeout };
        _idle.Tick += (_, _) => Done();
        _idle.Start();
        _ = LoadAsync();
    }

    public ObservableCollection<LogRow> Recent { get; } = new();

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _clubName = string.Empty;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private Brush _statusBrush = Brushes.Gray;
    [ObservableProperty] private string _badgeText = string.Empty;
    [ObservableProperty] private Brush _badgeBrush = Brushes.Gray;
    [ObservableProperty] private string _monthVisits = "-";
    [ObservableProperty] private string _monthTime = "-";
    [ObservableProperty] private string _averageTime = "-";
    [ObservableProperty] private string _totalVisits = "-";
    [ObservableProperty] private string _totalTime = "-";
    [ObservableProperty] private string _lastEntry = "-";
    [ObservableProperty] private bool _hasNoEntries;

    private static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}h {t.Minutes:00}m";

    private async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var attendance = scope.ServiceProvider.GetRequiredService<AttendanceService>();

        var mem = await db.Members.AsNoTracking().FirstAsync(x => x.Id == _memberId);
        FullName = mem.FullName;
        BadgeText = mem.Badge.ToString().ToUpperInvariant();
        BadgeBrush = MemberClubsViewModel.BadgeBrushFor(mem.Badge);

        var s = await attendance.GetClubStatsAsync(_memberId, _clubId);
        if (s is null) return;

        ClubName = s.ClubName;
        StatusText = s.IsInside ? "INSIDE NOW" : "NOT INSIDE";
        StatusBrush = s.IsInside ? Brushes.SeaGreen : Brushes.Gray;
        MonthVisits = s.MonthVisits.ToString();
        MonthTime = Fmt(s.MonthTime);
        // Average only over finished visits: an open visit has no time counted yet.
        var openThisMonth = s.IsInside && s.LastEntry is DateTime le && le >= new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var finished = s.MonthVisits - (openThisMonth ? 1 : 0);
        AverageTime = finished > 0 ? Fmt(s.MonthTime / finished) : "-";
        TotalVisits = s.TotalVisits.ToString();
        TotalTime = Fmt(s.TotalTime);
        LastEntry = s.LastEntry is DateTime d ? d.ToString("dd MMM, hh:mm tt") : "-";
        HasNoEntries = s.Recent.Count == 0;

        foreach (var r in s.Recent)
        {
            var isIn = r.Type == AttendanceType.CheckIn;
            Recent.Add(new LogRow
            {
                When = r.Timestamp.ToString("ddd, dd MMM yyyy   hh:mm tt"),
                TypeText = isIn ? "CHECK-IN" : "CHECK-OUT",
                TypeBrush = isIn ? Brushes.SeaGreen : Brushes.Firebrick,
                Extra = r.Method + (r.IsManualEntry ? " - time entered by member" : string.Empty)
            });
        }
    }

    [RelayCommand]
    private void Back()
    {
        _idle.Stop();
        _shell.NavigateTo(new MemberClubsViewModel(_shell, _memberId));
    }

    [RelayCommand]
    private void Done()
    {
        _idle.Stop();
        _shell.NavigateTo(new KioskViewModel(_shell, string.Empty));
    }
}


