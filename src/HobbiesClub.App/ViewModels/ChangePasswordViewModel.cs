using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Core.Entities;
using HobbiesClub.Core.Enums;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class ChangePasswordViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly AppUser _user;

    public ChangePasswordViewModel(ShellViewModel shell, AppUser user)
    {
        _shell = shell;
        _user = user;
    }

    [ObservableProperty] private string _error = string.Empty;

    // The view pushes these values in through PasswordField.
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    [RelayCommand]
    private async Task ChangeAsync()
    {
        Error = string.Empty;

        if (NewPassword != ConfirmPassword)
        {
            Error = "The two new passwords do not match.";
            return;
        }

        if (NewPassword == CurrentPassword)
        {
            Error = "The new password must be different from the current one.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var result = await auth.ChangePasswordAsync(_user.Id, CurrentPassword, NewPassword);

        if (!result.Succeeded)
        {
            Error = result.Error ?? "Something went wrong.";
            return;
        }

        object next = _user.Role == UserRole.SuperAdmin
            ? new AdminShellViewModel(_shell, _user)
            : new KioskViewModel(_shell, _user.FullName);
        _shell.NavigateTo(next);
    }

    [RelayCommand]
    private void Cancel() => _shell.NavigateTo(new LoginViewModel(_shell));
}
