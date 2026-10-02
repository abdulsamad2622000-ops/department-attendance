using System.Windows.Controls;
using System.Windows.Input;
using HobbiesClub.App.ViewModels;

namespace HobbiesClub.App.Views;

public partial class PinView : UserControl
{
    public PinView()
    {
        InitializeComponent();
        Focusable = true;
        Loaded += (_, _) => Keyboard.Focus(this);
        PreviewKeyDown += OnPreviewKeyDown;
    }

    // Physical keyboard support: digits, Backspace, Enter and Esc.
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is not PinViewModel vm || !vm.ShowKeypad) return;

        string? digit = e.Key switch
        {
            >= Key.D0 and <= Key.D9 => ((int)(e.Key - Key.D0)).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => ((int)(e.Key - Key.NumPad0)).ToString(),
            _ => null
        };

        if (digit is not null)
        {
            vm.DigitCommand.Execute(digit);
            e.Handled = true;
        }
        else if (e.Key == Key.Back)
        {
            vm.BackspaceCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter)
        {
            if (vm.OkCommand.CanExecute(null)) vm.OkCommand.Execute(null);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            vm.BackCommand.Execute(null);
            e.Handled = true;
        }
    }
}

