using System.ComponentModel;
using System.Diagnostics;
using System.Media;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Quizmester.Models;
using Quizmester.Services;

namespace Quizmester.Windows;

public partial class QuizWindow : Window
{
    // Alle spelregels op één plek. De normale quiz heeft 60 seconden totaal,
    // maximaal 5 seconden per vraag en 10 punten per goed antwoord.
    // De speciale vraag geeft 20 EXTRA punten; Speed Quiz stopt na 10 goede antwoorden.
    private const int NormalQuizSeconds = 60;
    private const int NormalQuestionSeconds = 5;
    private const int PointsPerCorrect = 10;
    private const int SpecialBonusPoints = 20;
    private const int CorrectAnswersToWinSpeed = 10;

    private readonly User _user;
    private readonly int? _categoryId;
    private readonly string _categoryName;
    // Enum QuizMode bepaalt welke spelregels gelden: Normal of Speed.
    // Daarmee gebruiken beide spelmodi hetzelfde quizvenster zonder dubbele code.
    private readonly QuizMode _mode;
    // DispatcherTimer werkt op de WPF UI-thread en ververst de zichtbare tijd 10x per seconde.
    // Stopwatch meet de ECHTE verstreken tijd: UI-vertraging verandert de speltijd dus niet.
    private readonly DispatcherTimer _uiTimer = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private readonly Stopwatch _quizWatch = new();
    private readonly Stopwatch _questionWatch = new();
    private readonly Button[] _answerButtons;

    private List<Question> _questions = new();
    private int _questionIndex = -1;
    private int _questionsShown;
    // -1 = nog geen speciale vraag gekozen. Er wordt per gewone quiz één index geloot.
    private int _specialQuestionIndex = -1;
    private int _score;
    private int _correctAnswers;
    private int _wrongAnswers;
    // Jokerstatus wordt per NIEUWE quiz gereset; een gebruikte joker is niet herbruikbaar.
    private bool _skipAvailable = true;
    private bool _fiftyFiftyAvailable = true;
    // Deze twee vlaggen voorkomen dubbele klikken en dubbel opslaan bij timer/antwoord-races.
    private bool _questionActive;
    private bool _finished;

    // Existing calls from CategoryWindow continue to work (default = normal quiz).
    // Optional argument mode=Normal houdt bestaande aanroepen uit CategoryWindow werkend.
    public QuizWindow(User user, int? categoryId, string categoryName,
                      QuizMode mode = QuizMode.Normal)
    {
        InitializeComponent();
        _user = user;
        _categoryId = categoryId;
        _categoryName = categoryName;
        _mode = mode;
        // De vier knoppen als array: hiermee kunnen we alle antwoorden identiek behandelen.
        _answerButtons = new[] { AnswerButton1, AnswerButton2, AnswerButton3, AnswerButton4 };
        _uiTimer.Tick += UiTimer_Tick;
        Loaded += QuizWindow_Loaded;

        CategoryText.Text = mode == QuizMode.Speed ? "SPEED QUIZ" : $"Categorie: {_categoryName}";

        // Speed Quiz: geen individuele vraagtimer, wel oplopende totale tijd + straf.
        if (_mode == QuizMode.Speed)
        {
            QuizTimerTitle.Text = "TIJD INCL. STRAF";
            QuestionTimerPanel.Visibility = Visibility.Collapsed;
            PenaltyText.Visibility = Visibility.Visible;
            ScoreText.Text = "Goed: 0 / 10";
            QuizTimerText.Text = "00:00.0";
        }
    }

    // Loaded start de ronde pas nadat het hele scherm is opgebouwd.
    private void QuizWindow_Loaded(object sender, RoutedEventArgs e)
    {
        // Quizvragen komen uit SQLite. null categoryId = General (alle categorieën).
        // Eerst vragen door elkaar schudden, zodat elke ronde anders begint.
        _questions = Shuffle(DatabaseService.GetQuestions(_categoryId));
        if (_questions.Count == 0)
        {
            MessageBox.Show("Er zijn geen geldige vragen beschikbaar voor deze quiz.");
            Close();
            return;
        }

        // Randomly pick ANY question from the first 20 shuffled questions.
        // Move it to a random position among the first 10: it is still within
        // the first 20, and will actually be shown within a 60-second game.
        // This selects a fresh special question on every new normal quiz.
        // Alleen Normal heeft een speciale vraag. Bij Speed Quiz nooit.
        if (_mode == QuizMode.Normal)
        {
            int poolCount = Math.Min(20, _questions.Count);
            // Random.Next(n) kiest een geheel getal van 0 t/m n-1.
            // Kies per nieuwe quiz één vraag uit maximaal de eerste 20 geschudde vragen.
            int selectedIndex = Random.Shared.Next(poolCount);
            // Zet deze vraag op een willekeurige plek binnen de eerste 10 (dus ook eerste 20).
            // Dat maakt de bonusvraag zichtbaar tijdens een korte quiz van 60 seconden.
            int displayIndex = Random.Shared.Next(Math.Min(10, poolCount));
            // Tuple-swap wisselt twee vragen om zonder een extra tijdelijke variabele.
            (_questions[selectedIndex], _questions[displayIndex]) =
                (_questions[displayIndex], _questions[selectedIndex]);
            _specialQuestionIndex = displayIndex;
        }

        ShowNextQuestion();
        _quizWatch.Start();
        _uiTimer.Start();
        UpdateTimers();
    }

    // Generic helper: werkt voor zowel Question als Answer, met een willekeurige volgorde.
    private static List<T> Shuffle<T>(IEnumerable<T> source)
        => source.OrderBy(_ => Random.Shared.Next()).ToList();

    // De teller van getoonde vragen begint op 1; daarom vergelijken we met -1.
    // Deze check is alleen waar voor de gekozen bonusvraag in Normal-modus.
    private bool IsCurrentQuestionSpecial =>
        _mode == QuizMode.Normal && _questionsShown - 1 == _specialQuestionIndex;

    private void ShowNextQuestion()
    {
        if (_finished)
            return;

        _questionIndex++;
        // Wanneer alle vragen zijn geweest, worden ze opnieuw geschud.
        // Dit kan bij Speed Quiz nodig zijn wanneer veel antwoorden fout zijn.
        if (_questionIndex >= _questions.Count)
        {
            // Speed quiz can require more than one pass if many answers are wrong.
            _questions = Shuffle(_questions);
            _questionIndex = 0;
        }

        _questionsShown++;
        Question question = _questions[_questionIndex];
        QuestionText.Text = question.QuestionText;
        // De HUD toont bij Speed Quiz hoeveel van de 10 juiste antwoorden al behaald zijn.
        ProgressText.Text = _mode == QuizMode.Speed
            ? $"Vraag {_questionsShown} · {_correctAnswers}/{CorrectAnswersToWinSpeed} goed"
            : $"Vraag {_questionsShown}";
        StatusText.Text = $"{question.CategoryName} · Kies één antwoord";

        // De 4 database-antwoorden worden ELKE keer opnieuw in willekeurige volgorde gezet.
        List<Answer> answers = Shuffle(question.Answers);
        for (int i = 0; i < _answerButtons.Length; i++)
        {
            // Answer is stored in Tag: reshuffling never loses correctness.
            _answerButtons[i].Content = CreateAnswerContent((char)('A' + i), answers[i].AnswerText);
            // Tag bewaart het VOLLEDIGE Answer-object (incl. IsCorrect).
            // We controleren dus nooit of bijvoorbeeld "knop A" goed is.
            _answerButtons[i].Tag = answers[i];
            _answerButtons[i].IsEnabled = true;
        }

        bool special = IsCurrentQuestionSpecial;
        // De gouden banner en rand geven duidelijk aan dat dit een bonusvraag is.
        SpecialBanner.Visibility = special ? Visibility.Visible : Visibility.Collapsed;
        QuestionPanel.BorderBrush = special
            ? Brushes.Gold
            : (Brush)(Application.Current.TryFindResource("BorderBrush") ?? Brushes.SlateGray);

        if (special)
            PlaySpecialSound();

        _questionActive = true;
        // Elke nieuwe vraag krijgt opnieuw een eigen stopwatch (5 sec bij Normal).
        _questionWatch.Restart();
        UpdateTimers();
    }

    // Speelt het meegeleverde WAV-bestand af; als het ontbreekt gebruiken we een systeemgeluid.
    // try/catch is hier nuttig omdat afspelen kan falen door een ontbrekend/ongeldig bestand.
    private static void PlaySpecialSound()
    {
        // Original included WAV sound; fallback is a Windows system sound.
        string file = Path.Combine(AppContext.BaseDirectory, "Assets", "special-question.wav");
        try
        {
            if (File.Exists(file))
                new SoundPlayer(file).Play();
            else
                SystemSounds.Asterisk.Play();
        }
        catch (Exception)
        {
            SystemSounds.Asterisk.Play();
        }
    }

    // TextBlock is needed so longer answers wrap instead of being cut off.
    // We tonen antwoordletters en een TextBlock met TextWrapping voor lange antwoorden.
    private static FrameworkElement CreateAnswerContent(char letter, string answerText)
    {
        return new TextBlock
        {
            Text = $"{letter}.  {answerText}",
            TextWrapping = TextWrapping.Wrap,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = Brushes.White,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold
        };
    }

    // Één click-handler voor alle vier knoppen. Welke knop geraakt is staat in sender.
    private void Answer_Click(object sender, RoutedEventArgs e)
    {
        // Guard clause: geen dubbele scores, geen klikken na einde, geen uitgeschakelde 50/50-antwoorden.
        if (_finished || !_questionActive || sender is not Button button ||
            !button.IsEnabled || button.Tag is not Answer answer)
            return;

        // Check the actual elapsed time, not only a delayed UI timer tick.
        // Controleer rechtstreeks de Stopwatch: een UI-tick kan iets te laat plaatsvinden.
        // Zo telt een antwoord na het verstrijken van 60 sec nooit meer mee.
        if (_mode == QuizMode.Normal && _quizWatch.Elapsed >= TimeSpan.FromSeconds(NormalQuizSeconds))
        {
            FinishNormalQuiz();
            return;
        }
        // Ook na 5 sec mag een vraag niet alsnog worden beantwoord.
        if (_mode == QuizMode.Normal && _questionWatch.Elapsed >= TimeSpan.FromSeconds(NormalQuestionSeconds))
        {
            ShowNextQuestion();
            return;
        }

        // Zet de vraag meteen op inactief voordat we punten optellen.
        _questionActive = false;
        DisableAnswers();

        if (answer.IsCorrect)
        {
            // Het aantal juiste antwoorden bepaalt wanneer Speed Quiz eindigt.
            _correctAnswers++;
            if (_mode == QuizMode.Normal)
            {
                _score += PointsPerCorrect;
                // Bonusvraag = 10 gewone punten + 20 extra = 30 punten totaal.
                if (IsCurrentQuestionSpecial)
                    _score += SpecialBonusPoints;
                ScoreText.Text = $"Score: {_score}";
            }
            else
            {
                ScoreText.Text = $"Goed: {_correctAnswers} / {CorrectAnswersToWinSpeed}";
                // Alleen bij het TIENDE goede antwoord eindigt Speed Quiz.
                if (_correctAnswers >= CorrectAnswersToWinSpeed)
                {
                    FinishSpeedQuiz();
                    return;
                }
            }
        }
        else if (_mode == QuizMode.Speed)
        {
            // Straf is 5 sec per fout antwoord. We stoppen de echte klok hiervoor NIET.
            // De formule (elapsed + wrong*5000) wordt gebruikt bij tonen én bewaren.
            _wrongAnswers++;
            // Each wrong answer adds 5 seconds to the final result, not the wall clock.
        }

        ShowNextQuestion();
    }

    // 50/50: laat het correcte antwoord en precies één GELoOT fout antwoord staan.
    // De overige twee knoppen blijven zichtbaar maar zijn niet meer klikbaar.
    private void FiftyFifty_Click(object sender, RoutedEventArgs e)
    {
        if (_finished || !_questionActive || !_fiftyFiftyAvailable)
            return;

        // Keep the correct answer and exactly ONE random incorrect answer.
        // Zoek de juiste knop via Answer.IsCorrect, onafhankelijk van antwoordvolgorde.
        Button? correctButton = _answerButtons.FirstOrDefault(b => (b.Tag as Answer)?.IsCorrect == true);
        Button[] wrongButtons = _answerButtons
            .Where(b => (b.Tag as Answer)?.IsCorrect == false)
            .ToArray();
        if (correctButton is null || wrongButtons.Length != 3)
            return;

        // Kies willekeurig 1 van de 3 onjuiste antwoorden die mag blijven staan.
        Button randomWrongButton = wrongButtons[Random.Shared.Next(wrongButtons.Length)];
        foreach (Button button in _answerButtons)
            // Alleen de juiste knop + geloot fout antwoord blijven enabled.
            button.IsEnabled = button == correctButton || button == randomWrongButton;

        // Joker direct verbruiken, ook als de speler uiteindelijk fout antwoordt.
        _fiftyFiftyAvailable = false;
        FiftyFiftyButton.IsEnabled = false;
        FiftyFiftyButton.Content = "50/50 gebruikt";
        StatusText.Text = "50/50: de twee overgebleven antwoorden zijn selecteerbaar.";
    }

    // De oorspronkelijke skip-joker slaat één vraag over zonder puntenaftrek.
    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_finished || !_questionActive || !_skipAvailable)
            return;

        _questionActive = false;
        _skipAvailable = false;
        SkipButton.IsEnabled = false;
        SkipButton.Content = "Joker gebruikt";
        ShowNextQuestion();
    }

    // UI-update: normale quiz controleert beide limieten; Speed Quiz heeft geen vraaglimiet.
    private void UiTimer_Tick(object? sender, EventArgs e)
    {
        if (_finished)
            return;

        if (_mode == QuizMode.Normal)
        {
            if (_quizWatch.Elapsed >= TimeSpan.FromSeconds(NormalQuizSeconds))
            {
                FinishNormalQuiz();
                return;
            }

            if (_questionWatch.Elapsed >= TimeSpan.FromSeconds(NormalQuestionSeconds))
                ShowNextQuestion();
        }

        UpdateTimers();
    }

    private void UpdateTimers()
    {
        // Bij Speed Quiz loopt de tijd omhoog. Strafseconden komen daar direct bij op.
        if (_mode == QuizMode.Speed)
        {
            long totalSoFar = _quizWatch.ElapsedMilliseconds + _wrongAnswers * 5000L;
            QuizTimerText.Text = FormatTime(totalSoFar);
            PenaltyText.Text = $"{_wrongAnswers} fout · +{_wrongAnswers * 5} sec straftijd";
            return;
        }

        // Ceiling zorgt dat we 60, 59, ... en daarna 0 tonen (geen schokkerige afronding).
        int remainingQuiz = Math.Max(0, (int)Math.Ceiling(NormalQuizSeconds - _quizWatch.Elapsed.TotalSeconds));
        int remainingQuestion = Math.Max(0, (int)Math.Ceiling(NormalQuestionSeconds - _questionWatch.Elapsed.TotalSeconds));
        QuizTimerText.Text = remainingQuiz.ToString();
        QuestionTimerText.Text = remainingQuestion.ToString();
        QuizTimerText.Foreground = remainingQuiz <= 10 ? Brushes.OrangeRed :
            (Brush)(Application.Current.TryFindResource("TextBrush") ?? Brushes.White);
    }

    // Vertaalt milliseconden naar minuten:seconden.tienden voor het scherm.
    private static string FormatTime(long milliseconds)
    {
        TimeSpan t = TimeSpan.FromMilliseconds(milliseconds);
        return t.TotalHours >= 1
            ? $"{(long)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}"
            : $"{t.Minutes:00}:{t.Seconds:00}.{t.Milliseconds / 100}";
    }

    private void DisableAnswers()
    {
        foreach (Button button in _answerButtons)
            button.IsEnabled = false;
    }

    // Centrale stopmethode: stopt beide stopwatches en schakelt knoppen uit.
    // Hierdoor kunnen timers of gebruikersklikken geen extra resultaat veroorzaken.
    private void StopQuiz()
    {
        _finished = true;
        _questionActive = false;
        _questionWatch.Stop();
        _quizWatch.Stop();
        _uiTimer.Stop();
        DisableAnswers();
        SkipButton.IsEnabled = false;
        FiftyFiftyButton.IsEnabled = false;
    }

    // Classic game: score opslaan in Games en positie in de Top 10 uitrekenen.
    private void FinishNormalQuiz()
    {
        if (_finished)
            return;

        StopQuiz();
        long gameId = DatabaseService.SaveGame(_user.Id, _score, _categoryId);
        // Rang wordt uit de database bepaald (ook met scores van vorige sessies).
        int rank = DatabaseService.GetGameRank(gameId);
        Hide();
        new ResultWindow(_score, rank) { Owner = Owner }.ShowDialog();
        Close();
    }

    // Speed Quiz: sla tijd + foute antwoorden op in SpeedRuns, NIET in Games.
    private void FinishSpeedQuiz()
    {
        if (_finished)
            return;

        StopQuiz();
        long elapsedMs = _quizWatch.ElapsedMilliseconds;
        long currentId = DatabaseService.SaveSpeedRun(_user.Id, elapsedMs, _wrongAnswers);
        SpeedRunResult best = DatabaseService.GetFastestSpeedRun()
            ?? throw new InvalidOperationException("Het snelheidsrecord kon niet worden geladen.");
        // Als de zojuist opgeslagen run ook de snelste is, heeft deze speler Top 1.
        // Bij een gelijke tijd blijft de oudere recordhouder op #1 staan.
        bool newRecord = best.Id == currentId;
        Hide();
        new ResultWindow(elapsedMs, _wrongAnswers, best, newRecord) { Owner = Owner }.ShowDialog();
        Close();
    }

    // Bij vroegtijdig sluiten timers stoppen om geen updates op een gesloten scherm te doen.
    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        _uiTimer.Stop();
        _quizWatch.Stop();
        _questionWatch.Stop();
    }
}
