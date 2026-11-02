using System.Windows;
using System.Windows.Controls;
using Quizmester.Models;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class CategoryWindow : Window
{
    private readonly User _user;
    // null betekent dat er geen specifieke categorie gekozen is (General).
    private Category? _selectedCategory;

    public CategoryWindow(User user)
    {
        InitializeComponent();
        _user = user;
        // Categorieën rechtstreeks uit database, zodat ze niet hardcoded in XAML staan.
        CategoryItems.ItemsSource = DatabaseService.GetCategories();
    }

    // RadioButtons delen GroupName="Category": er is slechts één selectie mogelijk.
    private void CategoryRadio_Checked(object sender, RoutedEventArgs e)
    {
        if (sender is RadioButton radio && radio.Tag is Category category)
            _selectedCategory = category;
    }

    private void Start_Click(object sender, RoutedEventArgs e)
    {
        // null CategoryId betekent General: alle vragen, geen aparte General-tabelrij.
        int? categoryId = null;
        string categoryName = "General";

        if (GeneralRadio.IsChecked != true)
        {
            if (_selectedCategory is null)
            {
                MessageBox.Show("Kies eerst een categorie.");
                return;
            }
            categoryId = _selectedCategory.Id;
            categoryName = _selectedCategory.Name;
        }

        Hide();
        // De standaardwaarde voor QuizMode is Normal (60 sec/5 sec per vraag).
        new QuizWindow(_user, categoryId, categoryName) { Owner = Owner }.ShowDialog();
        Close();
    }
}
