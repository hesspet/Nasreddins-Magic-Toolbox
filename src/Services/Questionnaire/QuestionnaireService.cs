using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using Toolbox.Models.Questionnaire;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Toolbox.Services.Questionnaire;

public class QuestionnaireService
{
    private readonly HttpClient _httpClient;
    private readonly IDeserializer _deserializer;

    public QuestionnaireService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public async Task<IReadOnlyList<QuizQuestion>> LoadAllQuestionsAsync(CancellationToken cancellationToken = default)
    {
        var manifest = await _httpClient.GetFromJsonAsync<List<string>>("questions/manifest.json", cancellationToken)
            ?? new List<string>();

        var questions = new List<QuizQuestion>();

        foreach (var fileName in manifest)
        {
            var trimmedName = fileName?.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
            {
                continue;
            }

            try
            {
                var yaml = await _httpClient.GetStringAsync($"questions/{trimmedName}", cancellationToken);
                var question = _deserializer.Deserialize<QuizQuestion>(yaml);
                if (question is not null && question.Options.Count > 0)
                {
                    if (!question.AllowsMultiple)
                    {
                        var correctAnswers = question.Options.Count(option => option.IsCorrect);
                        question.AllowsMultiple = correctAnswers > 1;
                    }

                    questions.Add(question);
                }
            }
            catch (HttpRequestException)
            {
                // Ignore individual files that cannot be loaded.
            }
        }

        return questions;
    }
}
