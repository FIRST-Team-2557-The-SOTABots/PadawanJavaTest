using System.Text.Json.Serialization;

namespace FrcResearchTest;

public enum QuestionType
{
    Research,
    Code,
}

public sealed class AcceptSpec
{
    public List<string> All { get; set; } = new();
    public List<string> Any { get; set; } = new();
}

public sealed class Question
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "research";
    public int Points { get; set; } = 1;
    public string Prompt { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string Language { get; set; } = "java";
    public string Rubric { get; set; } = "";
    public bool RequireCitation { get; set; } = true;
    public bool AutoScore { get; set; } = true;
    public AcceptSpec? Accept { get; set; }

    [JsonIgnore]
    public QuestionType Kind =>
        string.Equals(Type, "code", StringComparison.OrdinalIgnoreCase)
            ? QuestionType.Code
            : QuestionType.Research;

    [JsonIgnore]
    public string TypeLabel => Kind == QuestionType.Code ? "Programming" : "Research";
}

public sealed class QuestionBankFile
{
    public string Title { get; set; } = "FRC Research & Debugging Assessment";
    public List<Question> Questions { get; set; } = new();
}

public sealed class AnswerRecord
{
    public string QuestionId { get; set; } = "";
    public string Type { get; set; } = "research";
    public int Points { get; set; } = 1;
    public string Prompt { get; set; } = "";
    public string Snippet { get; set; } = "";
    public string Rubric { get; set; } = "";
    public string Answer { get; set; } = "";
    public string Citation { get; set; } = "";
    public bool KeywordMatch { get; set; }
    public string CitationStatus { get; set; } = "Empty";
    public bool CitationVerified { get; set; }
    public bool Credit { get; set; }
}

public sealed class SessionResult
{
    public string Student { get; set; } = "";
    public string Team { get; set; } = "";
    public string StartedAt { get; set; } = "";
    public string SubmittedAt { get; set; } = "";
    public int TimeLimitSeconds { get; set; }
    public int TimeUsedSeconds { get; set; }
    public bool AutoSubmitted { get; set; }
    public int AiAttempts { get; set; }
    public int Searches { get; set; }
    public int UniqueSites { get; set; }
    public int Unanswered { get; set; }
    public int ResearchTotal { get; set; }
    public int ResearchCorrect { get; set; }
    public int ResearchEffective { get; set; }
    public int CitationsVerified { get; set; }
    public List<string> Flags { get; set; } = new();
    public List<AnswerRecord> Answers { get; set; } = new();
    public string LogFile { get; set; } = "";
    public string ResultsFolder { get; set; } = "";
}
