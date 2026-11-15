using System.Text;
using System.Text.Json;

namespace FrcResearchTest;

public static class ResultsExporter
{
    public static (string JsonPath, string ReviewPath, string CsvPath) Export(
        AppConfig config,
        SessionResult result,
        string logPath)
    {
        var folder = config.EffectiveResultsFolder;
        Directory.CreateDirectory(folder);

        var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var baseName = $"{NameUtil.Safe(result.Student)}-{stamp}";
        var jsonPath = Path.Combine(folder, baseName + ".json");

        var suffix = 2;
        while (File.Exists(jsonPath))
        {
            jsonPath = Path.Combine(folder, $"{baseName}-{suffix++}.json");
        }

        var stem = Path.GetFileNameWithoutExtension(jsonPath);
        var reviewPath = Path.Combine(folder, stem + ".md");
        var csvPath = Path.Combine(folder, "summary.csv");

        result.LogFile = logPath;
        result.ResultsFolder = folder;

        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(result, AppConfig.JsonOptions),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        File.WriteAllText(
            reviewPath,
            BuildMarkdown(result, jsonPath),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));

        AppendSummaryCsv(csvPath, result, jsonPath, reviewPath);

        return (jsonPath, reviewPath, csvPath);
    }

    private static string BuildMarkdown(SessionResult result, string jsonPath)
    {
        var builder = new StringBuilder();
        var used = TimeSpan.FromSeconds(result.TimeUsedSeconds);
        var limit = TimeSpan.FromSeconds(result.TimeLimitSeconds);

        builder.AppendLine($"# FRC Research & Debugging Assessment - {result.Student}");
        builder.AppendLine();
        builder.AppendLine($"- **Team:** {result.Team}");
        builder.AppendLine($"- **Started:** {result.StartedAt}");
        builder.AppendLine($"- **Submitted:** {result.SubmittedAt}" +
                           (result.AutoSubmitted ? " (timer expired)" : ""));
        builder.AppendLine($"- **Time used:** {used.Minutes:00}:{used.Seconds:00} of {limit.Minutes:00}:{limit.Seconds:00}");
        builder.AppendLine($"- **Activity:** {result.Searches} searches, {result.UniqueSites} unique sites, " +
                           $"{result.AiAttempts} blocked AI attempt(s)");
        builder.AppendLine($"- **Section A (auto):** {result.ResearchEffective} / {result.ResearchTotal} " +
                           $"credit (answer + verified citation) - keywords: {result.ResearchCorrect}, " +
                           $"citations verified: {result.CitationsVerified}");
        builder.AppendLine($"- **Flags:** {(result.Flags.Count == 0 ? "none" : string.Join(", ", result.Flags))}");
        builder.AppendLine($"- **Activity log:** `{Path.GetFileName(result.LogFile)}`");
        builder.AppendLine($"- **JSON:** `{Path.GetFileName(jsonPath)}`");
        builder.AppendLine();

        var research = result.Answers.Where(a => a.Type == "research").ToList();
        var code = result.Answers.Where(a => a.Type == "code").ToList();

        builder.AppendLine("## Section A - Research (auto-scored)");
        builder.AppendLine();

        foreach (var answer in research)
        {
            var verdict = answer.Credit ? "CREDIT" : "NO CREDIT";
            builder.AppendLine($"### {answer.QuestionId} - {verdict}");
            builder.AppendLine();
            builder.AppendLine($"**Question:** {answer.Prompt}");
            builder.AppendLine();
            builder.AppendLine($"**Answer:** {(answer.Answer.Length == 0 ? "_No answer_" : answer.Answer)}");
            builder.AppendLine();
            builder.AppendLine($"**Source:** {(answer.Citation.Length == 0 ? "_none_" : answer.Citation)}");
            builder.AppendLine();
            builder.AppendLine($"**Check:** keyword={(answer.KeywordMatch ? "match" : "no match")}, " +
                               $"citation={answer.CitationStatus}");
            builder.AppendLine();
        }

        builder.AppendLine("## Section B - Programming (leadership review)");
        builder.AppendLine();

        foreach (var answer in code)
        {
            builder.AppendLine($"### {answer.QuestionId} - pending review");
            builder.AppendLine();
            builder.AppendLine("```java");
            builder.AppendLine(answer.Snippet.TrimEnd());
            builder.AppendLine("```");
            builder.AppendLine();
            builder.AppendLine($"**Question:** {answer.Prompt}");
            builder.AppendLine();
            builder.AppendLine($"**Student answer:** {(answer.Answer.Length == 0 ? "_No answer_" : answer.Answer)}");
            builder.AppendLine();
            builder.AppendLine($"**Rubric:** {answer.Rubric}");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static void AppendSummaryCsv(
        string csvPath,
        SessionResult result,
        string jsonPath,
        string reviewPath)
    {
        var isNew = !File.Exists(csvPath) || new FileInfo(csvPath).Length == 0;
        var builder = new StringBuilder();

        if (isNew)
        {
            builder.AppendLine(
                "submitted_at,student,team,research_correct,research_effective,research_total," +
                "citations_verified,answered,unanswered,ai_attempts,searches,unique_sites," +
                "time_used_sec,auto_submitted,flags,result_file,review_file,log_file");
        }

        var answered = result.Answers.Count(answer => answer.Answer.Length > 0);

        builder.Append(string.Join(",", new[]
        {
            Csv(result.SubmittedAt),
            Csv(result.Student),
            Csv(result.Team),
            Csv(result.ResearchCorrect.ToString()),
            Csv(result.ResearchEffective.ToString()),
            Csv(result.ResearchTotal.ToString()),
            Csv(result.CitationsVerified.ToString()),
            Csv(answered.ToString()),
            Csv(result.Unanswered.ToString()),
            Csv(result.AiAttempts.ToString()),
            Csv(result.Searches.ToString()),
            Csv(result.UniqueSites.ToString()),
            Csv(result.TimeUsedSeconds.ToString()),
            Csv(result.AutoSubmitted ? "yes" : "no"),
            Csv(string.Join("; ", result.Flags)),
            Csv(Path.GetFileName(jsonPath)),
            Csv(Path.GetFileName(reviewPath)),
            Csv(Path.GetFileName(result.LogFile)),
        }));
        builder.AppendLine();

        File.AppendAllText(csvPath, builder.ToString(), new UTF8Encoding(true));
    }

    private static string Csv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
