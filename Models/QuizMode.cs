// Enum: bepaalt of de QuizWindow normale tijd/score-regels of Speed Quiz-regels gebruikt.
namespace Quizmester.Models;

// Normal: 60-second quiz with score and special question.
// Speed: no time limits; finish after ten correct answers.
public enum QuizMode
{
    Normal,
    Speed
}
