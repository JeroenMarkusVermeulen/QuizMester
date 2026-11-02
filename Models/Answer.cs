// Model: tekst van een antwoord en IsCorrect (true/false). Knoppen bewaren dit object in Tag.
namespace Quizmester.Models;

public sealed class Answer
{
    public int Id { get; set; }
    public int QuestionId { get; set; }
    public string AnswerText { get; set; } = string.Empty;
    public bool IsCorrect { get; set; }
}
