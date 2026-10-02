using System.Windows;
using HobbiesClub.App.ViewModels;

namespace HobbiesClub.App;

public partial class MainWindow : Window
{
    public MainWindow(bool firstRun)
    {
        InitializeComponent();

        var shell = new ShellViewModel();
        DataContext = shell;
        shell.NavigateTo(firstRun ? new SetupViewModel(shell) : new LoginViewModel(shell));
    }
}
