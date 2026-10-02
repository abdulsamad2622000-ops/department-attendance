using Microsoft.Data.Sqlite;

namespace HobbiesClub.Tests;

public class DevToolsDeleteMember
{
    [Fact]
    public void Delete_member_by_code()
    {
        var code = Environment.GetEnvironmentVariable("DELMEMBER");
        if (string.IsNullOrEmpty(code)) return;

        using var c = new SqliteConnection("Data Source=C:\\ProgramData\\HobbiesClub\\Data\\hobbies.db");
        c.Open();

        var find = c.CreateCommand();
        find.CommandText = "SELECT Id, FullName FROM Members WHERE MemberCode = $code";
        find.Parameters.AddWithValue("$code", int.Parse(code));
        using var r = find.ExecuteReader();
        if (!r.Read()) { Console.WriteLine($"MEMBER {code} NOT FOUND"); return; }
        var id = r.GetInt32(0);
        var name = r.GetString(1);
        r.Close();

        foreach (var table in new[] { "AttendanceLogs", "BiometricTemplates", "MemberClubs" })
        {
            var del = c.CreateCommand();
            del.CommandText = $"DELETE FROM {table} WHERE MemberId = $id";
            del.Parameters.AddWithValue("$id", id);
            Console.WriteLine($"{table}: {del.ExecuteNonQuery()} deleted");
        }

        var m = c.CreateCommand();
        m.CommandText = "DELETE FROM Members WHERE Id = $id";
        m.Parameters.AddWithValue("$id", id);
        Console.WriteLine($"DELETED MEMBER {code} ({name}): {m.ExecuteNonQuery()}");
    }
}
