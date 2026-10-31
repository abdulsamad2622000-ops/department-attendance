using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using HobbiesClub.App.ViewModels;
using HobbiesClub.Core.Entities;
using HobbiesClub.Data;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.Views;

public partial class VerifyDialog : Window
{
    private static readonly TimeSpan NormalClose = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan NoteIdleClose = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan InputGuard = TimeSpan.FromMilliseconds(1500);
    private readonly DispatcherTimer _timer;
    private readonly DateTime _openedAt = DateTime.Now;
    private string _reason = "window closed (unknown)";
    private bool _ready;

    public VerifyDialog(VerifyInfo info)
    {
        InitializeComponent();
        DataContext = info;
        Topmost = true;

        _timer = new DispatcherTimer { Interval = NormalClose };
        _timer.Tick += (_, _) => { _reason = $"timer {(int)_timer.Interval.TotalSeconds}s"; Close(); };
        _timer.Start();

        // Ignore taps and Enter for the first moment, so the tap/Enter that opened the card cannot close it.
        IsHitTestVisible = false;
        OkBtn.IsDefault = false;
        var guard = new DispatcherTimer { Interval = InputGuard };
        guard.Tick += (_, _) =>
        {
            guard.Stop();
            _ready = true;
            IsHitTestVisible = true;
            if (NotePanel.Visibility != Visibility.Visible) OkBtn.IsDefault = true;
        };
        guard.Start();

        Closed += (_, _) => { _timer.Stop(); guard.Stop(); };
        if (info.LogId <= 0) AddNoteBtn.Visibility = Visibility.Collapsed;
    }

    private void WriteDebug()
    {
        try
        {
            Directory.CreateDirectory(AppPaths.LogFolder);
            var secs = (DateTime.Now - _openedAt).TotalSeconds;
            File.AppendAllText(Path.Combine(AppPaths.LogFolder, "card-debug.txt"),
                $"{_openedAt:yyyy-MM-dd HH:mm:ss}  open {secs:0.0}s  closed by: {_reason}{Environment.NewLine}");
        }
        catch { /* never block */ }
    }

    private void RestartTimer()
    {
        _timer.Stop();
        _timer.Start();
    }

    private void OnOk(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _reason = sender is Button { Content: string c } ? $"button '{c}'" : "button";
        Close();
    }

    private void OnAddNote(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _timer.Stop();
        _timer.Interval = NoteIdleClose;
        _timer.Start();
        AddNoteBtn.Visibility = Visibility.Collapsed;
        OkBtn.IsDefault = false;
        OkBtn.Visibility = Visibility.Collapsed;
        NotePanel.Visibility = Visibility.Visible;
        NoteBox.Focus();
    }

    private void OnNoteTyping(object sender, TextChangedEventArgs e)
    {
        NoteError.Text = string.Empty;
        RestartTimer();
    }

    private async void OnSaveNote(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        var text = NoteBox.Text.Trim();
        if (text.Length == 0) { _reason = "save with empty note"; Close(); return; }
        if (DataContext is not VerifyInfo { LogId: > 0 } info) { _reason = "save (no entry id)"; Close(); return; }

        _timer.Stop();
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.AttendanceLogs.FindAsync(info.LogId);
            if (log is not null)
            {
                log.Note = string.IsNullOrWhiteSpace(log.Note) ? text : log.Note + " | " + text;
                db.AuditLogs.Add(new AuditLog
                {
                    Timestamp = DateTime.Now,
                    Username = "kiosk",
                    Action = "AttendanceNoteAdded",
                    Details = $"{info.FullName}, {info.CurrentText} ({info.ClubName}): {text}"
                });
                await db.SaveChangesAsync();
            }
        }
        catch (Exception ex)
        {
            NoteError.Text = "Could not save the note: " + ex.Message;
            RestartTimer();
            return;
        }
        _reason = "note saved";
        Close();
    }
}

