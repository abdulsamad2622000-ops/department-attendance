using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Models;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public LoginViewModel(ShellViewModel shell) => _shell = shell;

    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    public string Password { get; set; } = string.Empty;

    [RelayCommand]
    private async Task LoginAsync()
    {
        Error = string.Empty;

        using var scope = App.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var result = await auth.LoginAsync(Username, Password);

        switch (result.Status)
        {
            case LoginStatus.Success:
                var user = result.User!;
                if (user.MustChangePassword)
                {
                    _shell.NavigateTo(new ChangePasswordViewModel(_shell, user));
                    break;
                }

                object next = user.Role == UserRole.SuperAdmin
                    ? new AdminShellViewModel(_shell, user)
                    : new KioskViewModel(_shell, user.FullName);
                _shell.NavigateTo(next);
                break;
            case LoginStatus.LockedOut:
                var secs = Math.Max(1, (int)Math.Ceiling(((result.LockedUntil ?? DateTime.Now) - DateTime.Now).TotalSeconds));
                Error = $"Too many failed attempts. Try again in {secs} seconds.";
                break;
            case LoginStatus.Inactive:
                Error = "This account is disabled.";
                break;
            default:
                Error = "Incorrect username or password.";
                break;
        }
    }
}
