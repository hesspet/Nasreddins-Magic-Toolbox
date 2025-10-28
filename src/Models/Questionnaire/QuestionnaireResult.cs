namespace Toolbox.Models.Questionnaire;

public record QuestionnaireResult(
    bool AllCorrect,
    int CorrectAnswers,
    int TotalQuestions,
    bool TimeExpired);
