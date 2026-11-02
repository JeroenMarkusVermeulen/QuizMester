using System.IO;
using Microsoft.Data.Sqlite;
using Quizmester.Models;

namespace Quizmester.Services;

// Centrale toegang tot SQLite: schermen kennen deze methods, maar hoeven zelf geen SQL te schrijven.
// static: er is maar één gezamenlijke database-service nodig voor het programma.
public static class DatabaseService
{
    // Databasebestand naast het .exe-bestand; AppContext.BaseDirectory wijst naar de programmamap.
    // readonly: na opstarten verandert dit pad niet.
    private static readonly string DatabasePath = Path.Combine(AppContext.BaseDirectory, "quizmester.db");
    private static readonly string ConnectionString = $"Data Source={DatabasePath}";

    // Wordt vanuit App.xaml.cs één keer bij het opstarten aangeroepen.
    // CREATE TABLE IF NOT EXISTS bewaart al bestaande accounts, vragen en scores.
    public static void Initialize()
    {
        using var connection = OpenConnection();

        // SQL-schema met primaire sleutels (Id) en foreign keys voor relaties:
        // Users -> Games/SpeedRuns, Categories -> Questions, Questions -> Answers.
        // UNIQUE Username voorkomt dubbele namen, zelfs bij gelijktijdige registraties.
        // CHECK beperkt o.a. rollen, scores en IsCorrect tot geldige waarden.
        // SpeedRuns is bewust apart: tijdrecords mogen niet tussen gewone scores staan.
        // Indexen versnellen zoeken op categorie, antwoorden en score/ranglijst.
        string sql = """
        CREATE TABLE IF NOT EXISTS Users (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Username TEXT NOT NULL COLLATE NOCASE UNIQUE,
            PasswordHash TEXT NOT NULL,
            Role TEXT NOT NULL DEFAULT 'User' CHECK(Role IN ('User', 'Admin'))
        );

        CREATE TABLE IF NOT EXISTS Categories (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            Name TEXT NOT NULL COLLATE NOCASE UNIQUE
        );

        CREATE TABLE IF NOT EXISTS Questions (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            QuestionText TEXT NOT NULL,
            CategoryId INTEGER NOT NULL,
            FOREIGN KEY(CategoryId) REFERENCES Categories(Id) ON DELETE RESTRICT
        );

        CREATE TABLE IF NOT EXISTS Answers (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            QuestionId INTEGER NOT NULL,
            AnswerText TEXT NOT NULL,
            IsCorrect INTEGER NOT NULL CHECK(IsCorrect IN (0,1)),
            FOREIGN KEY(QuestionId) REFERENCES Questions(Id) ON DELETE CASCADE
        );

        CREATE TABLE IF NOT EXISTS Games (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            Score INTEGER NOT NULL CHECK(Score >= 0),
            CategoryId INTEGER NULL,
            PlayedAt TEXT NOT NULL,
            FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE,
            FOREIGN KEY(CategoryId) REFERENCES Categories(Id) ON DELETE SET NULL
        );

        -- Separate table: speed-run results never appear in the regular top 10.
        -- Existing databases are migrated non-destructively by CREATE IF NOT EXISTS.
        CREATE TABLE IF NOT EXISTS SpeedRuns (
            Id INTEGER PRIMARY KEY AUTOINCREMENT,
            UserId INTEGER NOT NULL,
            ElapsedMilliseconds INTEGER NOT NULL CHECK(ElapsedMilliseconds >= 0),
            WrongAnswers INTEGER NOT NULL CHECK(WrongAnswers >= 0),
            TotalMilliseconds INTEGER NOT NULL CHECK(TotalMilliseconds >= 0),
            PlayedAt TEXT NOT NULL,
            FOREIGN KEY(UserId) REFERENCES Users(Id) ON DELETE CASCADE
        );

        CREATE INDEX IF NOT EXISTS IX_SpeedRuns_TotalMilliseconds
            ON SpeedRuns(TotalMilliseconds ASC, Id ASC);
        CREATE INDEX IF NOT EXISTS IX_Questions_CategoryId ON Questions(CategoryId);
        CREATE INDEX IF NOT EXISTS IX_Answers_QuestionId ON Answers(QuestionId);
        CREATE INDEX IF NOT EXISTS IX_Games_Score ON Games(Score DESC);
        """;

        using (var command = connection.CreateCommand())
        {
            command.CommandText = sql;
            command.ExecuteNonQuery();
        }

        // Seed-methodes voegen begininhoud toe bij een LEGE database.
        // Ze overschrijven geen bestaande spelers of aangepaste vragen.
        SeedCategories(connection);
        SeedAdmin(connection);
        SeedQuestions(connection);
    }

    // Eén methode voor iedere databaseverbinding, zodat foreign keys overal actief zijn.
    // "using" bij de aanroepende methodes sluit verbindingen automatisch, ook bij fouten.
    private static SqliteConnection OpenConnection()
    {
        var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        using var pragma = connection.CreateCommand();
        // SQLite schakelt foreign-key-controle niet altijd standaard in.
        // ON DELETE CASCADE verwijdert b.v. gekoppelde answers wanneer een vraag verdwijnt.
        pragma.CommandText = "PRAGMA foreign_keys = ON;";
        pragma.ExecuteNonQuery();

        return connection;
    }

    // Controleert case-insensitive op bestaande naam; "Anna" en "anna" zijn dus gelijk.
    public static bool UsernameExists(string username)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = @username COLLATE NOCASE;";
        // Parameter (@username) voorkomt SQL-injectie: gebruikersinvoer is nooit SQL-code.
        command.Parameters.AddWithValue("@username", username.Trim());
        return Convert.ToInt32(command.ExecuteScalar()) > 0;
    }

    // Registreert een account. Standaardrol = User; een admin kan expliciet Admin meegeven.
    public static bool CreateUser(string username, string password, string role = "User")
    {
        username = username.Trim();
        // Alleen twee toegestane rollen: alles behalve Admin wordt User.
        role = role.Equals("Admin", StringComparison.OrdinalIgnoreCase) ? "Admin" : "User";

        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return false;

        if (UsernameExists(username))
            return false;

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Users (Username, PasswordHash, Role) VALUES (@username, @hash, @role);";
        command.Parameters.AddWithValue("@username", username);
        // Sla nooit een leesbaar wachtwoord op: PBKDF2 maakt een salted hash.
        command.Parameters.AddWithValue("@hash", PasswordHasher.HashPassword(password));
        command.Parameters.AddWithValue("@role", role);
        return command.ExecuteNonQuery() == 1;
    }

    // Login: haal één gebruikersrecord op en controleer het ingevoerde wachtwoord tegen de hash.
    // Resultaat null betekent: combinatie gebruikersnaam/wachtwoord ongeldig.
    public static User? Authenticate(string username, string password)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, PasswordHash, Role FROM Users WHERE Username = @username COLLATE NOCASE LIMIT 1;";
        command.Parameters.AddWithValue("@username", username.Trim());

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        string hash = reader.GetString(2);
        // Vergelijk niet rechtstreeks met PasswordHash: dezelfde invoer wordt met salt gehasht.
        if (!PasswordHasher.VerifyPassword(password, hash))
            return null;

        // Het teruggegeven User-object bevat Id/Username/Role, NIET het wachtwoord of hash.
        return new User
        {
            Id = reader.GetInt32(0),
            Username = reader.GetString(1),
            Role = reader.GetString(3)
        };
    }

    // Toon gebruikers in het adminoverzicht zonder ooit PasswordHash op te vragen.
    public static List<User> GetUsers()
    {
        var users = new List<User>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Username, Role FROM Users ORDER BY Username COLLATE NOCASE;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            users.Add(new User
            {
                Id = reader.GetInt32(0),
                Username = reader.GetString(1),
                Role = reader.GetString(2)
            });
        }

        return users;
    }

    // Account verwijderen: voorkom verwijderen van jezelf of de allerlaatste admin.
    // out message geeft de tekst van het resultaat terug aan het adminvenster.
    public static bool DeleteUser(int userId, int currentUserId, out string message)
    {
        if (userId == currentUserId)
        {
            message = "Je kunt het account waarmee je nu bent ingelogd niet verwijderen.";
            return false;
        }

        using var connection = OpenConnection();

        string? role = null;
        using (var roleCommand = connection.CreateCommand())
        {
            roleCommand.CommandText = "SELECT Role FROM Users WHERE Id = @id;";
            roleCommand.Parameters.AddWithValue("@id", userId);
            role = roleCommand.ExecuteScalar()?.ToString();
        }

        if (role is null)
        {
            message = "De gebruiker bestaat niet meer.";
            return false;
        }

        // Bescherm de laatste beheerder zodat er altijd ten minste één admin blijft.
        if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
        {
            using var countCommand = connection.CreateCommand();
            countCommand.CommandText = "SELECT COUNT(*) FROM Users WHERE Role = 'Admin';";
            int adminCount = Convert.ToInt32(countCommand.ExecuteScalar());
            if (adminCount <= 1)
            {
                message = "De laatste administrator kan niet worden verwijderd.";
                return false;
            }
        }

        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Users WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", userId);
        // ExecuteNonQuery geeft het aantal aangepaste/verwijderde database-rijen terug.
        bool deleted = command.ExecuteNonQuery() == 1;
        message = deleted ? "Gebruiker verwijderd." : "Gebruiker kon niet worden verwijderd.";
        return deleted;
    }

    // Levert beschikbare categorieën voor de keuzelijst in CategoryWindow.
    public static List<Category> GetCategories()
    {
        var categories = new List<Category>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, Name FROM Categories ORDER BY Name;";

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            categories.Add(new Category
            {
                Id = reader.GetInt32(0),
                Name = reader.GetString(1)
            });
        }

        return categories;
    }

    // categoryId == null betekent General: ALLE vragen uit ALLE categorieën.
    // Anders filtert de SQL-query alleen vragen van de geselecteerde categorie.
    public static List<Question> GetQuestions(int? categoryId)
    {
        var questions = new List<Question>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();

        // De JOIN haalt de categorienaam op via de foreign key CategoryId.
        command.CommandText = categoryId.HasValue
            ? """
              SELECT q.Id, q.QuestionText, q.CategoryId, c.Name
              FROM Questions q
              JOIN Categories c ON c.Id = q.CategoryId
              WHERE q.CategoryId = @categoryId;
              """
            : """
              SELECT q.Id, q.QuestionText, q.CategoryId, c.Name
              FROM Questions q
              JOIN Categories c ON c.Id = q.CategoryId;
              """;

        if (categoryId.HasValue)
            command.Parameters.AddWithValue("@categoryId", categoryId.Value);

        using (var reader = command.ExecuteReader())
        {
            while (reader.Read())
            {
                questions.Add(new Question
                {
                    Id = reader.GetInt32(0),
                    QuestionText = reader.GetString(1),
                    CategoryId = reader.GetInt32(2),
                    CategoryName = reader.GetString(3)
                });
            }
        }

        // Antwoorden apart opvragen via QuestionId: één vraag heeft vier Answer-objecten.
        foreach (Question question in questions)
            question.Answers = GetAnswers(connection, question.Id);

        // Extra controle: quiz krijgt alleen vragen met precies 4 opties en 1 goed antwoord.
        // De adminvalidatie zorgt dat deze eis ook bij toevoegen/wijzigen klopt.
        return questions.Where(q => q.Answers.Count == 4 && q.Answers.Count(a => a.IsCorrect) == 1).ToList();
    }

    // Alfabetisch/categoriegewijs overzicht, zodat vragen in admin makkelijk te vinden zijn.
    public static List<Question> GetAllQuestionsForAdmin()
    {
        return GetQuestions(null).OrderBy(q => q.CategoryName).ThenBy(q => q.QuestionText).ToList();
    }

    // Laad één bestaande vraag inclusief antwoorden wanneer admin op Bewerken klikt.
    public static Question? GetQuestionById(int questionId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT q.Id, q.QuestionText, q.CategoryId, c.Name
            FROM Questions q
            JOIN Categories c ON c.Id = q.CategoryId
            WHERE q.Id = @id;
            """;
        command.Parameters.AddWithValue("@id", questionId);

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        var question = new Question
        {
            Id = reader.GetInt32(0),
            QuestionText = reader.GetString(1),
            CategoryId = reader.GetInt32(2),
            CategoryName = reader.GetString(3)
        };
        // Sluit reader vóór GetAnswers: dan kan dezelfde SQLite-verbinding opnieuw queryen.
        reader.Close();
        question.Answers = GetAnswers(connection, question.Id);
        return question;
    }

    // IsCorrect staat in SQLite als INTEGER 0/1; in C# gebruiken we een bool true/false.
    private static List<Answer> GetAnswers(SqliteConnection connection, int questionId)
    {
        var answers = new List<Answer>();
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id, QuestionId, AnswerText, IsCorrect FROM Answers WHERE QuestionId = @questionId;";
        command.Parameters.AddWithValue("@questionId", questionId);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            answers.Add(new Answer
            {
                Id = reader.GetInt32(0),
                QuestionId = reader.GetInt32(1),
                AnswerText = reader.GetString(2),
                IsCorrect = reader.GetInt32(3) == 1
            });
        }
        return answers;
    }

    // Admin-CRUD (Create): vraag en 4 antwoorden vormen één transactie.
    // Een transactie wordt pas vastgelegd bij Commit; zo ontstaan geen halve vragen.
    public static void AddQuestion(int categoryId, string questionText, IReadOnlyList<(string Text, bool IsCorrect)> answers)
    {
        ValidateQuestion(questionText, answers);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using var questionCommand = connection.CreateCommand();
        questionCommand.Transaction = transaction;
        questionCommand.CommandText = "INSERT INTO Questions (QuestionText, CategoryId) VALUES (@text, @categoryId); SELECT last_insert_rowid();";
        questionCommand.Parameters.AddWithValue("@text", questionText.Trim());
        questionCommand.Parameters.AddWithValue("@categoryId", categoryId);
        // last_insert_rowid() geeft het Id van de net toegevoegde vraag terug.
        // Dat Id gebruiken de gekoppelde answers als foreign key.
        long questionId = (long)(questionCommand.ExecuteScalar() ?? 0L);

        InsertAnswers(connection, transaction, questionId, answers);
        // Pas bij Commit worden alle wijzigingen definitief zichtbaar.
        transaction.Commit();
    }

    // Admin-CRUD (Update): pas bestaande vraag aan binnen een transactie.
    // Bestaande antwoorden vervangen is eenvoudiger dan vier losse updates.
    public static void UpdateQuestion(int questionId, int categoryId, string questionText, IReadOnlyList<(string Text, bool IsCorrect)> answers)
    {
        ValidateQuestion(questionText, answers);
        using var connection = OpenConnection();
        using var transaction = connection.BeginTransaction();

        using (var update = connection.CreateCommand())
        {
            update.Transaction = transaction;
            update.CommandText = "UPDATE Questions SET QuestionText = @text, CategoryId = @categoryId WHERE Id = @id;";
            update.Parameters.AddWithValue("@text", questionText.Trim());
            update.Parameters.AddWithValue("@categoryId", categoryId);
            update.Parameters.AddWithValue("@id", questionId);
            update.ExecuteNonQuery();
        }

        using (var deleteAnswers = connection.CreateCommand())
        {
            deleteAnswers.Transaction = transaction;
            deleteAnswers.CommandText = "DELETE FROM Answers WHERE QuestionId = @id;";
            deleteAnswers.Parameters.AddWithValue("@id", questionId);
            deleteAnswers.ExecuteNonQuery();
        }

        // Na het vervangen van de antwoorden worden vraag + answers als één geheel opgeslagen.
        InsertAnswers(connection, transaction, questionId, answers);
        transaction.Commit();
    }

    // Admin-CRUD (Delete): door ON DELETE CASCADE verdwijnen gekoppelde antwoorden ook.
    public static void DeleteQuestion(int questionId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM Questions WHERE Id = @id;";
        command.Parameters.AddWithValue("@id", questionId);
        command.ExecuteNonQuery();
    }

    // Validatie in de service én in het formulier: voorkomt ongeldige gegevens via andere aanroepen.
    private static void ValidateQuestion(string questionText, IReadOnlyList<(string Text, bool IsCorrect)> answers)
    {
        if (string.IsNullOrWhiteSpace(questionText))
            throw new ArgumentException("De vraag mag niet leeg zijn.");
        if (answers.Count != 4)
            throw new ArgumentException("Iedere vraag moet exact vier antwoorden hebben.");
        if (answers.Any(a => string.IsNullOrWhiteSpace(a.Text)))
            throw new ArgumentException("Alle vier antwoorden moeten ingevuld zijn.");
        if (answers.Count(a => a.IsCorrect) != 1)
            throw new ArgumentException("Er moet exact één correct antwoord zijn.");
    }

    // Deze helper wordt zowel bij toevoegen als bij wijzigen gebruikt: minder dubbele code.
    // De transactie komt van de aanroeper, zodat vraag + 4 antwoorden bij elkaar horen.
    private static void InsertAnswers(SqliteConnection connection, SqliteTransaction transaction, long questionId, IReadOnlyList<(string Text, bool IsCorrect)> answers)
    {
        foreach (var answer in answers)
        {
            using var answerCommand = connection.CreateCommand();
            answerCommand.Transaction = transaction;
            answerCommand.CommandText = "INSERT INTO Answers (QuestionId, AnswerText, IsCorrect) VALUES (@questionId, @text, @correct);";
            answerCommand.Parameters.AddWithValue("@questionId", questionId);
            answerCommand.Parameters.AddWithValue("@text", answer.Text.Trim());
            answerCommand.Parameters.AddWithValue("@correct", answer.IsCorrect ? 1 : 0);
            answerCommand.ExecuteNonQuery();
        }
    }

    // Normale quiz opslaan als één afgeronde game (score en speler).
    // Bij General is CategoryId = NULL, omdat General geen eigen categorie is.
    public static long SaveGame(int userId, int score, int? categoryId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO Games (UserId, Score, CategoryId, PlayedAt)
            VALUES (@userId, @score, @categoryId, @playedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@score", score);
        // DBNull.Value is de SQL-NULL-waarde, anders dan het C#-woord null.
        command.Parameters.AddWithValue("@categoryId", categoryId.HasValue ? categoryId.Value : DBNull.Value);
        command.Parameters.AddWithValue("@playedAt", DateTime.Now.ToString("O"));
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    // Rang van één uitgespeelde game: 1 + aantal games dat hoger eindigde.
    // Bij gelijke score komt de game met het laagste (oudste) Id eerst.
    public static int GetGameRank(long gameId)
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT 1 + COUNT(*)
            FROM Games
            WHERE Score > (SELECT Score FROM Games WHERE Id = @id)
               OR (Score = (SELECT Score FROM Games WHERE Id = @id) AND Id < @id);
            """;
        command.Parameters.AddWithValue("@id", gameId);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    // Classic top 10: hoogste scores, niet hoogste spelers (één speler kan vaker voorkomen).
    public static List<ScoreEntry> GetTopScores()
    {
        var scores = new List<ScoreEntry>();
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT u.Username, g.Score, g.PlayedAt
            FROM Games g
            JOIN Users u ON u.Id = g.UserId
            ORDER BY g.Score DESC, g.Id ASC
            LIMIT 10;
            """;

        using var reader = command.ExecuteReader();
        // De eerste rij uit ORDER BY is rang 1, daarna 2 enzovoort.
        int position = 1;
        while (reader.Read())
        {
            DateTime.TryParse(reader.GetString(2), out DateTime playedAt);
            scores.Add(new ScoreEntry
            {
                Position = position++,
                Username = reader.GetString(0),
                Score = reader.GetInt32(1),
                PlayedAt = playedAt
            });
        }
        return scores;
    }

    /// <summary>
    /// Stores a speed quiz separately from Games so it does not affect the score top 10.
    /// Total time = real elapsed time + five seconds for every wrong answer.
    /// </summary>
    // Speed Quiz record opslaan: werkelijke tijd (ms) + 5.000 ms per fout antwoord.
    // Milliseconden als long voorkomt afrondingsfouten bij snelle runs.
    public static long SaveSpeedRun(int userId, long elapsedMilliseconds, int wrongAnswers)
    {
        if (elapsedMilliseconds < 0 || wrongAnswers < 0)
            throw new ArgumentOutOfRangeException(nameof(elapsedMilliseconds));

        // checked signaleert (onwaarschijnlijke) integer-overflow in plaats van fout tijdrecord.
        long totalMilliseconds = checked(elapsedMilliseconds + (long)wrongAnswers * 5000L);

        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO SpeedRuns (UserId, ElapsedMilliseconds, WrongAnswers, TotalMilliseconds, PlayedAt)
            VALUES (@userId, @elapsed, @wrong, @total, @playedAt);
            SELECT last_insert_rowid();
            """;
        command.Parameters.AddWithValue("@userId", userId);
        command.Parameters.AddWithValue("@elapsed", elapsedMilliseconds);
        command.Parameters.AddWithValue("@wrong", wrongAnswers);
        command.Parameters.AddWithValue("@total", totalMilliseconds);
        command.Parameters.AddWithValue("@playedAt", DateTime.Now.ToString("O"));
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    /// <summary>
    /// Top 1 only. With equal times, the earliest run keeps the record.
    /// </summary>
    // Top 1: kortste totale tijd is de winnaar; bij gelijkspel blijft oudste run bovenaan.
    public static SpeedRunResult? GetFastestSpeedRun()
    {
        using var connection = OpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.Id, u.Username, s.ElapsedMilliseconds, s.WrongAnswers,
                   s.TotalMilliseconds, s.PlayedAt
            FROM SpeedRuns s
            JOIN Users u ON u.Id = s.UserId
            ORDER BY s.TotalMilliseconds ASC, s.Id ASC
            LIMIT 1;
            """;

        using var reader = command.ExecuteReader();
        if (!reader.Read())
            return null;

        DateTime.TryParse(reader.GetString(5), out DateTime playedAt);
        return new SpeedRunResult
        {
            Id = reader.GetInt64(0),
            Username = reader.GetString(1),
            ElapsedMilliseconds = reader.GetInt64(2),
            WrongAnswers = reader.GetInt32(3),
            TotalMilliseconds = reader.GetInt64(4),
            PlayedAt = playedAt
        };
    }

    // Voorbeeldcategorieën bij start: INSERT OR IGNORE maakt niets dubbel aan.
    private static void SeedCategories(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO Categories (Name) VALUES ('Sports'), ('Science');";
        command.ExecuteNonQuery();
    }

    // DEMO: maakt éénmalig het standaard adminaccount aan als de naam "admin" niet bestaat.
    // Verander deze vaste demo-credentials voor een echte uitgave; de DB bevat alleen een hash.
    private static void SeedAdmin(SqliteConnection connection)
    {
        using var exists = connection.CreateCommand();
        exists.CommandText = "SELECT COUNT(*) FROM Users WHERE Username = 'admin' COLLATE NOCASE;";
        if (Convert.ToInt32(exists.ExecuteScalar()) > 0)
            return;

        using var command = connection.CreateCommand();
        command.CommandText = "INSERT INTO Users (Username, PasswordHash, Role) VALUES ('admin', @hash, 'Admin');";
        // De demo-login is admin / Admin123! (alleen als dit account nog niet bestaat).
        // PasswordHasher bewaart salt+hash; bestaande wachtwoorden veranderen NIET mee.
        command.Parameters.AddWithValue("@hash", PasswordHasher.HashPassword("Admin123!"));
        command.ExecuteNonQuery();
    }

    // Voegt de 40 voorbeeldvragen eenmalig toe als de Questions-tabel leeg is.
    // Sports: 20, Science: 20. General wordt dynamisch samengesteld (geen aparte categorie).
    private static void SeedQuestions(SqliteConnection connection)
    {
        using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM Questions;";
        if (Convert.ToInt32(countCommand.ExecuteScalar()) > 0)
            return;

        int sportsId = GetCategoryId(connection, "Sports");
        int scienceId = GetCategoryId(connection, "Science");

        var sports = new (string Q, string Correct, string W1, string W2, string W3)[]
        {
            ("Hoeveel spelers staan er per team op het veld bij voetbal aan het begin van een wedstrijd?", "11", "9", "10", "12"),
            ("Hoeveel ringen heeft het olympische symbool?", "5", "4", "6", "7"),
            ("Bij welke sport hoort Wimbledon?", "Tennis", "Golf", "Cricket", "Hockey"),
            ("Hoeveel punten is een vrije worp waard bij basketbal?", "1", "2", "3", "4"),
            ("Welke sport hoort bij de Tour de France?", "Wielrennen", "Hardlopen", "Motorsport", "Schaatsen"),
            ("Hoeveel spelers staan er per team in het veld bij volleybal?", "6", "5", "7", "8"),
            ("Met hoeveel schaakstukken begint één speler een schaakpartij?", "16", "12", "14", "18"),
            ("Wat is de officiële afstand van een marathon?", "42,195 km", "40 km", "41,5 km", "45 km"),
            ("Welke vlag betekent het einde van een Formule 1-race?", "Zwart-wit geblokt", "Rood", "Geel", "Blauw"),
            ("Bij welke sport gebruik je een shuttle?", "Badminton", "Squash", "Tennis", "Padel"),
            ("Hoe ver ligt de penaltystip bij voetbal van de doellijn?", "11 meter", "9 meter", "10 meter", "12 meter"),
            ("Hoeveel sets moet een speler winnen in een best-of-five tenniswedstrijd?", "3", "2", "4", "5"),
            ("Hoeveel spelers staan er per team op het veld bij handbal?", "7", "5", "6", "8"),
            ("Wat is de maximale standaardbreak in snooker zonder bijzondere free-ball-situatie?", "147", "140", "150", "155"),
            ("Hoe hoog hangt een basketbalring ongeveer?", "3,05 meter", "2,75 meter", "3,50 meter", "4,00 meter"),
            ("Waarmee wordt bij ijshockey gespeeld?", "Een puck", "Een bal", "Een shuttle", "Een ring"),
            ("Wat is de juiste volgorde van een triatlon?", "Zwemmen, fietsen, hardlopen", "Fietsen, zwemmen, hardlopen", "Hardlopen, fietsen, zwemmen", "Zwemmen, hardlopen, fietsen"),
            ("Na hoeveel strikes is een slagman uit bij honkbal?", "3", "2", "4", "5"),
            ("Tot hoeveel punten wordt normaal een game bij tafeltennis gespeeld?", "11", "15", "21", "25"),
            ("Welke kaart betekent uitsluiting bij voetbal?", "Rode kaart", "Gele kaart", "Blauwe kaart", "Groene kaart")
        };

        var science = new (string Q, string Correct, string W1, string W2, string W3)[]
        {
            ("Waar staat de formule H2O voor?", "Water", "Zuurstof", "Waterstof", "Koolstofdioxide"),
            ("Welke planeet wordt de rode planeet genoemd?", "Mars", "Venus", "Jupiter", "Mercurius"),
            ("Wat is het grootste orgaan van het menselijk lichaam?", "De huid", "De lever", "De longen", "De hersenen"),
            ("Welk gas nemen planten voornamelijk op voor fotosynthese?", "Koolstofdioxide", "Zuurstof", "Stikstof", "Helium"),
            ("Wat is het scheikundige symbool voor goud?", "Au", "Ag", "Gd", "Go"),
            ("Hoeveel chromosomen heeft een normale menselijke lichaamscel?", "46", "23", "44", "48"),
            ("Bij welke temperatuur kookt zuiver water ongeveer op zeeniveau?", "100 °C", "90 °C", "110 °C", "120 °C"),
            ("Welke ster staat het dichtst bij de aarde?", "De zon", "Sirius", "Polaris", "Proxima Centauri"),
            ("In welk celonderdeel vindt fotosynthese voornamelijk plaats?", "Chloroplast", "Celkern", "Mitochondrium", "Ribosoom"),
            ("Wat beweegt sneller in vacuüm?", "Licht", "Geluid", "Ze bewegen even snel", "Dat hangt van de temperatuur af"),
            ("Welke pH-waarde is neutraal?", "7", "0", "5", "14"),
            ("Wat is de SI-eenheid van elektrische stroomsterkte?", "Ampère", "Volt", "Watt", "Ohm"),
            ("Welke stof is NaCl?", "Keukenzout", "Water", "Suiker", "Koolstofdioxide"),
            ("Wat is de natuurlijke satelliet van de aarde?", "De maan", "Mars", "De zon", "Venus"),
            ("Waar staat DNA voor?", "Desoxyribonucleïnezuur", "Dynamisch nucleair atoom", "Dubbel neutronenatoom", "Digitale nucleïne-analyse"),
            ("Welk orgaan pompt bloed door het menselijk lichaam?", "Het hart", "De lever", "De nier", "De long"),
            ("Wat is het atoomnummer van zuurstof?", "8", "6", "7", "10"),
            ("Hoe heet de overgang waarbij een vaste stof direct gas wordt?", "Sublimatie", "Condensatie", "Stolling", "Verdamping"),
            ("Welke kracht trekt voorwerpen naar de aarde?", "Zwaartekracht", "Magnetisme", "Wrijving", "Opwaartse kracht"),
            ("Wat is de kleinste eenheid van een chemisch element die de eigenschappen van dat element behoudt?", "Een atoom", "Een cel", "Een molecuul water", "Een protonenpaar")
        };

        foreach (var item in sports)
            SeedQuestion(connection, sportsId, item);
        foreach (var item in science)
            SeedQuestion(connection, scienceId, item);
    }

    // Zoek het database-Id bij de categorienaam, zodat seedvragen juiste FK krijgen.
    private static int GetCategoryId(SqliteConnection connection, string name)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT Id FROM Categories WHERE Name = @name COLLATE NOCASE;";
        command.Parameters.AddWithValue("@name", name);
        return Convert.ToInt32(command.ExecuteScalar());
    }

    // Voeg één seedvraag en precies vier antwoorden toe in één transactie.
    // item.Correct = true; de drie andere antwoordopties zijn false.
    private static void SeedQuestion(SqliteConnection connection, int categoryId, (string Q, string Correct, string W1, string W2, string W3) item)
    {
        using var transaction = connection.BeginTransaction();
        using var questionCommand = connection.CreateCommand();
        questionCommand.Transaction = transaction;
        questionCommand.CommandText = "INSERT INTO Questions (QuestionText, CategoryId) VALUES (@q, @categoryId); SELECT last_insert_rowid();";
        questionCommand.Parameters.AddWithValue("@q", item.Q);
        questionCommand.Parameters.AddWithValue("@categoryId", categoryId);
        long questionId = (long)(questionCommand.ExecuteScalar() ?? 0L);

        var answers = new[]
        {
            (item.Correct, true),
            (item.W1, false),
            (item.W2, false),
            (item.W3, false)
        };

        InsertAnswers(connection, transaction, questionId, answers);
        transaction.Commit();
    }
}
