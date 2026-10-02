using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class SetupViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;

    public SetupViewModel(ShellViewModel shell) => _shell = shell;

    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _error = string.Empty;

    // PasswordBox does not support binding, so the view pushes these values in.
    public string Password { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;

    [RelayCommand]
    private async Task CreateAsync()
    {
        Error = string.Empty;

        if (Password != ConfirmPassword)
        {
            Error = "The two passwords do not match.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
        var result = await auth.CreateFirstSuperAdminAsync(Username, FullName, Password);

        if (!result.Succeeded)
        {
            Error = result.Error ?? "Something went wrong.";
            return;
        }

        _shell.NavigateTo(new LoginViewModel(_shell));
    }
}
