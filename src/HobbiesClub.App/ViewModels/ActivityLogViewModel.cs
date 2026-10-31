using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public class ActivityRow
{
    public string Time { get; init; } = string.Empty;
    public string User { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
}

/// <summary>Admin panel: everything important that happened (audit log), with filters.</summary>
public partial class ActivityLogViewModel : ObservableObject
{
    public const string AllActions = "All actions";
    private const int MaxRows = 2000;

    private static readonly Dictionary<string, string> Friendly = new()
    {
        ["FirstSuperAdminCreated"] = "First Super Admin created",
        ["Login"] = "Logged in",
        ["LoginFailed"] = "Wrong password",
        ["AccountLocked"] = "Account locked",
        ["PasswordChanged"] = "Password changed",
        ["UserCreated"] = "User created",
        ["MemberAdded"] = "Member registered",
        ["MemberTerminated"] = "Member terminated",
        ["MemberReactivated"] = "Member reactivated",
        ["BadgeChanged"] = "Badge changed",
        ["ClubJoined"] = "Joined a department",
        ["ClubLeft"] = "Removed from a department",
        ["PinReset"] = "PIN reset",
        ["FaceReenrolled"] = "Face re-enrolled",
        ["LateCheckOutEntered"] = "Late check-out entered",
        ["AutoCheckOut"] = "Auto check-out (other department)",
        ["BackupCreated"] = "Backup created",
        ["BackupFailed"] = "Backup failed",
        ["BackupRestored"] = "Backup restored",
        ["FingersReenrolled"] = "Fingerprints re-enrolled",
        ["DetailsEdited"] = "Member details edited",
        ["UserEdited"] = "User edited",
        ["UserPasswordReset"] = "User password reset",
        ["UserUnlocked"] = "User account unlocked"
    };

    private readonly Dictionary<string, string> _friendlyToRaw = new();
    private bool _loading;

    public ActivityLogViewModel()
    {
        _from = DateTime.Today.AddDays(-7);
        _to = DateTime.Today;
        _ = LoadAsync();
    }

    public static string FriendlyName(string raw) => Friendly.TryGetValue(raw, out var f) ? f : raw;

    public ObservableCollection<ActivityRow> Rows { get; } = new();
    public ObservableCollection<string> ActionOptions { get; } = new() { AllActions };

    [ObservableProperty] private DateTime? _from;
    [ObservableProperty] private DateTime? _to;
    [ObservableProperty] private string _actionFilter = AllActions;
    [ObservableProperty] private string _userSearch = string.Empty;
    [ObservableProperty] private string _summary = string.Empty;

    partial void OnFromChanged(DateTime? value) => _ = LoadAsync();
    partial void OnToChanged(DateTime? value) => _ = LoadAsync();
    partial void OnActionFilterChanged(string value) => _ = LoadAsync();
    partial void OnUserSearchChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (_loading) return;
        _loading = true;
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            if (ActionOptions.Count == 1)
            {
                var actions = await db.AuditLogs.AsNoTracking().Select(a => a.Action).Distinct().ToListAsync();
                foreach (var raw in actions.OrderBy(FriendlyName))
                {
                    var name = FriendlyName(raw);
                    _friendlyToRaw[name] = raw;
                    ActionOptions.Add(name);
                }
            }

            var from = (From ?? DateTime.Today.AddDays(-7)).Date;
            var to = (To ?? DateTime.Today).Date.AddDays(1);
            var query = db.AuditLogs.AsNoTracking().Where(a => a.Timestamp >= from && a.Timestamp < to);

            if (ActionFilter != AllActions && _friendlyToRaw.TryGetValue(ActionFilter, out var rawAction))
                query = query.Where(a => a.Action == rawAction);

            var user = (UserSearch ?? string.Empty).Trim();
            if (user.Length > 0)
                query = query.Where(a => a.Username.Contains(user));

            var total = await query.CountAsync();
            var list = await query.OrderByDescending(a => a.Timestamp).ThenByDescending(a => a.Id)
                .Take(MaxRows).ToListAsync();

            Rows.Clear();
            foreach (var a in list)
                Rows.Add(new ActivityRow
                {
                    Time = a.Timestamp.ToString("dd MMM yyyy, hh:mm:ss tt"),
                    User = a.Username,
                    Action = FriendlyName(a.Action),
                    Details = a.Details ?? string.Empty
                });

            Summary = total > MaxRows ? $"Showing latest {MaxRows} of {total} entries" : $"{total} entries";
        }
        catch (Exception ex)
        {
            Summary = "Could not load: " + ex.Message;
        }
        finally
        {
            _loading = false;
        }
    }
}




