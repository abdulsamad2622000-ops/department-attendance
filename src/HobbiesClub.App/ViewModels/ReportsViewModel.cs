using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace HobbiesClub.App.ViewModels;

public class LogRowVm
{
    public string Date { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Club { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public string Method { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
}

public class MemberRowVm
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Clubs { get; init; } = string.Empty;
    public string Visits { get; init; } = string.Empty;
    public string TotalTime { get; init; } = string.Empty;
    public string AverageTime { get; init; } = string.Empty;
    public string LastVisit { get; init; } = string.Empty;
}

public class DailyRowVm
{
    public string Date { get; init; } = string.Empty;
    public string Members { get; init; } = string.Empty;
    public string CheckIns { get; init; } = string.Empty;
    public string CheckOuts { get; init; } = string.Empty;
    public string ByClub { get; init; } = string.Empty;
}

/// <summary>Admin panel: attendance reports with Excel (CSV) export.</summary>
public partial class ReportsViewModel : ObservableObject
{
    public const string LogReport = "Attendance log";
    public const string MemberReport = "Member summary";
    public const string DailyReport = "Daily summary";

    public ReportsViewModel()
    {
        _from = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _to = DateTime.Today;
        _ = LoadClubsAsync();
    }

    public string[] ReportTypes { get; } = { LogReport, MemberReport, DailyReport };
    public ObservableCollection<ClubItem> Clubs { get; } = new();
    public ObservableCollection<LogRowVm> LogRows { get; } = new();
    public ObservableCollection<MemberRowVm> MemberRows { get; } = new();
    public ObservableCollection<DailyRowVm> DailyRows { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLog), nameof(IsMember), nameof(IsDaily))]
    private string _reportType = LogReport;

    [ObservableProperty] private DateTime? _from;
    [ObservableProperty] private DateTime? _to;
    [ObservableProperty] private ClubItem? _club;
    [ObservableProperty] private string _search = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    public bool IsLog => ReportType == LogReport;
    public bool IsMember => ReportType == MemberReport;
    public bool IsDaily => ReportType == DailyReport;

    partial void OnReportTypeChanged(string value) => _ = GenerateAsync();

    private static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}h {t.Minutes:00}m";

    private async Task LoadClubsAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = await db.Clubs.AsNoTracking().Where(c => c.IsActive).OrderBy(c => c.Id)
            .Select(c => new ClubItem(c.Id, c.Name)).ToListAsync();

        Clubs.Clear();
        Clubs.Add(new ClubItem(0, "All departments"));
        foreach (var c in list) Clubs.Add(c);
        Club = Clubs[0];
        await GenerateAsync();
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        Error = string.Empty;
        var from = (From ?? DateTime.Today).Date;
        var to = (To ?? DateTime.Today).Date;
        if (to < from)
        {
            Error = "'To' date cannot be before 'From' date.";
            return;
        }

        int? clubId = Club is { Id: > 0 } c ? c.Id : null;

        using var scope = App.Services.CreateScope();
        var reports = scope.ServiceProvider.GetRequiredService<ReportService>();

        if (IsLog)
        {
            var rows = await reports.AttendanceLogAsync(from, to, clubId, Search);
            LogRows.Clear();
            foreach (var r in rows)
                LogRows.Add(new LogRowVm
                {
                    Date = r.Time.ToString("dd MMM yyyy"),
                    Time = r.Time.ToString("hh:mm tt"),
                    Id = r.MemberCode.ToString(),
                    Name = r.Name,
                    Club = r.Club,
                    Type = r.Type == AttendanceType.CheckIn ? "CHECK-IN" : "CHECK-OUT",
                    Method = r.Method.ToString(),
                    Note = r.IsManualEntry ? "Time entered by member" + (string.IsNullOrWhiteSpace(r.Note) || r.Note == "Late check-out entered by member" ? string.Empty : " - " + r.Note) : (r.Note ?? string.Empty)
                });
            Summary = $"{rows.Count} entries";
        }
        else if (IsMember)
        {
            var rows = await reports.MemberSummaryAsync(from, to, clubId, Search);
            MemberRows.Clear();
            foreach (var r in rows)
                MemberRows.Add(new MemberRowVm
                {
                    Id = r.MemberCode.ToString(),
                    Name = r.Name,
                    Clubs = r.Clubs,
                    Visits = r.Visits.ToString(),
                    TotalTime = Fmt(r.TotalTime),
                    AverageTime = Fmt(r.AverageTime),
                    LastVisit = r.LastVisit.ToString("dd MMM yyyy, hh:mm tt")
                });
            Summary = $"{rows.Count} members, {rows.Sum(r => r.Visits)} visits";
        }
        else
        {
            var rows = await reports.DailySummaryAsync(from, to, clubId, Search);
            DailyRows.Clear();
            foreach (var r in rows)
                DailyRows.Add(new DailyRowVm
                {
                    Date = r.Date.ToString("ddd, dd MMM yyyy"),
                    Members = r.Members.ToString(),
                    CheckIns = r.CheckIns.ToString(),
                    CheckOuts = r.CheckOuts.ToString(),
                    ByClub = r.ByClub
                });
            Summary = $"{rows.Count} days, {rows.Sum(r => r.CheckIns)} check-ins";
        }
    }

    [RelayCommand]
    private void ExportCsv()
    {
        Error = string.Empty;
        Message = string.Empty;

        string csv;
        int count;
        if (IsLog)
        {
            csv = ReportService.ToCsv(
                new[] { "Date", "Time", "Member ID", "Name", "Department", "Type", "Method", "Note" },
                LogRows.Select(r => new[] { r.Date, r.Time, r.Id, r.Name, r.Club, r.Type, r.Method, r.Note }));
            count = LogRows.Count;
        }
        else if (IsMember)
        {
            csv = ReportService.ToCsv(
                new[] { "Member ID", "Name", "Departments", "Visits", "Total time", "Average per visit", "Last visit" },
                MemberRows.Select(r => new[] { r.Id, r.Name, r.Clubs, r.Visits, r.TotalTime, r.AverageTime, r.LastVisit }));
            count = MemberRows.Count;
        }
        else
        {
            csv = ReportService.ToCsv(
                new[] { "Date", "Members", "Check-ins", "Check-outs", "By department" },
                DailyRows.Select(r => new[] { r.Date, r.Members, r.CheckIns, r.CheckOuts, r.ByClub }));
            count = DailyRows.Count;
        }

        if (count == 0)
        {
            Error = "Nothing to export. Generate a report first.";
            return;
        }

        var from = (From ?? DateTime.Today).ToString("yyyyMMdd");
        var to = (To ?? DateTime.Today).ToString("yyyyMMdd");
        var dlg = new SaveFileDialog
        {
            Title = "Save report",
            Filter = "Excel (CSV)|*.csv",
            FileName = $"{ReportType.Replace(" ", "-")}-{from}-{to}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            File.WriteAllText(dlg.FileName, csv, new UTF8Encoding(true));
            Message = "Saved: " + dlg.FileName;
            Process.Start(new ProcessStartInfo(dlg.FileName) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Error = "Could not save: " + ex.Message;
        }
    }
}


