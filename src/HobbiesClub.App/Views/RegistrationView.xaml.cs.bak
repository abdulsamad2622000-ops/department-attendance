using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Input;
using HobbiesClub.App.ViewModels;

namespace HobbiesClub.App.Views;

public partial class RegistrationView : UserControl
{
    public RegistrationView()
    {
        InitializeComponent();
        Focusable = true;
        PreviewKeyDown += OnPreviewKeyDown;
        DataContextChanged += (_, e) =>
        {
            if (e.NewValue is INotifyPropertyChanged vm)
                vm.PropertyChanged += OnVmPropertyChanged;
        };
    }

    // When the step changes, take keyboard focus so the PIN can be typed.
    private void OnVmPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == "Step" && DataContext is RegistrationViewModel { IsPin: true })
            Dispatcher.BeginInvoke(() => Keyboard.Focus(this));
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not RegistrationViewModel vm || !vm.IsPin) return;

        string? key = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(e.Key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((int)(e.Key - Key.NumPad0)).ToString(),
            Key.Back => "Del",
            Key.Enter => "OK",
            _ => null
        };
        if (key is null) return;

        if (vm.KeyCommand.CanExecute(key)) vm.KeyCommand.Execute(key);
        e.Handled = true;
    }
}
