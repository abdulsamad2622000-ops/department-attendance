using System.Windows;
using System.Windows.Threading;
using HobbiesClub.App.ViewModels;

namespace HobbiesClub.App.Views;

public partial class VerifyDialog : Window
{
    private readonly DispatcherTimer _timer;

    public VerifyDialog(VerifyInfo info)
    {
        InitializeComponent();
        DataContext = info;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        _timer.Tick += (_, _) => Close();
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void OnOk(object sender, RoutedEventArgs e) => Close();
}

