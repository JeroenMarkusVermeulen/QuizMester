# Quizmester – three extra features (November 2026)

## Important: this is not necessarily your latest local UI

The uploaded `Quizmester.sln` contains only a reference to `Quizmester.csproj`, not the current source code. This updated solution was built from the **earlier Quizmester_complete.zip** available in the conversation. If you have since changed the appearance, especially light/dark mode, **make a backup and merge the changes instead of replacing all files**. The theme in this reference solution is the older dark one.

## New behavior

### 1. 50/50 joker

The classic and speed quiz now have a separate `50/50` button in addition to the existing skip joker. It can be used once per quiz and immediately disables exactly two incorrect answers. The correct answer and one randomly selected incorrect answer remain enabled. On the next question, the four answer buttons are available again, but the 50/50 joker remains used.

### 2. Special bonus question (classic only)

For every classic game, all questions are shuffled. One of the first 20 is chosen randomly and placed at a random slot among the first 10, to guarantee it is actually shown during the short 60-second quiz. It is always within the first 20. It displays a gold border and banner and plays the bundled original `Assets/special-question.wav` audio cue. Correct answer = **10 normal points + 20 bonus points = 30 points**. Wrong, skipped or timed out = 0 points. No special question in speed mode.

### 3. Speed Quiz

Accessible from the main menu. Questions are randomly selected from all categories and do not have per-question or overall time limits. The timer counts upwards including 5-second penalties. Every correct answer increments the count; the quiz automatically ends at **10 correct answers**, regardless of wrong answers along the way.

Example: 43.2 seconds real elapsed + 3 wrong answers (15 sec) = final time of **58.2 seconds**.

After completion it shows your real time, wrong answers/penalty, final adjusted time and the all-time fastest record holder and time. If your new run is faster it explicitly announces `NIEUW RECORD!`. Ties preserve the earlier record. No speed runs appear in the classic top 10.

## Database migration

`Services/DatabaseService.cs` adds a `SpeedRuns` table in `Initialize()` using `CREATE TABLE IF NOT EXISTS`. It does **not** delete or recreate existing Users, Questions, Answers or Games. Do not delete `quizmester.db` if you want to keep existing scores and accounts.

The database file is located at `AppContext.BaseDirectory/quizmester.db`, usually under your `bin/Debug/net8.0-windows` directory for a debug build. Back it up before switching/rebuilding.

## Files changed

- `Services/DatabaseService.cs` – schema migration and speed-run save/read methods; fixes missing `using System.IO;`
- `Models/QuizMode.cs` – new enum
- `Models/SpeedRunResult.cs` – new model
- `Windows/MainMenuWindow.xaml` – adds Speed Quiz button
- `Windows/MainMenuWindow.xaml.cs` – starts Speed Quiz
- `Windows/QuizWindow.xaml` – special banner, penalty/time display, 50/50 button, classic and speed info
- `Windows/QuizWindow.xaml.cs` – gameplay logic, timers, sound, bonus, 50/50 and speed mode
- `Windows/ResultWindow.xaml` – adds fields needed for speed result
- `Windows/ResultWindow.xaml.cs` – normal and speed result modes
- `Quizmester.csproj` – includes/copies WAV sound
- `Assets/special-question.wav` – original alert sound
- `Quizmester.sln` – uploaded solution file copied into the same folder as .csproj

## Merging with a more recent project

To preserve a newer light/dark theme:

1. Back up the entire current Visual Studio folder and its `quizmester.db` file.
2. Add new `Models/QuizMode.cs`, `Models/SpeedRunResult.cs` and `Assets/special-question.wav`.
3. Merge the `SpeedRuns` table and `SaveSpeedRun`/`GetFastestSpeedRun` methods into your existing `DatabaseService.cs` (or copy the file if you did not change the DB service).
4. Replace/merge `QuizWindow.xaml.cs` and `ResultWindow.xaml.cs`.
5. In your current `QuizWindow.xaml`, add the new named controls `ProgressText`, `SpecialBanner`, `PenaltyText`, `QuizTimerTitle`, `QuestionTimerPanel`, `QuestionPanel`, and `FiftyFiftyButton` with its `Click="FiftyFifty_Click"`. The provided XAML shows exactly how.
6. In your current `ResultWindow.xaml`, add `ResultTitleText`, `ScoreTitleText`, `DetailsText`, and name the existing top-10 button `ScoreboardButton`. Keep existing `ScoreText` and `RankText`.
7. In your current `MainMenuWindow.xaml`, add a button `Click="SpeedQuiz_Click"`; add that event handler to its code-behind. Do **not** remove your current theme toggle.
8. In the `.csproj`, add the content entry for copying the WAV file to output, shown in this package's `Quizmester.csproj`.
9. Rebuild Solution and run all the acceptance tests below.

To apply these modifications directly to your **latest** layout without manual merging, upload a ZIP containing `Quizmester.csproj`, `App.xaml`, the `Windows`, `Services`, `Models`, `Themes` folders and any `Assets` files. A `.sln` file alone does not contain those files.

## Acceptance checklist

- [ ] 50/50 can be used once per game.
- [ ] Exactly one correct and one RANDOM incorrect answer are still clickable; 2 other incorrect buttons disabled.
- [ ] Four buttons return on next question, but 50/50 cannot be used again.
- [ ] Existing Skip is still available independently once per game.
- [ ] Every classic game has one random bonus question during first 10 (therefore first 20) questions.
- [ ] Gold banner and gold border visible; audible special question sound.
- [ ] Only correct bonus answer adds 30 points; other correct answers add 10.
- [ ] Special question absent from speed quiz.
- [ ] Speed timer increases, no question timeout and no total quiz timeout.
- [ ] 10 correct answers end speed quiz; wrong answers add +5s each.
- [ ] Speed result shows real/penalty/final/best time and the top 1 user's name.
- [ ] Finishing with a new best announces it; no speed result in classic top 10.
- [ ] Restarting application preserves previous fastest speed time.
- [ ] Existing registration, login, admin, general categories and classic score top 10 still work.

## Build and testing limitation

XAML files have been XML-parsed, XAML named controls/click handlers cross-checked, and the SQL schema run against SQLite. This environment has no Windows WPF/.NET build toolchain, so an actual Visual Studio compilation and interactive Windows test have **not** been performed. Build and run in Visual Studio; report any build errors with their exact file/line.


## Docent-uitleg (nieuwe toevoeging)

In `DOCENT_UITLEG.md` staat een overzicht van mogelijke beoordelingsvragen.
Alle belangrijke `.cs`-bestanden bevatten nu Nederlandse uitlegcommentaren bij de methodes en kernregels.
De functionele code is voor deze commentaar-update niet aangepast.
