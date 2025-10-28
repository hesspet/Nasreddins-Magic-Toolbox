using System.Collections.Generic;
using YamlDotNet.Serialization;

namespace Toolbox.Models.Questionnaire;

public class QuizQuestion
{
    [YamlMember(Alias = "id")]
    public string Id { get; set; } = string.Empty;

    [YamlMember(Alias = "title")]
    public string Title { get; set; } = string.Empty;

    [YamlMember(Alias = "prompt")]
    public string Prompt { get; set; } = string.Empty;

    [YamlMember(Alias = "explanation")]
    public string? Explanation { get; set; }

    [YamlMember(Alias = "options")]
    public IList<QuizOption> Options { get; set; } = new List<QuizOption>();

    [YamlMember(Alias = "allowsMultiple")]
    public bool AllowsMultiple { get; set; }

    [YamlMember(Alias = "tags")]
    public IList<string>? Tags { get; set; }
}

public class QuizOption
{
    [YamlMember(Alias = "text")]
    public string Text { get; set; } = string.Empty;

    [YamlMember(Alias = "isCorrect")]
    public bool IsCorrect { get; set; }

    [YamlMember(Alias = "description")]
    public string? Description { get; set; }
}
