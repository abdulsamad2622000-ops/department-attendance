using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace HobbiesClub.Services;

/// <summary>Database backups (safe while the app is running), cleanup and restore.</summary>
public class BackupService
{
    public const int KeepDays = 30;

    /// <summary>Set when the last backup could not be copied to the safe copy folder.</summary>
    public string? ExternalWarning { get; private set; }

    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public BackupService(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<OperationResult> CreateBackupAsync(BackupKind kind, string actorUsername, int? actorUserId)
    {
        var now = _clock.Now;
        Directory.CreateDirectory(AppPaths.BackupFolder);
        var file = Path.Combine(AppPaths.BackupFolder, $"hobbies-{now:yyyyMMdd-HHmmss}-{kind}.db");
        var history = new BackupHistory { Kind = kind, FilePath = file, CreatedAt = now, CreatedByUserId = actorUserId };

        try
        {
            using (var source = new SqliteConnection(AppPaths.ConnectionString))
            using (var target = new SqliteConnection($"Data Source={file};Pooling=False"))
            {
                source.Open();
                target.Open();
                source.BackupDatabase(target);

                // Keep the backup a single self-contained file (no -wal / -shm files next to it).
                using var pragma = target.CreateCommand();
                pragma.CommandText = "PRAGMA journal_mode=DELETE;";
                pragma.ExecuteScalar();
            }
            DeleteSidecars(file);
            history.SizeBytes = new FileInfo(file).Length;
            history.IsSuccessful = true;
            ExternalWarning = kind == BackupKind.PreRestore ? null : CopyToExternal(file);
        }
        catch (Exception ex)
        {
            history.IsSuccessful = false;
            history.ErrorMessage = ex.Message;
        }

        _db.BackupHistories.Add(history);
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = now,
            UserId = actorUserId,
            Username = actorUsername,
            Action = history.IsSuccessful ? "BackupCreated" : "BackupFailed",
            Details = $"{kind}: {Path.GetFileName(file)}" + (history.IsSuccessful ? string.Empty : " - " + history.ErrorMessage)
        });
        await _db.SaveChangesAsync();

        return history.IsSuccessful
            ? new OperationResult(true)
            : new OperationResult(false, "Backup failed: " + history.ErrorMessage);
    }

    /// <summary>Copies a backup to the safe copy folder (USB, OneDrive, Google Drive). Never throws. Returns a warning text or null.</summary>
    public static string? CopyToExternal(string file)
    {
        var folder = BackupSettings.ExternalFolder;
        if (string.IsNullOrWhiteSpace(folder)) return null;
        try
        {
            if (!Directory.Exists(folder))
                return "Safe copy folder not found (is the USB plugged in?): " + folder;

            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)), overwrite: true);

            var cutoff = DateTime.Now.AddDays(-KeepDays);
            foreach (var old in Directory.GetFiles(folder, "hobbies-*.db"))
            {
                if (File.GetLastWriteTime(old) >= cutoff) continue;
                try { File.Delete(old); } catch { /* in use; try next time */ }
            }
            return null;
        }
        catch (Exception ex)
        {
            return "Could not copy to the safe copy folder: " + ex.Message;
        }
    }

    /// <summary>Deletes backup files older than 30 days. Returns how many were deleted.</summary>
    public int DeleteOldBackups()
    {
        if (!Directory.Exists(AppPaths.BackupFolder)) return 0;
        var cutoff = _clock.Now.AddDays(-KeepDays);
        var deleted = 0;
        foreach (var f in Directory.GetFiles(AppPaths.BackupFolder, "hobbies-*.db"))
        {
            if (File.GetLastWriteTime(f) >= cutoff) continue;
            try { File.Delete(f); DeleteSidecars(f); deleted++; } catch { /* in use or locked; try next time */ }
        }
        return deleted;
    }

    /// <summary>
    /// The history table lives inside the database, so a restore rolls it back too.
    /// This adds a history row for every backup file in the folder that has none (time and type come from the file name).
    /// </summary>
    public async Task SyncHistoryFromFolderAsync()
    {
        if (!Directory.Exists(AppPaths.BackupFolder)) return;

        var paths = await _db.BackupHistories.AsNoTracking().Select(b => b.FilePath).ToListAsync();
        var known = new HashSet<string>(paths, StringComparer.OrdinalIgnoreCase);

        var added = false;
        foreach (var f in Directory.GetFiles(AppPaths.BackupFolder, "hobbies-*.db"))
        {
            if (known.Contains(f)) continue;
            if (!TryParseFileName(f, out var created, out var kind)) continue;

            _db.BackupHistories.Add(new BackupHistory
            {
                Kind = kind,
                FilePath = f,
                CreatedAt = created,
                SizeBytes = new FileInfo(f).Length,
                IsSuccessful = true
            });
            added = true;
        }
        if (added) await _db.SaveChangesAsync();
    }

    private static bool TryParseFileName(string path, out DateTime created, out BackupKind kind)
    {
        created = default;
        kind = default;
        // hobbies-20261001-164149-Manual
        var parts = Path.GetFileNameWithoutExtension(path).Split('-');
        return parts.Length == 4
            && DateTime.TryParseExact(parts[1] + parts[2], "yyyyMMddHHmmss", CultureInfo.InvariantCulture, DateTimeStyles.None, out created)
            && Enum.TryParse(parts[3], out kind);
    }

    private static void DeleteSidecars(string dbFile)
    {
        foreach (var ext in new[] { "-wal", "-shm" })
        {
            try { File.Delete(dbFile + ext); } catch { /* read-only location or in use */ }
        }
    }

    public Task<BackupHistory?> GetLastSuccessfulAsync() =>
        _db.BackupHistories.AsNoTracking()
            .Where(b => b.IsSuccessful && b.Kind != BackupKind.PreRestore)
            .OrderByDescending(b => b.CreatedAt)
            .FirstOrDefaultAsync();

    /// <summary>Makes today's automatic backup if there is none yet. Returns true if a backup was made.</summary>
    public async Task<bool> RunDailyIfDueAsync()
    {
        await SyncHistoryFromFolderAsync();
        var last = await GetLastSuccessfulAsync();
        if (last is not null && last.CreatedAt.Date >= _clock.Now.Date)
            return false;

        await CreateBackupAsync(BackupKind.Automatic, "system", null);
        DeleteOldBackups();
        return true;
    }

    /// <summary>Restores a backup file over the live database. A safety backup is made first.</summary>
    public async Task<OperationResult> RestoreAsync(string backupFile, string actorUsername, int? actorUserId)
    {
        if (!File.Exists(backupFile))
            return new OperationResult(false, "Backup file not found.");

        try
        {
            using var test = new SqliteConnection($"Data Source={backupFile};Mode=ReadOnly;Pooling=False");
            test.Open();
            using var cmd = test.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Members";
            cmd.ExecuteScalar();
        }
        catch
        {
            return new OperationResult(false, "This file is not a valid Departments Attendance backup.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteSidecars(backupFile);
        }

        var safety = await CreateBackupAsync(BackupKind.PreRestore, actorUsername, actorUserId);
        if (!safety.Succeeded)
            return new OperationResult(false, "Safety backup failed, so the restore was cancelled.");

        try
        {
            SqliteConnection.ClearAllPools();
            using var source = new SqliteConnection($"Data Source={backupFile};Mode=ReadOnly;Pooling=False");
            using var target = new SqliteConnection(AppPaths.ConnectionString);
            source.Open();
            target.Open();
            source.BackupDatabase(target);
        }
        catch (Exception ex)
        {
            return new OperationResult(false, "Restore failed: " + ex.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            DeleteSidecars(backupFile);
        }

        _db.ChangeTracker.Clear();
        _db.AuditLogs.Add(new AuditLog
        {
            Timestamp = _clock.Now,
            UserId = actorUserId,
            Username = actorUsername,
            Action = "BackupRestored",
            Details = Path.GetFileName(backupFile)
        });
        await _db.SaveChangesAsync();
        return new OperationResult(true);
    }
}


