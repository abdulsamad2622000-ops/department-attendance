using System.IO;
using HobbiesClub.Biometrics.Fake;
using HobbiesClub.Core.Interfaces;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace HobbiesClub.App;

public static class AppHost
{
    public static void ConfigureLogging()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(AppPaths.LogFolder, "hobbies-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30)
            .CreateLogger();
    }

    public static ServiceProvider Build()
    {
        var services = new ServiceCollection();

        services.AddDbContext<AppDbContext>(o => o.UseSqlite(AppPaths.ConnectionString));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();

        // Fake implementations until the hardware arrives. Real ones will be swapped in here.
        services.AddSingleton<IFingerprintService, FakeFingerprintService>();
        // Real camera by default. Set HOBBIES_FAKE_FACE=1 to develop without a camera.
        if (Environment.GetEnvironmentVariable("HOBBIES_FAKE_FACE") == "1")
            services.AddSingleton<IFaceService, FakeFaceService>();
        else
            services.AddSingleton<IFaceService>(_ => new HobbiesClub.Biometrics.Face.EmguFaceService(0));

        services.AddScoped<AuthService>();
        services.AddScoped<MemberService>();
        services.AddScoped<BiometricService>();
        services.AddScoped<AttendanceService>();
        services.AddScoped<BadgeService>();
        services.AddScoped<MemberAdminService>();
        services.AddScoped<BackupService>();
        services.AddScoped<ReportService>();
        services.AddScoped<UserAdminService>();

        return services.BuildServiceProvider();
    }
}
