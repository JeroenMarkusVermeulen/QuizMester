using System.Windows;
using Quizmester.Models;

namespace Quizmester.Windows;

public partial class MainMenuWindow : Window
{
    // Gekoppelde, ingelogde speler; ook zijn rol (User/Admin) is beschikbaar.
    private readonly User _user;

    public MainMenuWindow(User user)
    {
        InitializeComponent();
        _user = user;
        WelcomeText.Text = $"Welkom, {_user.Username}";
        RoleText.Text = _user.IsAdmin ? "ADMINISTRATOR" : "SPELER";
        // Alleen admins zien het beheerknopje; Admin_Click controleert rol nogmaals.
        AdminButton.Visibility = _user.IsAdmin ? Visibility.Visible : Visibility.Collapsed;
    }

    // Start de klassieke quiz met keuze uit General of één categorie.
    private void StartQuiz_Click(object sender, RoutedEventArgs e)
    {
        new CategoryWindow(_user) { Owner = this }.ShowDialog();
    }

    // Speed Quiz uses all categories and does not write to the classic score top 10.
    // Speed Quiz kiest ALLE categorieën (null = General) en aparte spelregels.
    private void SpeedQuiz_Click(object sender, RoutedEventArgs e)
    {
        new QuizWindow(_user, null, "Speed Quiz", QuizMode.Speed)
        {
            Owner = this
        }.ShowDialog();
    }

    // Toont alleen de reguliere top 10, niet het snelheidsrecord.
    private void Scoreboard_Click(object sender, RoutedEventArgs e)
    {
        new ScoreboardWindow { Owner = this }.ShowDialog();
    }

    // Autorisatie-check ook in code, niet alleen via verborgen knop.
    private void Admin_Click(object sender, RoutedEventArgs e)
    {
        if (!_user.IsAdmin)
            return;
        new AdminWindow(_user) { Owner = this }.ShowDialog();
    }

    // Open opnieuw login en sluit het menu, zodat de oude sessie niet actief blijft.
    private void Logout_Click(object sender, RoutedEventArgs e)
    {
        new LoginWindow().Show();
        Close();
    }
}
