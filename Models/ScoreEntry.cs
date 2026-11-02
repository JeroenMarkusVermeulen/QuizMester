// Model: één rij voor de normale Top 10, met positie, naam, score en datum.
namespace Quizmester.Models;

public sealed class ScoreEntry
{
    public int Position { get; set; }
    public string Username { get; set; } = string.Empty;
    public int Score { get; set; }
    public DateTime PlayedAt { get; set; }
}
