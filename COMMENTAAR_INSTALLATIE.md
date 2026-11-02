# Commentaar-update voor je bestaande Quizmester-project

## Wat is gewijzigd?

Alle 20 C#-bestanden bevatten nu Nederlandstalige uitlegcommentaren bij de belangrijkste spelregels, databasequeries, foutafhandeling, accountbeheer, password hashing en UI-events. De **uitvoerbare C#-regels zijn niet gewijzigd**. De `.xaml`-bestanden, assets en het `.csproj`-bestand zijn identiek aan het project `Quizmester_three_new_features.zip`.

`DOCENT_UITLEG.md` bevat extra vragen en antwoorden om je voor te bereiden op de beoordeling.

## Welke ZIP gebruiken?

- `Quizmester_met_docent_comments.zip`: compleet project, gebaseerd op de eerdere aangeleverde drie-features-versie.
- `Quizmester_alleen_CSharp_comments.zip`: alleen de geannoteerde C#-bestanden + deze handleidingen. Aanbevolen voor het overnemen in jouw eigen bestaande project.

## Overzetten naar je eigen project

1. Maak een backup van je huidige project.
2. Vergelijk eerst de `.cs`-bestanden met die uit jouw project. Als je na de vorige versie nog zelf spelregels hebt aangepast, neem dan **alleen de commentaren** over of merge met Visual Studio/Git.
3. Neem geen `.xaml`-bestanden over: deze update bevat geen aanpassingen aan je design.
4. Open je eigen `.sln` in Visual Studio, Restore NuGet Packages en Rebuild Solution.
5. Test vooral login, normale quiz, bonusvraag, 50/50, Speed Quiz en adminbeheer.

De geüploade `.sln` is alleen een solutionbeschrijving. Daarom is niet vastgesteld of jouw lokale C#-code exact gelijk is aan de eerdere drie-features-versie.
