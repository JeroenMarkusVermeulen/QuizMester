using System.Windows;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class ScoreboardWindow : Window
{
    public ScoreboardWindow()
    {
        InitializeComponent();
        // WPF data-binding: ItemsSource verbindt de 10 beste gespeelde games met DataGrid.
        Loaded += (_, _) => ScoreGrid.ItemsSource = DatabaseService.GetTopScores();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
