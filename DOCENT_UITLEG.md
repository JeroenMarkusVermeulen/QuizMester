# Quizmester – uitleg voor de beoordeling

Deze uitleg hoort bij de **C#-commentaren in de projectbestanden**. De commentaren zijn in het Nederlands en benoemen waarom een keuze is gemaakt, niet alleen wat de regel letterlijk doet.

> **Welke versie?** Dit is de eerdere Quizmester-versie met de 3 extra functies (50/50, bonusvraag, Speed Quiz), niet automatisch je eventuele nieuwere lokale light/dark-mode. Bij overzetten naar je eigen project hoef je alleen de gewijzigde `.cs`-bestanden te vergelijken/overnemen.

## Snel weten waar je moet kijken

| Vraag van docent | Bestand / methode |
|---|---|
| Hoe wordt de database gemaakt? | `Services/DatabaseService.cs` → `Initialize()` |
| Wat zijn primary en foreign keys? | `DatabaseService.cs` → de SQL `CREATE TABLE`-instructies |
| Waarom bestaat General niet als tabelrij? | `DatabaseService.cs` → `GetQuestions(int? categoryId)` |
| Hoe werkt registreren en uniek username? | `Windows/RegisterWindow.xaml.cs` → `Register_Click()` en `DatabaseService.CreateUser()` |
| Hoe worden wachtwoorden beveiligd? | `Services/PasswordHasher.cs` → `HashPassword()` en `VerifyPassword()` |
| Waar staat de demo-admin? | `DatabaseService.cs` → `SeedAdmin()` |
| Hoe werkt het aanmelden? | `Windows/LoginWindow.xaml.cs` → `Login_Click()` en `DatabaseService.Authenticate()` |
| Hoe worden antwoorden gerandomiseerd? | `Windows/QuizWindow.xaml.cs` → `Shuffle()` en `ShowNextQuestion()` |
| Waarom is antwoord A niet altijd correct? | `QuizWindow.xaml.cs` → `.Tag = answers[i]` en `Answer_Click()` |
| Hoe werken beide timers? | `QuizWindow.xaml.cs` → `_quizWatch`, `_questionWatch`, `UiTimer_Tick()`, `UpdateTimers()` |
| Hoe werkt het overslaan? | `QuizWindow.xaml.cs` → `Skip_Click()` |
| Hoe werkt de 50/50? | `QuizWindow.xaml.cs` → `FiftyFifty_Click()` |
| Waarom is de bonusvraag elk potje anders? | `QuizWindow.xaml.cs` → `QuizWindow_Loaded()` en `IsCurrentQuestionSpecial` |
| Hoe werkt het geluid? | `QuizWindow.xaml.cs` → `PlaySpecialSound()` en `Assets/special-question.wav` |
| Hoe werkt Speed Quiz? | `QuizWindow.xaml.cs` → `QuizMode.Speed`, `Answer_Click()` en `FinishSpeedQuiz()` |
| Hoe worden strafseconden berekend? | `DatabaseService.cs` → `SaveSpeedRun()` |
| Waarom aparte tabellen voor records? | `DatabaseService.cs` → `Games` versus `SpeedRuns` |
| Hoe wordt de Top 10 bepaald? | `DatabaseService.cs` → `GetTopScores()` en `GetGameRank()` |
| Hoe werkt het snelste record ooit? | `DatabaseService.cs` → `GetFastestSpeedRun()` |
| Hoe wordt adminbeheer gedaan? | `Windows/AdminWindow.xaml.cs` en `QuestionEditorWindow.xaml.cs` |
| Waarom transacties? | `DatabaseService.cs` → `AddQuestion()` en `UpdateQuestion()` |

## 15 vragen die de docent kan stellen

**1. Waarom gebruik je classes en modellen?**

`User`, `Question`, `Answer`, `Category`, `ScoreEntry` en `SpeedRunResult` bundelen gegevens die bij elkaar horen. In plaats van overal losse strings en integers door te geven, werken de vensters met objecten.

**2. Waarom gebruik je een aparte DatabaseService?**

Om SQL op één plek te bewaren. Het formulier roept bijvoorbeeld `CreateUser(...)` aan, maar hoeft niet te weten hoe `INSERT INTO Users` is opgebouwd.

**3. Wat doet `using var connection`?**

De databaseverbinding wordt aan het einde van de scope automatisch opgeruimd, ook als onderweg een exceptie optreedt.

**4. Wat is het verschil tussen een primary key en een foreign key?**

`Users.Id` identificeert een gebruiker uniek. `Games.UserId` verwijst via een foreign key naar die gebruiker. Een `Question.Id` wordt door `Answers.QuestionId` gebruikt om de juiste vier antwoorden op te halen.

**5. Waarom gebruik je SQL-parameters (`@username`) in plaats van tekst plakken?**

Parameters behandelen gebruikersinvoer als data. Daardoor kan een gebruiker met speciale tekens niet de bedoeling van je SQL-query aanpassen (SQL-injectie).

**6. Waarom geen plaintext wachtwoorden?**

`PasswordHasher.HashPassword` gebruikt PBKDF2 met een willekeurige salt en bewaart alleen iteraties, salt en hash. Bij login wordt dezelfde berekening opnieuw uitgevoerd en vergeleken. Het oorspronkelijke wachtwoord is niet uit de opgeslagen hash af te lezen.

**7. Waar worden het adminaccount en adminwachtwoord gemaakt?**

`SeedAdmin()` in `DatabaseService.cs`: standaard `admin` / `Admin123!` voor de demo, uitsluitend als de gebruiker `admin` nog niet bestaat. In SQLite staat de hash. De standaardgegevens zijn ongeschikt als definitieve productiewachtwoorden.

**8. Waarom zijn antwoorden willekeurig en hoe weet je wat correct is?**

`Shuffle(question.Answers)` verandert de volgorde. Elke antwoordknop krijgt vervolgens het volledige `Answer`-object in `.Tag`, met de property `IsCorrect`. Daarom kan het correcte antwoord op A, B, C of D verschijnen.

**9. Waarom gebruik je een Stopwatch en ook een DispatcherTimer?**

`Stopwatch` meet verstreken tijd onafhankelijk van het tekenen van de UI. `DispatcherTimer` vernieuwt alleen de zichtbare tijd en controleert regelmatig of een limiet is bereikt. Een late UI-update voegt geen extra antwoordtijd toe.

**10. Hoe werkt de 50/50?**

Eerst wordt de knop met `IsCorrect == true` gevonden. Daarna worden de drie foutieve knoppen verzameld, waarvan één willekeurig gekozen wordt. Alleen de juiste en die ene foutieve knop blijven `IsEnabled = true`. De joker kan maar één keer.

**11. Hoe werkt de speciale vraag?**

Bij elke nieuwe reguliere quiz worden de vragen geschud. Er wordt een willekeurige vraag uit maximaal de eerste twintig gekozen en op een willekeurige plek binnen de eerste tien gezet. Dat is gegarandeerd binnen de eerste twintig. Op die plek verschijnt een gouden designaccent en klinkt het WAV-bestand. Goed antwoord: 10 + 20 bonus = 30 punten. Speed Quiz heeft géén speciale vraag.

**12. Wanneer stopt Speed Quiz?**

Bij precies tien **correcte** antwoorden. Foute antwoorden tellen niet mee voor die tien, maar voegen 5 seconden straf toe. Er is geen tijdslimiet voor een vraag en geen puntenscore voor die modus.

**13. Hoe bereken je de eindtijd?**

`TotalMilliseconds = ElapsedMilliseconds + WrongAnswers * 5000`. Voorbeeld: 42 seconden gespeeld en twee keer fout is 42 + 2×5 = **52 seconden** eindtijd.

**14. Waarom een aparte SpeedRuns-tabel?**

De normale top 10 is gebaseerd op punten (`Games.Score`). Een snelheidsrecord moet op de laagste eindtijd worden bepaald, dus `SpeedRuns.TotalMilliseconds`. De twee rankings mogen elkaar niet beïnvloeden.

**15. Wat doet een database-transactie?**

Wanneer je een vraag toevoegt, moeten ook precies vier antwoordrecords worden opgeslagen. Met `BeginTransaction()` en `Commit()` worden alle wijzigingen samen opgeslagen; als een bewerking tussendoor faalt, kan de transactie worden teruggedraaid. Zo vermijd je een vraag zonder bijbehorende antwoorden.

## Databasestructuur

```text
Users (Id PK, Username UNIQUE, PasswordHash, Role)
  ├── Games (UserId FK, Score, CategoryId FK/NULL, PlayedAt)
  └── SpeedRuns (UserId FK, ElapsedMilliseconds, WrongAnswers,
                 TotalMilliseconds, PlayedAt)

Categories (Id PK, Name UNIQUE)
  └── Questions (Id PK, CategoryId FK, QuestionText)
       └── Answers (Id PK, QuestionId FK, AnswerText, IsCorrect)
```

General is **geen tabelrij in Categories**: `GetQuestions(null)` haalt alle beschikbare vragen op.

## Demo-volgorde voor beoordeling

1. Log in met bestaande speler, en registreer daarna ook een nieuwe gebruiker.
2. Speel een normale quiz, gebruik één keer skip en één keer 50/50.
3. Laat de goudkleurige speciale vraag met geluid zien en verdien bonuspunten.
4. Laat de 60-seconden timer eindigen en bekijk de score/top 10.
5. Start Speed Quiz, maak bewust één fout antwoord (+5 sec) en speel tot 10 goede antwoorden.
6. Vergelijk je eindtijd met het snelste record (Top 1).
7. Log in als admin en toon toevoegen/bewerken/verwijderen van vragen en gebruikers.
8. Toon `DatabaseService.cs` en `PasswordHasher.cs` om de technische keuzes toe te lichten.

## Beperkingen die je eerlijk kunt benoemen

- Dit is een **lokale desktopapplicatie** met een SQLite-bestand; er is geen online server of cloud-ranglijst.
- De demo-admin heeft een vast initieel wachtwoord in de broncode. Dat moet in een echte applicatie via veilige configuratie/initialisatie anders.
- De UI kan bij onverwachte fouten een melding geven, maar dat betekent niet dat iedere mogelijke fout automatisch veilig kan worden hersteld.
- De speciale vraag wordt binnen de eerste tien getoond (en voldoet daarmee aan de eis 'binnen de eerste twintig').
- Comments helpen bij het uitleggen van de code, maar je moet de belangrijke methodes daadwerkelijk kunnen aanwijzen en testen.
