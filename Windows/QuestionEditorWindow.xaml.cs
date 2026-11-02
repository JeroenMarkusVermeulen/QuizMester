using System.Windows;
using System.Windows.Controls;
using Quizmester.Models;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class QuestionEditorWindow : Window
{
    // null QuestionId = nieuwe vraag; een gevuld Id = bestaande vraag bewerken.
    private readonly int? _questionId;
    private readonly TextBox[] _answerBoxes;
    private readonly RadioButton[] _correctRadios;

    public QuestionEditorWindow(int? questionId = null)
    {
        InitializeComponent();
        _questionId = questionId;
        // Vier TextBoxes en vier RadioButtons in arrays: geen aparte code per antwoord nodig.
        _answerBoxes = new[] { Answer1Box, Answer2Box, Answer3Box, Answer4Box };
        _correctRadios = new[] { Correct1Radio, Correct2Radio, Correct3Radio, Correct4Radio };

        CategoryCombo.ItemsSource = DatabaseService.GetCategories();
        CategoryCombo.SelectedIndex = 0;

        // Zelfde formulier voor Create en Update; DatabaseService beslist welke SQL nodig is.
        if (_questionId.HasValue)
            LoadQuestion();
    }

    // Vraag én bijbehorende 4 answers ophalen en in de invoervelden plaatsen.
    private void LoadQuestion()
    {
        Question? question = DatabaseService.GetQuestionById(_questionId!.Value);
        if (question is null)
        {
            MessageBox.Show("Vraag kon niet worden geladen.");
            Close();
            return;
        }

        TitleText.Text = "Vraag bewerken";
        QuestionBox.Text = question.QuestionText;
        CategoryCombo.SelectedValue = question.CategoryId;

        // Kopieer de opgeslagen answers en zet precies het juiste RadioButton aan.
        for (int i = 0; i < Math.Min(4, question.Answers.Count); i++)
        {
            _answerBoxes[i].Text = question.Answers[i].AnswerText;
            _correctRadios[i].IsChecked = question.Answers[i].IsCorrect;
        }
    }

    // Validaties vóór INSERT/UPDATE: category, vraagtekst, 4 antwoorden, exact 1 correct.
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (CategoryCombo.SelectedValue is not int categoryId)
        {
            ErrorText.Text = "Kies een categorie.";
            return;
        }

        if (string.IsNullOrWhiteSpace(QuestionBox.Text))
        {
            ErrorText.Text = "Vul een vraag in.";
            return;
        }

        // Tuple (Text, IsCorrect) combineert antwoordtekst en juistheid zonder extra class.
        var answers = new List<(string Text, bool IsCorrect)>();
        for (int i = 0; i < 4; i++)
            answers.Add((_answerBoxes[i].Text.Trim(), _correctRadios[i].IsChecked == true));

        if (answers.Any(a => string.IsNullOrWhiteSpace(a.Text)))
        {
            ErrorText.Text = "Vul alle vier antwoorden in.";
            return;
        }
        if (answers.Count(a => a.IsCorrect) != 1)
        {
            ErrorText.Text = "Kies precies één correct antwoord.";
            return;
        }

        if (_questionId.HasValue)
            DatabaseService.UpdateQuestion(_questionId.Value, categoryId, QuestionBox.Text, answers);
        else
            DatabaseService.AddQuestion(categoryId, QuestionBox.Text, answers);

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
}
