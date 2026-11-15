using System.Text.Json;

namespace FrcResearchTest;

public static class QuestionBank
{
    public static QuestionBankFile Load()
    {
        if (!File.Exists(AppPaths.QuestionsPath))
        {
            throw new FileNotFoundException(
                "questions.json was not found next to the application.",
                AppPaths.QuestionsPath);
        }

        var json = File.ReadAllText(AppPaths.QuestionsPath);
        var bank = JsonSerializer.Deserialize<QuestionBankFile>(json, AppConfig.JsonOptions)
                   ?? throw new InvalidDataException("questions.json is empty or invalid.");

        bank.Questions ??= new List<Question>();
        if (bank.Questions.Count == 0)
        {
            throw new InvalidDataException("questions.json does not contain any questions.");
        }

        foreach (var question in bank.Questions)
        {
            question.Type = string.IsNullOrWhiteSpace(question.Type)
                ? "research"
                : question.Type.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(question.Id))
            {
                throw new InvalidDataException("Every question needs an \"id\".");
            }

            if (string.IsNullOrWhiteSpace(question.Prompt))
            {
                throw new InvalidDataException($"Question {question.Id} has no \"prompt\".");
            }

            if (question.Kind == QuestionType.Research && (question.Accept is null ||
                (question.Accept.All.Count == 0 && question.Accept.Any.Count == 0)))
            {
                throw new InvalidDataException(
                    $"Research question {question.Id} needs an \"accept\" rule (all/any keywords).");
            }
        }

        return bank;
    }
}
