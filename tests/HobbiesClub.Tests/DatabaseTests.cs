using HobbiesClub.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Tests;

public class DatabaseTests
{
    [Fact]
    public void Migration_creates_database_with_two_seeded_clubs()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        using var db = new AppDbContext(options);
        DatabaseInitializer.Initialize(db);

        var names = db.Clubs.OrderBy(c => c.Id).Select(c => c.Name).ToArray();

        Assert.Equal(new[] { "AI Club", "Hobbies Club" }, names);
    }
}
