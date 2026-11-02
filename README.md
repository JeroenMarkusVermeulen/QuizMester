# Quizmester

Complete C# WPF .NET 8 + SQLite-uitwerking voor de Quizmester-opdracht.

## Vereisten
- Visual Studio 2022
- .NET 8 Desktop Development workload
- Internet bij de eerste build om `Microsoft.Data.Sqlite` via NuGet te herstellen

## Starten
1. Open `Quizmester.csproj` in Visual Studio.
2. Wacht tot NuGet packages zijn hersteld.
3. Start met F5.
4. `quizmester.db` wordt automatisch aangemaakt naast de executable.

## Standaard admin
- Gebruikersnaam: `admin`
- Wachtwoord: `Admin123!`

Verander dit voor de uiteindelijke demonstratie als je wilt.

## Wat is geïmplementeerd
- Classes en objects
- Eigen donker Quizmester-thema
- Registratie met unieke username
- Wachtwoorden gehasht met PBKDF2
- Login
- SQLite database
- 40 standaardvragen
- 20 Sports + 20 Science
- General = alle vragen
- Exact 4 antwoorden per vraag
- Antwoorden worden iedere vraag geshuffeld
- Correct antwoord controleren
- +10 punten per correct antwoord
- Quiztimer 60 seconden
- Laatste 10 seconden opvallend
- Vraagtimer 5 seconden
- Bij 0 automatisch volgende vraag
- Eén joker/skip per quiz
- Iedere game wordt opgeslagen
- Top 10 scoreboard met username + score
- Na afloop eindscore en rankingpositie
- Administratorrechten
- Admin: vraag toevoegen/bewerken/verwijderen
- Admin: gebruiker toevoegen/verwijderen
- Validatie en globale foutafhandeling

## Database
Tabellen:
- Users
- Categories
- Questions
- Answers
- Games

`General` staat bewust niet in Categories. General haalt alle vragen op.

## GitHub
De code kan niet voor jou aantonen dat je wekelijks hebt gewerkt. Maak dus iedere week zelf een betekenisvolle commit. Ook een week zonder C#-werk moet volgens de MoSCoW een commit bevatten die uitlegt waarom.

## Opleveren
De MoSCoW zegt dat de uiteindelijke ZIP-naam en exacte inlevermethode in de BPRJ-opdracht staan. Gebruik dus de naam uit die opdracht; die naam is niet opgenomen in de aangeleverde Quizmester-documenten.
