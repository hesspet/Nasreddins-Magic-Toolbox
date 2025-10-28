using System.Collections.Generic;

namespace Toolbox.Models.Questionnaire;

public class QuizQuestion
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;

    public string? Explanation { get; set; }

    public IList<QuizOption> Options { get; set; } = new List<QuizOption>();

    public bool AllowsMultiple { get; set; }

    public IList<string>? Tags { get; set; }
}

public class QuizOption
{
    public string Text { get; set; } = string.Empty;

    public bool IsCorrect { get; set; }

    public string? Description { get; set; }
}
