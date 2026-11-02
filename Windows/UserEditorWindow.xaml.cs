using System.Windows;
using System.Windows.Controls;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class UserEditorWindow : Window
{
    public UserEditorWindow()
    {
        InitializeComponent();
    }

    // Adminformulier controleert een nieuw account vóór het in de DB terechtkomt.
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;
        // ?? gebruikt User als fallback wanneer er onverwacht geen rol gekozen is.
        string role = (RoleCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "User";

        if (username.Length < 3)
        {
            ErrorText.Text = "Gebruikersnaam moet minimaal 3 tekens bevatten.";
            return;
        }
        if (password.Length < 6)
        {
            ErrorText.Text = "Wachtwoord moet minimaal 6 tekens bevatten.";
            return;
        }
        // UsernameExists + UNIQUE in SQLite voorkomen dubbele accounts.
        if (DatabaseService.UsernameExists(username))
        {
            ErrorText.Text = "Deze gebruikersnaam bestaat al.";
            return;
        }
        // DatabaseService hasht het wachtwoord, controleert rol en doet parameterized INSERT.
        if (!DatabaseService.CreateUser(username, password, role))
        {
            ErrorText.Text = "Gebruiker kon niet worden toegevoegd.";
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
