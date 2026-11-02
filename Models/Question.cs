// Model: één quizvraag, gekoppeld aan een categorie en zijn vier antwoordobjecten.
namespace Quizmester.Models;

public sealed class Question
{
    public int Id { get; set; }
    public string QuestionText { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public List<Answer> Answers { get; set; } = new();
}
