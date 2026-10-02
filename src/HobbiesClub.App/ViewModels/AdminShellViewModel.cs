using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;
using HobbiesClub.Data;

namespace HobbiesClub.App.ViewModels;

public class UserRow
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public string Active { get; init; } = string.Empty;
}

public partial class AdminShellViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly int _actorId;
    private readonly string _actorUsername;

    public AdminShellViewModel(ShellViewModel shell, AppUser actor)
    {
        _shell = shell;
        _actorId = actor.Id;
        _actorUsername = actor.Username;
        Title = $"Admin panel - {actor.FullName}";
        DisplayName = actor.FullName;
        DisplayInitial = actor.FullName.Length > 0 ? actor.FullName.Substring(0, 1).ToUpperInvariant() : "A";
        Roles = new[] { UserRole.Kiosk, UserRole.SuperAdmin };
        Members = new MembersViewModel(_actorId, _actorUsername);
        Backup = new BackupViewModel(_actorId, _actorUsername);
        BackupScheduler.Start();
        Dashboard = new DashboardViewModel();
        _ = LoadUsersAsync();
    }

    public string Title { get; }
    public string DisplayName { get; }
    public string DisplayInitial { get; }
    public UserRole[] Roles { get; }
    public ObservableCollection<UserRow> Users { get; } = new();
    public MembersViewModel Members { get; }
    public DashboardViewModel Dashboard { get; }
    public ReportsViewModel Reports { get; } = new();
    public BackupViewModel Backup { get; }
    public ActivityLogViewModel ActivityLog { get; } = new();

    [ObservableProperty] private string _newFullName = string.Empty;
    [ObservableProperty] private string _newUsername = string.Empty;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private UserRole _newRole = UserRole.Kiosk;
    [ObservableProperty] private string _message = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    private async Task LoadUsersAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var list = await db.AppUsers.OrderBy(u => u.Id).ToListAsync();

        Users.Clear();
        foreach (var u in list)
            Users.Add(new UserRow
            {
                Id = u.Id,
                Username = u.Username,
                FullName = u.FullName,
                Role = u.Role.ToString(),
                Active = u.IsActive ? "Yes" : "No"
            });
    }

    [RelayCommand]
    private async Task CreateUserAsync()
    {
        Error = string.Empty;
        Message = string.Empty;

        using var scope = App.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var result = await auth.CreateUserAsync(
            NewUsername, NewFullName, NewPassword, NewRole, _actorUsername, _actorId);

        if (!result.Succeeded)
        {
            Error = result.Error ?? "Something went wrong.";
            return;
        }

        Message = $"User '{NewUsername.Trim().ToLowerInvariant()}' created.";
        NewFullName = string.Empty;
        NewUsername = string.Empty;
        NewPassword = string.Empty;
        NewRole = UserRole.Kiosk;
        await LoadUsersAsync();
    }

    [RelayCommand]
    private void Logout() { Dashboard.Stop(); _shell.NavigateTo(new LoginViewModel(_shell)); }
    // ---- Edit an existing user ----
    [ObservableProperty] private UserRow? _selectedUser;
    [ObservableProperty] private bool _hasSelectedUser;
    [ObservableProperty] private string _editUserFullName = string.Empty;
    [ObservableProperty] private UserRole _editUserRole = UserRole.Kiosk;
    [ObservableProperty] private bool _editUserActive = true;
    [ObservableProperty] private string _userMessage = string.Empty;
    [ObservableProperty] private string _userError = string.Empty;

    partial void OnSelectedUserChanged(UserRow? value)
    {
        HasSelectedUser = value is not null;
        UserMessage = string.Empty;
        UserError = string.Empty;
        if (value is null) return;
        EditUserFullName = value.FullName;
        EditUserRole = Enum.TryParse<UserRole>(value.Role, out var r) ? r : UserRole.Kiosk;
        EditUserActive = value.Active == "Yes";
    }

    private async Task RunUserAsync(Func<UserAdminService, Task<HobbiesClub.Core.Models.OperationResult>> action, string okText)
    {
        UserMessage = string.Empty;
        UserError = string.Empty;
        var keepId = SelectedUser?.Id;

        HobbiesClub.Core.Models.OperationResult result;
        using (var scope = App.Services.CreateScope())
            result = await action(scope.ServiceProvider.GetRequiredService<UserAdminService>());

        if (!result.Succeeded)
        {
            UserError = result.Error ?? "Something went wrong.";
            return;
        }

        await LoadUsersAsync();
        SelectedUser = Users.FirstOrDefault(u => u.Id == keepId);
        UserMessage = okText;
    }

    [RelayCommand]
    private async Task SaveUserAsync()
    {
        if (SelectedUser is not { } u) return;
        var name = EditUserFullName; var role = EditUserRole; var active = EditUserActive;
        await RunUserAsync(s => s.UpdateUserAsync(u.Id, name, role, active, _actorUsername, _actorId), "User saved.");
    }

    [RelayCommand]
    private async Task ResetUserPasswordAsync()
    {
        if (SelectedUser is not { } u) return;
        var pwd = HobbiesClub.App.Views.PromptDialog.Ask("Reset password",
            $"New password for {u.Username} (at least 8 characters). They must change it at next login:", 100);
        if (pwd is null) return;
        await RunUserAsync(s => s.ResetPasswordAsync(u.Id, pwd, _actorUsername, _actorId),
            "Password reset. The user must change it at next login.");
    }

    [RelayCommand]
    private async Task UnlockUserAsync()
    {
        if (SelectedUser is not { } u) return;
        await RunUserAsync(s => s.UnlockAsync(u.Id, _actorUsername, _actorId), "Account unlocked.");
    }
}



