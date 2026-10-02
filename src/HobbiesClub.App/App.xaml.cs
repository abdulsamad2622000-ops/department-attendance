using System.Windows;
using HobbiesClub.Data;
using HobbiesClub.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace HobbiesClub.App;

public partial class App : Application
{
    public static ServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            AppPaths.EnsureFolders();
            AppHost.ConfigureLogging();
            Log.Information("App start");

            DispatcherUnhandledException += (_, args) =>
            {
                Log.Error(args.Exception, "Unhandled UI exception");
                MessageBox.Show("Something went wrong. Details were written to the log.", "Hobbies Club",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                args.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (_, args) =>
                Log.Fatal(args.ExceptionObject as Exception, "Fatal exception");

            Services = AppHost.Build();

            bool firstRun;
            using (var scope = Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                DatabaseInitializer.Initialize(db);

                var auth = scope.ServiceProvider.GetRequiredService<AuthService>();
                firstRun = await auth.IsFirstRunAsync();
            }

            Log.Information("Startup OK, first run: {FirstRun}", firstRun);

            var window = new MainWindow(firstRun);
            window.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Startup failed");
            MessageBox.Show("The app could not start:\n" + ex.Message, "Hobbies Club",
                MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("App exit");
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
