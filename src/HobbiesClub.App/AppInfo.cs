using System.Reflection;

namespace HobbiesClub.App;

/// <summary>App version shown on screen. Change it only in HobbiesClub.App.csproj (Version).</summary>
public static class AppInfo
{
    public static string Version { get; } = Format(Assembly.GetExecutingAssembly().GetName().Version);

    public static string VersionText => "Version " + Version;

    private static string Format(System.Version? v) => v is null ? "1.0" : $"{v.Major}.{v.Minor}.{v.Build}";
}
