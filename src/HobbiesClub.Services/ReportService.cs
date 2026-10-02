using System.Text;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Services;

public record LogReportRow(DateTime Time, int MemberCode, string Name, string Club, AttendanceType Type, VerificationMethod Method, bool IsManualEntry);
public record MemberReportRow(int MemberCode, string Name, string Clubs, int Visits, TimeSpan TotalTime, TimeSpan AverageTime, DateTime LastVisit);
public record DailyReportRow(DateTime Date, int Members, int CheckIns, int CheckOuts, string ByClub);

/// <summary>Attendance reports for a date range, optional club and member filter.</summary>
public class ReportService
{
    private readonly AppDbContext _db;

    public ReportService(AppDbContext db) => _db = db;

    private async Task<(List<AttendanceLog> Logs, Dictionary<int, Member> Members, Dictionary<int, string> Clubs)> LoadAsync(
        DateTime from, DateTime to, int? clubId, string? search)
    {
        var start = from.Date;
        var end = to.Date.AddDays(1);

        var members = await _db.Members.AsNoTracking().ToDictionaryAsync(m => m.Id);
        var clubs = await _db.Clubs.AsNoTracking().ToDictionaryAsync(c => c.Id, c => c.Name);

        var q = _db.AttendanceLogs.AsNoTracking().Where(a => a.Timestamp >= start && a.Timestamp < end);
        if (clubId is int c)
            q = q.Where(a => a.ClubId == c);

        var logs = await q.OrderBy(a => a.Timestamp).ThenBy(a => a.Id).ToListAsync();
        logs = logs.Where(l => members.ContainsKey(l.MemberId)).ToList();

        var s = search?.Trim();
        if (!string.IsNullOrEmpty(s))
            logs = logs.Where(l =>
            {
                var m = members[l.MemberId];
                return m.FullName.Contains(s, StringComparison.OrdinalIgnoreCase)
                    || m.MemberCode.ToString() == s
                    || m.Phone.Contains(s);
            }).ToList();

        return (logs, members, clubs);
    }

    private static string ClubName(Dictionary<int, string> clubs, int id) => clubs.TryGetValue(id, out var n) ? n : "?";

    public async Task<List<LogReportRow>> AttendanceLogAsync(DateTime from, DateTime to, int? clubId, string? search)
    {
        var (logs, members, clubs) = await LoadAsync(from, to, clubId, search);
        return logs.Select(l => new LogReportRow(
            l.Timestamp, members[l.MemberId].MemberCode, members[l.MemberId].FullName,
            ClubName(clubs, l.ClubId), l.Type, l.Method, l.IsManualEntry)).ToList();
    }

    /// <summary>Visits = check-ins. Time = each check-in paired with the next check-out in the same club.</summary>
    public async Task<List<MemberReportRow>> MemberSummaryAsync(DateTime from, DateTime to, int? clubId, string? search)
    {
        var (logs, members, clubs) = await LoadAsync(from, to, clubId, search);
        var rows = new List<MemberReportRow>();

        foreach (var g in logs.GroupBy(l => l.MemberId))
        {
            var total = TimeSpan.Zero;
            var pairs = 0;
            foreach (var cg in g.GroupBy(x => x.ClubId))
            {
                DateTime? open = null;
                foreach (var x in cg)
                {
                    if (x.Type == AttendanceType.CheckIn)
                        open = x.Timestamp;
                    else if (open is DateTime o)
                    {
                        total += x.Timestamp - o;
                        pairs++;
                        open = null;
                    }
                }
            }

            var m = members[g.Key];
            rows.Add(new MemberReportRow(
                m.MemberCode, m.FullName,
                string.Join(", ", g.Select(x => x.ClubId).Distinct().OrderBy(id => id).Select(id => ClubName(clubs, id))),
                g.Count(x => x.Type == AttendanceType.CheckIn),
                total,
                pairs == 0 ? TimeSpan.Zero : TimeSpan.FromTicks(total.Ticks / pairs),
                g.Max(x => x.Timestamp)));
        }

        return rows.OrderBy(r => r.MemberCode).ToList();
    }

    public async Task<List<DailyReportRow>> DailySummaryAsync(DateTime from, DateTime to, int? clubId, string? search)
    {
        var (logs, _, clubs) = await LoadAsync(from, to, clubId, search);
        return logs.GroupBy(l => l.Timestamp.Date).OrderBy(g => g.Key).Select(g => new DailyReportRow(
            g.Key,
            g.Select(x => x.MemberId).Distinct().Count(),
            g.Count(x => x.Type == AttendanceType.CheckIn),
            g.Count(x => x.Type == AttendanceType.CheckOut),
            string.Join(", ", g.Where(x => x.Type == AttendanceType.CheckIn).GroupBy(x => x.ClubId)
                .OrderBy(cg => cg.Key).Select(cg => $"{ClubName(clubs, cg.Key)}: {cg.Count()}"))))
            .ToList();
    }

    /// <summary>Builds CSV text that Excel opens correctly.</summary>
    public static string ToCsv(IEnumerable<string> headers, IEnumerable<IEnumerable<string>> rows)
    {
        static string Esc(string v) => v.Contains(',') || v.Contains('"') || v.Contains('\n')
            ? "\"" + v.Replace("\"", "\"\"") + "\"" : v;

        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", headers.Select(Esc)));
        foreach (var r in rows)
            sb.AppendLine(string.Join(",", r.Select(v => Esc(v ?? string.Empty))));
        return sb.ToString();
    }
}
