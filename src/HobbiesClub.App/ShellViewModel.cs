using CommunityToolkit.Mvvm.ComponentModel;

namespace HobbiesClub.App;

public partial class ShellViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _current;

    public void NavigateTo(object viewModel) => Current = viewModel;
}
