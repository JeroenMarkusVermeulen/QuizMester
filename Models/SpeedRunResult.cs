// Model: één SpeedRuns-databaserij inclusief speler, echte tijd, straf en totale tijd.
namespace Quizmester.Models;

public sealed class SpeedRunResult
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public long ElapsedMilliseconds { get; set; }
    public int WrongAnswers { get; set; }
    public long TotalMilliseconds { get; set; }
    public DateTime PlayedAt { get; set; }
}
