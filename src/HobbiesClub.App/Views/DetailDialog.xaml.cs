using System.Windows;
using HobbiesClub.App.ViewModels;

namespace HobbiesClub.App.Views;

/// <summary>Popup list behind a dashboard card.</summary>
public partial class DetailDialog : Window
{
    public DetailDialog(string title, IReadOnlyList<DetailRow> rows)
    {
        InitializeComponent();
        Title = title;
        Heading.Text = $"{title} ({rows.Count})";
        RowsGrid.ItemsSource = rows;
        EmptyText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
