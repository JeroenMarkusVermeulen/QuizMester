using System.Windows;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class RegisterWindow : Window
{
    public RegisterWindow()
    {
        InitializeComponent();
    }

    // Controleer invoer vóór database-aanroep: voorkomt lege of ongeldige accounts.
    private void Register_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        string username = UsernameBox.Text.Trim();
        // PasswordBox toont geen leesbaar wachtwoord; DB slaat straks alleen een hash op.
        string password = PasswordBox.Password;
        string confirm = ConfirmPasswordBox.Password;

        // Projectvalidatie: minimaal drie tekens voor de username.
        if (username.Length < 3)
        {
            ErrorText.Text = "De gebruikersnaam moet minimaal 3 tekens bevatten.";
            return;
        }
        // Projectvalidatie: minimaal zes tekens voor het wachtwoord.
        if (password.Length < 6)
        {
            ErrorText.Text = "Het wachtwoord moet minimaal 6 tekens bevatten.";
            return;
        }
        // Dubbele invoer voorkomt het opslaan van een onbedoeld verkeerd wachtwoord.
        if (password != confirm)
        {
            ErrorText.Text = "De wachtwoorden zijn niet gelijk.";
            return;
        }
        // Unieke gebruikersnaam is een Must-eis; DB heeft ook een UNIQUE-constraint.
        if (DatabaseService.UsernameExists(username))
        {
            ErrorText.Text = "Deze gebruikersnaam bestaat al.";
            return;
        }
        // CreateUser hasht het wachtwoord en gebruikt standaard rol "User".
        if (!DatabaseService.CreateUser(username, password))
        {
            ErrorText.Text = "Account kon niet worden aangemaakt.";
            return;
        }

        MessageBox.Show("Account is aangemaakt. Je kunt nu inloggen.", "Registratie gelukt", MessageBoxButton.OK, MessageBoxImage.Information);
        // true sluit het modal venster en vertelt het loginvenster dat het gelukt is.
        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
