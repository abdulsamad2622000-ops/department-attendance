using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HobbiesClub.App.Views;
using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Core.Enums;
using HobbiesClub.Core.Models;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HobbiesClub.App.ViewModels;

public partial class KioskViewModel : ObservableObject
{
    private readonly ShellViewModel _shell;
    private readonly DispatcherTimer _timer;

    public KioskViewModel(ShellViewModel shell, string fullName)
    {
        _shell = shell;
        Title = "Fleet UMS Hobby Club & AI Center";
        UpdateClock();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += (_, _) => UpdateClock();
        _timer.Start();
        BackupScheduler.Start();
    }

    public string Title { get; }

    [ObservableProperty] private string _clockText = string.Empty;
    [ObservableProperty] private string _dateText = string.Empty;
    [ObservableProperty] private string _status = string.Empty;

    private void UpdateClock()
    {
        var now = DateTime.Now;
        ClockText = now.ToString("hh:mm:ss tt");
        DateText = now.ToString("dddd, dd MMMM yyyy");
    }

    [RelayCommand]
    private Task FaceScanAsync() => ScanAsync(VerificationMethod.Face);

    [RelayCommand]
    private Task FingerprintAsync() => ScanAsync(VerificationMethod.Fingerprint);

    [RelayCommand]
    private void Pin()
    {
        _timer.Stop();
        _shell.NavigateTo(new PinViewModel(_shell));
    }

    [RelayCommand]
    private void Register()
    {
        _timer.Stop();
        _shell.NavigateTo(new RegistrationViewModel(_shell));
    }

    private int _failedScans;

    /// <summary>Face or fingerprint: identify the member, then continue with the same flow as PIN.</summary>
    private async Task ScanAsync(VerificationMethod method)
    {
        Status = method == VerificationMethod.Face ? "Look at the camera..." : "Place your finger on the scanner...";

        using var scope = App.Services.CreateScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var bio = sp.GetRequiredService<BiometricService>();
        var type = method == VerificationMethod.Face ? BiometricType.Face : BiometricType.Fingerprint;

        byte[]? captured;
        if (method == VerificationMethod.Face)
        {
            var svc = sp.GetRequiredService<IFaceService>();
            if (svc is FakeFaceService fake) fake.NextTemplate = await DevPickTemplateAsync(db, type);
            captured = HobbiesClub.App.Views.FaceScanDialog.Scan(svc, "Face check-in", false);
        }
        else
        {
            var svc = sp.GetRequiredService<IFingerprintService>();
            if (svc is FakeFingerprintService && Environment.GetEnvironmentVariable("HOBBIES_DEV") != "1")
            {
                Status = "Fingerprint scanner not connected. Please use Face or PIN.";
                return;
            }
            if (svc is FakeFingerprintService fake) fake.NextTemplate = await DevPickTemplateAsync(db, type);
            captured = await svc.CaptureTemplateAsync();
        }

        if (captured is null || captured.Length == 0)
        {
            Status = "Scan failed. Please try again.";
            return;
        }

        var member = method == VerificationMethod.Face
            ? await bio.IdentifyFaceAsync(captured)
            : await bio.IdentifyFingerprintAsync(captured);

        if (member is null)
        {
            _failedScans++;
            Status = _failedScans >= 3 ? "Not recognised. Please use your PIN." : "Not recognised. Please try again.";
            return;
        }

        _failedScans = 0;
        _timer.Stop();
        var vm = new PinViewModel(_shell, member.Id, method);
        _shell.NavigateTo(vm);
        await vm.StartForMemberAsync();
    }

    // Development only: with the fake scanners, "scan" a saved template of a random active member.
    private static async Task<byte[]?> DevPickTemplateAsync(AppDbContext db, BiometricType type)
    {
        var all = await db.BiometricTemplates
            .Where(t => t.Type == type && t.Member.Status == MemberStatus.Active)
            .Select(t => t.TemplateData)
            .ToListAsync();
        return all.Count == 0 ? null : all[Random.Shared.Next(all.Count)];
    }
    // "Log out" button: needs a Super Admin password before leaving the kiosk.
    [RelayCommand]
    private async Task AdminAsync()
    {
        Status = string.Empty;

        var dialog = new AdminPasswordDialog { Owner = Application.Current.MainWindow };
        if (dialog.ShowDialog() != true)
            return;

        var password = dialog.Password;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var auth = scope.ServiceProvider.GetRequiredService<AuthService>();

        var admins = await db.AppUsers
            .Where(u => u.Role == UserRole.SuperAdmin && u.IsActive)
            .Select(u => u.Username)
            .ToListAsync();

        foreach (var username in admins)
        {
            var result = await auth.LoginAsync(username, password);
            if (result.Status == LoginStatus.Success)
            {
                _timer.Stop();
                _shell.NavigateTo(new LoginViewModel(_shell));
                return;
            }
        }

        Status = "Incorrect Super Admin password.";
    }
}






