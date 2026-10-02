using System.Text.Json;
using HobbiesClub.Data;

namespace HobbiesClub.Services;

/// <summary>Small settings file kept outside the database, so it survives a restore.</summary>
public static class BackupSettings
{
    private sealed class Data { public string? ExternalFolder { get; set; } }

    private static string FilePath => Path.Combine(AppPaths.Root, "backup-settings.json");

    /// <summary>Folder (USB, OneDrive, Google Drive) where a safe copy of every backup is placed.</summary>
    public static string? ExternalFolder
    {
        get
        {
            try
            {
                if (!File.Exists(FilePath)) return null;
                return JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath))?.ExternalFolder;
            }
            catch { return null; }
        }
        set
        {
            try
            {
                Directory.CreateDirectory(AppPaths.Root);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(new Data { ExternalFolder = value }));
            }
            catch { /* settings are optional */ }
        }
    }
}
