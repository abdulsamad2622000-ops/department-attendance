namespace HobbiesClub.Data;

public static class AppPaths
{
    public static string Root => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "HobbiesClub");

    public static string DataFolder => Path.Combine(Root, "Data");
    public static string BackupFolder => Path.Combine(Root, "Backups");
    public static string LogFolder => Path.Combine(Root, "Logs");

    public static string DatabasePath => Path.Combine(DataFolder, "hobbies.db");
    public static string ConnectionString => $"Data Source={DatabasePath}";

    public static void EnsureFolders()
    {
        Directory.CreateDirectory(DataFolder);
        Directory.CreateDirectory(BackupFolder);
        Directory.CreateDirectory(LogFolder);
    }
}
