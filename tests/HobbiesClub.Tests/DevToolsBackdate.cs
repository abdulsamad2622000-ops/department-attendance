using Microsoft.Data.Sqlite;

namespace HobbiesClub.Tests;

public class DevToolsBackdate
{
    [Fact]
    public void Make_open_checkin_yesterday()
    {
        if (Environment.GetEnvironmentVariable("BACKDATE") != "1") return;

        using var c = new SqliteConnection("Data Source=C:\\ProgramData\\HobbiesClub\\Data\\hobbies.db");
        c.Open();

        var del = c.CreateCommand();
        del.CommandText = "DELETE FROM AttendanceLogs WHERE MemberId = 1";
        Console.WriteLine($"DELETED rows: {del.ExecuteNonQuery()}");

        var club = c.CreateCommand();
        club.CommandText = "SELECT ClubId FROM MemberClubs WHERE MemberId = 1 ORDER BY ClubId LIMIT 1";
        var clubId = Convert.ToInt32(club.ExecuteScalar());

        var yesterday = DateTime.Now.Date.AddDays(-1).AddHours(9).ToString("yyyy-MM-dd HH:mm:ss");
        var ins = c.CreateCommand();
        ins.CommandText = "INSERT INTO AttendanceLogs (MemberId, ClubId, Type, Method, Timestamp, IsManualEntry) VALUES (1, $club, 1, 3, $ts, 0)";
        ins.Parameters.AddWithValue("$club", clubId);
        ins.Parameters.AddWithValue("$ts", yesterday);
        Console.WriteLine($"INSERTED open check-in: {ins.ExecuteNonQuery()} at {yesterday}, club {clubId}");
    }
}
