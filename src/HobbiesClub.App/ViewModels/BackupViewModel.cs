using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.App.Views;
using HobbiesClub.Core.Enums;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace HobbiesClub.App.ViewModels;

public class BackupRow
{
    public string FilePath { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public string Time { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Size { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool Exists { get; init; }
}

/// <summary>Admin panel: backup status, Backup now, safe copy folder, history and restore.</summary>
public partial class BackupViewModel : ObservableObject
{
    private readonly int _actorId;
    private readonly string _actorUsername;

    public BackupViewModel(int actorId, string actorUsername)
    {
        _actorId = actorId;
        _actorUsername = actorUsername;
        _ = LoadAsync();
    }

    public string FolderText => "Backups are saved in: " + AppPaths.BackupFolder + $" (older than {BackupService.KeepDays} days are deleted automatically)";

    public ObservableCollection<BackupRow> Rows { get; } = new();

    [ObservableProperty] private BackupRow? _selected;
    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private Brush _statusBrush = Brushes.Gray;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _error = string.Empty;
    [ObservableProperty] private bool _isIdle = true;
    [ObservableProperty] private string _externalText = string.Empty;
    [ObservableProperty] private Brush _externalBrush = Brushes.Gray;

    private static string KindText(BackupKind k) => k switch
    {
        BackupKind.Automatic => "Automatic",
        BackupKind.Manual => "Manual",
        BackupKind.PreRestore => "Safety (before restore)",
        _ => k.ToString()
    };

    private static string SizeText(long bytes) =>
        bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:0.0} MB" : $"{bytes / 1024d:0} KB";

    private void UpdateExternalStatus()
    {
        var folder = BackupSettings.ExternalFolder;
        if (string.IsNullOrWhiteSpace(folder))
        {
            ExternalText = "Safe copy folder: NOT SET. Choose a USB, OneDrive or Google Drive folder so backups survive a laptop crash.";
            ExternalBrush = Brushes.DarkOrange;
            return;
        }
        if (!Directory.Exists(folder))
        {
            ExternalText = "Safe copy folder not found (is the USB plugged in?): " + folder;
            ExternalBrush = Brushes.Firebrick;
            return;
        }
        var files = Directory.GetFiles(folder, "hobbies-*.db");
        var newest = files.Length == 0 ? (DateTime?)null : files.Max(f => File.GetLastWriteTime(f));
        ExternalText = "Safe copy folder: " + folder + (newest is DateTime d ? $" (last copy: {d:dd MMM yyyy, hh:mm tt})" : " (no copy yet)");
        ExternalBrush = Brushes.SeaGreen;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var backups = scope.ServiceProvider.GetRequiredService<BackupService>();

        await backups.SyncHistoryFromFolderAsync();
        UpdateExternalStatus();
        var last = await backups.GetLastSuccessfulAsync();
        if (last is null)
        {
            StatusText = "No backup has been made yet.";
            StatusBrush = Brushes.Firebrick;
        }
        else if (DateTime.Now - last.CreatedAt > TimeSpan.FromDays(1))
        {
            StatusText = $"Backup overdue! Last backup: {last.CreatedAt:dd MMM yyyy, hh:mm tt}";
            StatusBrush = Brushes.Firebrick;
        }
        else
        {
            StatusText = $"Last backup: {last.CreatedAt:dd MMM yyyy, hh:mm tt} ({KindText(last.Kind)}, {SizeText(last.SizeBytes)})";
            StatusBrush = Brushes.SeaGreen;
        }

        var list = await db.BackupHistories.AsNoTracking().OrderByDescending(b => b.CreatedAt).Take(300).ToListAsync();
        Rows.Clear();
        foreach (var b in list)
        {
            var exists = File.Exists(b.FilePath);
            Rows.Add(new BackupRow
            {
                FilePath = b.FilePath,
                FileName = Path.GetFileName(b.FilePath),
                Time = b.CreatedAt.ToString("dd MMM yyyy, hh:mm:ss tt"),
                Kind = KindText(b.Kind),
                Size = b.IsSuccessful ? SizeText(b.SizeBytes) : string.Empty,
                Status = !b.IsSuccessful ? "Failed: " + b.ErrorMessage : exists ? "OK" : "File deleted (older than 30 days)",
                Exists = b.IsSuccessful && exists
            });
        }
    }

    private void Clear()
    {
        Message = string.Empty;
        Error = string.Empty;
    }

    [RelayCommand]
    private async Task BackupNowAsync()
    {
        Clear();
        IsIdle = false;
        try
        {
            using var scope = App.Services.CreateScope();
            var backups = scope.ServiceProvider.GetRequiredService<BackupService>();
            var r = await backups.CreateBackupAsync(BackupKind.Manual, _actorUsername, _actorId);
            backups.DeleteOldBackups();
            if (r.Succeeded) Message = "Backup created."; else Error = r.Error ?? "Backup failed.";
            if (backups.ExternalWarning is string warn) Error = warn;
        }
        finally
        {
            IsIdle = true;
        }
        await LoadAsync();
    }

    [RelayCommand]
    private void ChooseExternalFolder()
    {
        Clear();
        var dlg = new OpenFolderDialog { Title = "Choose a USB drive, OneDrive or Google Drive folder for safe copies" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var probe = Path.Combine(dlg.FolderName, ".hobbies-write-test");
            File.WriteAllText(probe, "ok");
            File.Delete(probe);
        }
        catch (Exception ex)
        {
            Error = "Cannot write to that folder: " + ex.Message;
            return;
        }

        BackupSettings.ExternalFolder = dlg.FolderName;
        var latest = Directory.Exists(AppPaths.BackupFolder)
            ? Directory.GetFiles(AppPaths.BackupFolder, "hobbies-*.db").OrderByDescending(f => File.GetLastWriteTime(f)).FirstOrDefault()
            : null;
        var warn = latest is null ? null : BackupService.CopyToExternal(latest);
        UpdateExternalStatus();
        if (warn is not null) Error = warn; else Message = "Safe copy folder saved. Every new backup is copied there.";
    }

    [RelayCommand]
    private void ClearExternalFolder()
    {
        Clear();
        BackupSettings.ExternalFolder = null;
        UpdateExternalStatus();
        Message = "Safe copy folder removed.";
    }

    [RelayCommand]
    private void CopyToFolder()
    {
        Clear();
        if (Selected is not { Exists: true } row)
        {
            Error = "Select a backup in the list first.";
            return;
        }

        var dlg = new OpenFolderDialog { Title = "Choose a USB drive or folder" };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var target = Path.Combine(dlg.FolderName, row.FileName);
            File.Copy(row.FilePath, target, overwrite: true);
            Message = "Copied to " + target;
        }
        catch (Exception ex)
        {
            Error = "Copy failed: " + ex.Message;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        Directory.CreateDirectory(AppPaths.BackupFolder);
        Process.Start(new ProcessStartInfo("explorer.exe", AppPaths.BackupFolder) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task RestoreSelectedAsync()
    {
        Clear();
        if (Selected is not { Exists: true } row)
        {
            Error = "Select a backup in the list first.";
            return;
        }
        await RestoreAsync(row.FilePath, row.Time);
    }

    [RelayCommand]
    private async Task RestoreFromFileAsync()
    {
        Clear();
        var dlg = new OpenFileDialog { Title = "Choose a backup file", Filter = "Hobbies Club backup (*.db)|*.db" };
        if (dlg.ShowDialog() != true) return;
        await RestoreAsync(dlg.FileName, Path.GetFileName(dlg.FileName));
    }

    private async Task RestoreAsync(string file, string label)
    {
        var sure = MessageBox.Show(
            $"Restore the backup from {label}?\n\nAll data entered after that backup will be replaced. " +
            "A safety backup of the current data is made first.",
            "Restore backup", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (sure != MessageBoxResult.Yes) return;

        var typed = PromptDialog.Ask("Confirm restore", "Type RESTORE to continue:", 10);
        if (!string.Equals(typed, "RESTORE", StringComparison.Ordinal))
        {
            Error = "Restore cancelled.";
            return;
        }

        IsIdle = false;
        try
        {
            using var scope = App.Services.CreateScope();
            var r = await scope.ServiceProvider.GetRequiredService<BackupService>().RestoreAsync(file, _actorUsername, _actorId);
            if (!r.Succeeded)
            {
                Error = r.Error ?? "Restore failed.";
                return;
            }
        }
        finally
        {
            IsIdle = true;
        }

        MessageBox.Show("Backup restored. The app will now restart.", "Restore complete",
            MessageBoxButton.OK, MessageBoxImage.Information);
        if (Environment.ProcessPath is string exe)
            Process.Start(new ProcessStartInfo(exe) { UseShellExecute = true });
        Application.Current.Shutdown();
    }
}
