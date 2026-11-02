using System.Windows;
using Quizmester.Models;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class AdminWindow : Window
{
    // Het huidige account-ID is nodig om te voorkomen dat admin zichzelf verwijdert.
    private readonly User _currentUser;

    public AdminWindow(User currentUser)
    {
        InitializeComponent();
        _currentUser = currentUser;
        // Zodra het venster geladen is, vullen we beide tabellen vanuit SQLite.
        Loaded += (_, _) => RefreshAll();
    }

    // Na een toevoegen/bewerken/verwijderen opnieuw data ophalen: UI is weer up-to-date.
    private void RefreshAll()
    {
        QuestionGrid.ItemsSource = DatabaseService.GetAllQuestionsForAdmin();
        UserGrid.ItemsSource = DatabaseService.GetUsers();
    }

    // CRUD: Create. Een nieuwe QuestionEditorWindow krijgt geen Id mee.
    private void AddQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (new QuestionEditorWindow { Owner = this }.ShowDialog() == true)
            RefreshAll();
    }

    // CRUD: Update. Een geselecteerde Question heeft een Id waarmee data wordt geladen.
    private void EditQuestion_Click(object sender, RoutedEventArgs e)
    {
        // Pattern matching controleert type én selectie tegelijk.
        if (QuestionGrid.SelectedItem is not Question selected)
        {
            MessageBox.Show("Selecteer eerst een vraag.");
            return;
        }

        if (new QuestionEditorWindow(selected.Id) { Owner = this }.ShowDialog() == true)
            RefreshAll();
    }

    // CRUD: Delete. Eerst selectie en bevestiging, dan database-DELETE.
    private void DeleteQuestion_Click(object sender, RoutedEventArgs e)
    {
        if (QuestionGrid.SelectedItem is not Question selected)
        {
            MessageBox.Show("Selecteer eerst een vraag.");
            return;
        }

        if (MessageBox.Show($"Weet je zeker dat je deze vraag wilt verwijderen?\n\n{selected.QuestionText}", "Vraag verwijderen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        // Door foreign key ON DELETE CASCADE worden bijbehorende Answers verwijderd.
        DatabaseService.DeleteQuestion(selected.Id);
        RefreshAll();
    }

    // Admin kan ook een User of nieuw Admin-account maken.
    private void AddUser_Click(object sender, RoutedEventArgs e)
    {
        if (new UserEditorWindow { Owner = this }.ShowDialog() == true)
            RefreshAll();
    }

    // Verwijder een gebruiker pas na expliciete bevestiging; scores verdwijnen mee.
    private void DeleteUser_Click(object sender, RoutedEventArgs e)
    {
        if (UserGrid.SelectedItem is not User selected)
        {
            MessageBox.Show("Selecteer eerst een gebruiker.");
            return;
        }

        if (MessageBox.Show($"Gebruiker '{selected.Username}' verwijderen? De scores van deze gebruiker worden ook verwijderd.", "Gebruiker verwijderen", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        // DatabaseService verhindert verwijderen van jezelf en van de laatste Admin.
        // Via out krijgt het scherm een passende melding terug.
        DatabaseService.DeleteUser(selected.Id, _currentUser.Id, out string message);
        MessageBox.Show(message);
        RefreshAll();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
