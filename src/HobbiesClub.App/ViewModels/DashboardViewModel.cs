using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.App.Views;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public class StatTile
{
    public string Key { get; init; } = string.Empty;
    public string Label { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

public class DashClubRow
{
    public string Name { get; init; } = string.Empty;
    public int Inside { get; init; }
    public int Members { get; init; }
    public int TodayCheckIns { get; init; }
}

public class DashInsideRow
{
    public int MemberCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ClubName { get; init; } = string.Empty;
    public string Since { get; init; } = string.Empty;
    public string InsideFor { get; init; } = string.Empty;
    public string Note { get; init; } = string.Empty;
    public string Initial { get; init; } = string.Empty;
    public ImageSource? Photo { get; init; }
}

/// <summary>One line in a tile's detail window.</summary>
public class DetailRow
{
    public int MemberCode { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Club { get; init; } = string.Empty;
    public string When { get; init; } = string.Empty;
    public string Info { get; init; } = string.Empty;
    public string Initial { get; init; } = string.Empty;
    public ImageSource? Photo { get; init; }
}

/// <summary>Live overview: who is inside which club, headline numbers with drill-down. Refreshes every 30 seconds.</summary>
public partial class DashboardViewModel : ObservableObject
{
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<string, List<DetailRow>> _details = new();
    private bool _busy;

    public DashboardViewModel()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => _ = RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    public void Stop() => _timer.Stop();

    public ObservableCollection<StatTile> Tiles { get; } = new();
    public ObservableCollection<DashClubRow> ClubRows { get; } = new();
    public ObservableCollection<DashInsideRow> Inside { get; } = new();
    public ObservableCollection<DashInsideRow> NotInside { get; } = new();

    [ObservableProperty] private string _updatedText = string.Empty;

    private static string InitialOf(Member m) =>
        m.PhotoJpeg is { Length: > 0 } || m.FullName.Length == 0 ? string.Empty : m.FullName.Substring(0, 1).ToUpperInvariant();

    private static string Fmt(TimeSpan t) => $"{(int)t.TotalHours}h {t.Minutes:00}m";

    [RelayCommand]
    private void OpenTile(StatTile tile)
    {
        if (!_details.TryGetValue(tile.Key, out var rows)) return;
        new DetailDialog(tile.Label, rows) { Owner = Application.Current.MainWindow }.ShowDialog();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (_busy) return;
        _busy = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var now = DateTime.Now;
            var today = now.Date;
            var monthStart = new DateTime(now.Year, now.Month, 1);

            var members = await db.Members.AsNoTracking().OrderBy(m => m.MemberCode).ToListAsync();
            var clubs = await db.Clubs.AsNoTracking().OrderBy(c => c.Id).ToListAsync();
            var links = await db.MemberClubs.AsNoTracking().Select(mc => new { mc.MemberId, mc.ClubId }).ToListAsync();
            var logs = await db.AttendanceLogs.AsNoTracking()
                .Select(a => new { a.Id, a.MemberId, a.ClubId, a.Type, a.Timestamp, a.Method, a.IsManualEntry })
                .ToListAsync();

            var memberById = members.ToDictionary(m => m.Id);
            var clubById = clubs.ToDictionary(c => c.Id);
            string ClubName(int id) => clubById.TryGetValue(id, out var c) ? c.Name : "?";
            string ClubsOf(int memberId) => string.Join(", ", links.Where(l => l.MemberId == memberId).OrderBy(l => l.ClubId).Select(l => ClubName(l.ClubId)));

            DetailRow Row(Member m, string club, string when, string info) => new()
            {
                MemberCode = m.MemberCode, Name = m.FullName, Club = club, When = when, Info = info,
                Photo = PhotoHelper.ToImage(m.PhotoJpeg), Initial = InitialOf(m)
            };

            // Inside now = the latest entry of a member in a club is a check-in.
            var insideNow = logs
                .GroupBy(l => (l.MemberId, l.ClubId))
                .Select(g => g.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id).First())
                .Where(x => x.Type == AttendanceType.CheckIn && memberById.ContainsKey(x.MemberId) && clubById.ContainsKey(x.ClubId))
                .OrderBy(x => ClubName(x.ClubId)).ThenBy(x => x.Timestamp)
                .ToList();
            var insideMemberIds = insideNow.Select(x => x.MemberId).ToHashSet();

            var lastByMember = logs
                .GroupBy(l => l.MemberId)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.Timestamp).ThenByDescending(x => x.Id).First());

            var active = members.Where(m => m.Status == MemberStatus.Active).ToList();
            var terminated = members.Where(m => m.Status != MemberStatus.Active).ToList();
            var todayIns = logs.Where(l => l.Type == AttendanceType.CheckIn && l.Timestamp >= today && memberById.ContainsKey(l.MemberId)).OrderByDescending(l => l.Timestamp).ToList();
            var todayOuts = logs.Where(l => l.Type == AttendanceType.CheckOut && l.Timestamp >= today && memberById.ContainsKey(l.MemberId)).OrderByDescending(l => l.Timestamp).ToList();
            var monthIns = logs.Where(l => l.Type == AttendanceType.CheckIn && l.Timestamp >= monthStart && memberById.ContainsKey(l.MemberId)).OrderByDescending(l => l.Timestamp).ToList();
            var missed = insideNow.Where(x => x.Timestamp.Date < today).ToList();

            string MethodText(VerificationMethod method, bool manual) => method + (manual ? " - time entered by member" : string.Empty);

            _details.Clear();
            _details["inside"] = insideNow.Select(x => Row(memberById[x.MemberId], ClubName(x.ClubId),
                x.Timestamp.ToString("dd MMM, hh:mm tt"),
                "Inside for " + Fmt(now - x.Timestamp) + (x.Timestamp.Date < today ? " - missed check-out?" : string.Empty))).ToList();
            _details["active"] = active.Select(m =>
            {
                var last = lastByMember.TryGetValue(m.Id, out var l) ? l : null;
                var lastText = last is null ? "No entries yet" : last.Timestamp.ToString("dd MMM, hh:mm tt");
                var status = insideMemberIds.Contains(m.Id)
                    ? "Inside now (" + string.Join(", ", insideNow.Where(x => x.MemberId == m.Id).Select(x => ClubName(x.ClubId))) + ")"
                    : "Not inside";
                return Row(m, ClubsOf(m.Id), lastText, status);
            }).ToList();
            _details["terminated"] = terminated.Select(m => Row(m, ClubsOf(m.Id),
                m.TerminatedAt?.ToString("dd MMM yyyy") ?? string.Empty, m.TerminationReason ?? string.Empty)).ToList();
            _details["in_today"] = todayIns.Select(l => Row(memberById[l.MemberId], ClubName(l.ClubId),
                l.Timestamp.ToString("hh:mm tt"), "CHECK-IN by " + MethodText(l.Method, l.IsManualEntry))).ToList();
            _details["out_today"] = todayOuts.Select(l => Row(memberById[l.MemberId], ClubName(l.ClubId),
                l.Timestamp.ToString("hh:mm tt"), "CHECK-OUT by " + MethodText(l.Method, l.IsManualEntry))).ToList();
            _details["month"] = monthIns.Select(l => Row(memberById[l.MemberId], ClubName(l.ClubId),
                l.Timestamp.ToString("dd MMM, hh:mm tt"), "CHECK-IN by " + MethodText(l.Method, l.IsManualEntry))).ToList();
            _details["missed"] = missed.Select(x => Row(memberById[x.MemberId], ClubName(x.ClubId),
                x.Timestamp.ToString("dd MMM, hh:mm tt"), "Checked in, no check-out yet")).ToList();

            Tiles.Clear();
            Tiles.Add(new StatTile { Key = "inside", Label = "Inside now", Value = insideNow.Count.ToString() });
            Tiles.Add(new StatTile { Key = "active", Label = "Active members", Value = active.Count.ToString() });
            Tiles.Add(new StatTile { Key = "terminated", Label = "Terminated", Value = terminated.Count.ToString() });
            Tiles.Add(new StatTile { Key = "in_today", Label = "Check-ins today", Value = todayIns.Count.ToString() });
            Tiles.Add(new StatTile { Key = "out_today", Label = "Check-outs today", Value = todayOuts.Count.ToString() });
            Tiles.Add(new StatTile { Key = "month", Label = "Visits this month", Value = monthIns.Count.ToString() });
            Tiles.Add(new StatTile { Key = "missed", Label = "Missed check-outs", Value = missed.Count.ToString() });

            ClubRows.Clear();
            foreach (var c in clubs.Where(c => c.IsActive))
            {
                ClubRows.Add(new DashClubRow
                {
                    Name = c.Name,
                    Inside = insideNow.Count(x => x.ClubId == c.Id),
                    Members = links.Count(l => l.ClubId == c.Id && memberById.TryGetValue(l.MemberId, out var mm) && mm.Status == MemberStatus.Active),
                    TodayCheckIns = todayIns.Count(l => l.ClubId == c.Id)
                });
            }

            Inside.Clear();
            foreach (var x in insideNow)
            {
                var m = memberById[x.MemberId];
                Inside.Add(new DashInsideRow
                {
                    MemberCode = m.MemberCode,
                    Name = m.FullName,
                    ClubName = ClubName(x.ClubId),
                    Since = x.Timestamp.ToString("dd MMM, hh:mm tt"),
                    InsideFor = Fmt(now - x.Timestamp),
                    Note = x.Timestamp.Date < today ? "Missed check-out?" : string.Empty,
                    Photo = PhotoHelper.ToImage(m.PhotoJpeg),
                    Initial = InitialOf(m)
                });
            }

            NotInside.Clear();
            foreach (var m in active.Where(m => !insideMemberIds.Contains(m.Id)))
            {
                var last = lastByMember.TryGetValue(m.Id, out var l) ? l : null;
                NotInside.Add(new DashInsideRow
                {
                    MemberCode = m.MemberCode,
                    Name = m.FullName,
                    ClubName = ClubsOf(m.Id),
                    Since = last is null ? "Never" : last.Timestamp.ToString("dd MMM, hh:mm tt"),
                    InsideFor = last is null ? string.Empty
                        : (last.Type == AttendanceType.CheckIn ? "CHECK-IN" : "CHECK-OUT") + " at " + ClubName(last.ClubId),
                    Photo = PhotoHelper.ToImage(m.PhotoJpeg),
                    Initial = InitialOf(m)
                });
            }

            UpdatedText = "Updated " + now.ToString("hh:mm:ss tt") + " (auto-refresh every 30 seconds). Click a card for details.";
        }
        catch (Exception ex)
        {
            UpdatedText = "Could not load: " + ex.Message;
        }
        finally
        {
            _busy = false;
        }
    }
}
