using Microsoft.EntityFrameworkCore;

namespace HobbiesClub.Data;

public static class DatabaseInitializer
{
    /// <summary>Applies pending migrations at app start (creates the database if missing).</summary>
    public static void Initialize(AppDbContext db)
    {
        db.Database.Migrate();
    }
}
