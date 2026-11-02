using System.Windows;
using System.Windows.Input;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class LoginWindow : Window
{
    // Constructor: wordt uitgevoerd bij het aanmaken van het loginvenster.
    public LoginWindow()
    {
        InitializeComponent();
        // De cursor staat meteen in de gebruikersnaam-input (gebruiksvriendelijkheid).
        UsernameBox.Focus();
    }

    // Event-handler: WPF roept dit aan bij een klik op de Inloggen-knop.
    private void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        // Trim haalt spaties aan begin/einde weg; lege invoer wordt hieronder afgewezen.
        string username = UsernameBox.Text.Trim();
        string password = PasswordBox.Password;

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            ErrorText.Text = "Vul je gebruikersnaam en wachtwoord in.";
            return;
        }

        // SQL + PBKDF2 controle zitten in DatabaseService, niet in het scherm.
        // Bij onjuiste gegevens levert Authenticate null op.
        var user = DatabaseService.Authenticate(username, password);
        if (user is null)
        {
            ErrorText.Text = "Gebruikersnaam of wachtwoord is onjuist.";
            return;
        }

        // Geef het User-object met Id en Role mee naar het hoofdmenu.
        new MainMenuWindow(user).Show();
        Close();
    }

    // Modal venster: gebruiker rondt eerst de registratie af of annuleert.
    private void Register_Click(object sender, RoutedEventArgs e)
    {
        new RegisterWindow { Owner = this }.ShowDialog();
    }

    // Enter in wachtwoordveld start dezelfde loginlogica als een muisklik.
    private void PasswordBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            Login_Click(sender, new RoutedEventArgs());
    }
}
