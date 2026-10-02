using Microsoft.Data.Sqlite;

namespace HobbiesClub.Tests;

public class DevToolsShowLogs
{
    [Fact]
    public void Show_attendance_logs()
    {
        if (Environment.GetEnvironmentVariable("SHOWLOGS") != "1") return;

        using var c = new SqliteConnection("Data Source=C:\\ProgramData\\HobbiesClub\\Data\\hobbies.db;Mode=ReadOnly");
        c.Open();
        var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT Id, MemberId, ClubId, Type, Timestamp, IsManualEntry, Note FROM AttendanceLogs ORDER BY Id";
        using var r = cmd.ExecuteReader();
        while (r.Read())
            Console.WriteLine($"ROW {r[0]} | member {r[1]} | club {r[2]} | type {r[3]} | {r[4]} | manual {r[5]} | {r[6]}");
    }
}
