using System.Windows;
using Quizmester.Models;

namespace Quizmester.Windows;

public partial class ResultWindow : Window
{
    // Existing regular quiz result with its top-10 position.
    // Constructor overload 1: reguliere quiz toont punten en positie binnen alle opgeslagen games.
    public ResultWindow(int score, int rank)
    {
        InitializeComponent();
        ScoreText.Text = score.ToString();
        RankText.Text = rank <= 10
            ? $"Gefeliciteerd! Je staat met deze score op plaats #{rank} in de top 10."
            : $"Deze score staat momenteel op plaats #{rank}; dus nog niet in de top 10.";
    }

    // Speed quiz result, with exactly one all-time best record (top 1).
    // Constructor overload 2: Speed Quiz toont de eindTIJD i.p.v. een puntenscore.
    public ResultWindow(long elapsedMilliseconds, int wrongAnswers,
                        SpeedRunResult bestRun, bool isNewRecord)
    {
        InitializeComponent();
        // Strafregel: één fout antwoord = +5 seconden = +5000 milliseconden.
        long penaltyMilliseconds = wrongAnswers * 5000L;
        long totalMilliseconds = elapsedMilliseconds + penaltyMilliseconds;

        ResultTitleText.Text = "Speed Quiz voltooid!";
        ScoreTitleText.Text = "Jouw eindtijd inclusief straftijd";
        ScoreText.Text = FormatTime(totalMilliseconds);
        // isNewRecord komt uit vergelijking van het opgeslagen SpeedRun.Id met het Top-1-Id.
        RankText.Text = isNewRecord
            ? "NIEUW RECORD! Jij hebt de snelste tijd ooit neergezet!"
            : $"Snelste tijd ooit: {FormatTime(bestRun.TotalMilliseconds)} door {bestRun.Username}.";
        DetailsText.Text = $"Werkelijke tijd: {FormatTime(elapsedMilliseconds)} · " +
                           $"{wrongAnswers} fout × 5 seconden = +{wrongAnswers * 5} sec" +
                           $"\nTop 1: {bestRun.Username} - {FormatTime(bestRun.TotalMilliseconds)}";
        // In Speed Quiz geen Top 10; alleen het snelste record verschijnt in de resultaten.
        ScoreboardButton.Visibility = Visibility.Collapsed;
    }

    // Zelfde tijdsnotatie als quizvenster voor consistente tijden.
    private static string FormatTime(long milliseconds)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(milliseconds);
        return t.TotalHours >= 1
            ? $"{(long)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}"
            : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
    }

    private void Scoreboard_Click(object sender, RoutedEventArgs e)
    {
        new ScoreboardWindow { Owner = this }.ShowDialog();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();
}
