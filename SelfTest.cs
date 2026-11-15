using System.Text;
using System.Text.Json;

namespace FrcResearchTest;

/// <summary>
/// Headless checks for the parts that are easy to get subtly wrong:
/// udm=14 rewriting, keyword grading, citation verification, export, and
/// WebView2 startup. Run with: FrcResearchTest.exe --selftest
/// </summary>
public static class SelfTest
{
    private static readonly StringBuilder Report = new();
    private static int _failures;

    public static string ReportPath =>
        Path.Combine(Path.GetTempPath(), "frc-research-selftest.txt");

    public static int Run(bool includeNetworkProbe = false)
    {
        _failures = 0;
        Report.Clear();
        Report.AppendLine("FRC Research Test - self test");
        Report.AppendLine("============================");
        Report.AppendLine();

        AppConfig config;
        QuestionBankFile bank;

        try
        {
            config = AppConfig.Load();
            bank = QuestionBank.Load();
            Pass($"config + questions loaded ({bank.Questions.Count} questions, " +
                 $"{bank.Questions.Count(q => q.Kind == QuestionType.Research)} research / " +
                 $"{bank.Questions.Count(q => q.Kind == QuestionType.Code)} code)");
        }
        catch (Exception ex)
        {
            Fail("config/questions failed to load: " + ex.Message);
            return Finish();
        }

        TestRewriter();
        TestScoring(bank);
        TestCitations(config);
        TestBlockedSites(config);
        TestExport(config);
        TestMainFormConstruction();
        TestWebView(config);
        if (includeNetworkProbe) TestGoogleRewriteLive(config);

        return Finish();
    }

    private static void TestRewriter()
    {
        Section("Google Web-only rewrite");

        const string plain = "https://www.google.com/search?q=wpilib+command+scheduler";
        Check("plain Google search gets udm=14",
            SearchRewriter.TryRewrite(plain, out var rewritten, out _) &&
            rewritten.Contains("udm=14", StringComparison.Ordinal),
            rewritten);

        Check("already-Web search is left alone",
            !SearchRewriter.TryRewrite(plain + "&udm=14", out _, out _));

        Check("udm=web is accepted",
            !SearchRewriter.TryRewrite(plain + "&udm=web", out _, out _));

        Check("AI Mode (udm=50) is redirected to Web",
            SearchRewriter.TryRewrite(plain + "&udm=50", out var aiMode, out _) &&
            aiMode.Contains("udm=14", StringComparison.Ordinal) &&
            !aiMode.Contains("udm=50", StringComparison.Ordinal),
            aiMode);

        Check("image search is not rewritten",
            !SearchRewriter.TryRewrite(plain + "&tbm=isch", out _, out _));

        Check("non-Google site is not rewritten",
            !SearchRewriter.TryRewrite("https://docs.wpilib.org/en/stable/index.html", out _, out _));

        Check("Google subdomain is rewritten",
            SearchRewriter.TryRewrite("https://www.google.co.uk/search?q=pid", out var uk, out _) &&
            uk.Contains("udm=14", StringComparison.Ordinal));

        var built = SearchRewriter.BuildSearchUrl(
            "talon fx config",
            "https://www.google.com/search?q={query}&udm=14");
        Check("address-bar query builds an encoded Web search",
            built.Contains("talon%20fx%20config", StringComparison.Ordinal) &&
            built.Contains("udm=14", StringComparison.Ordinal),
            built);
    }

    private static void TestScoring(QuestionBankFile bank)
    {
        Section("Keyword grading");

        var r3 = bank.Questions.First(q => q.Id == "R3");
        var r8 = bank.Questions.First(q => q.Id == "R8");

        Check("R3 accepts atSetpoint()",
            Scoring.MatchesKeyword(r3.Accept, "PIDController.atSetpoint()"));
        Check("R3 accepts 'at setpoint' with a space",
            Scoring.MatchesKeyword(r3.Accept, "at setpoint"));
        Check("R3 rejects a vague answer",
            !Scoring.MatchesKeyword(r3.Accept, "the setpoint was reached"));
        Check("R8 requires both class and method",
            Scoring.MatchesKeyword(r8.Accept, "TalonFXConfiguration and getConfigurator().apply()"));
        Check("R8 rejects a half answer",
            !Scoring.MatchesKeyword(r8.Accept, "TalonFXConfiguration"));
        Check("empty answer never matches",
            !Scoring.MatchesKeyword(r3.Accept, "   "));
    }

    private static void TestCitations(AppConfig config)
    {
        Section("Citation verification");

        var visits = new VisitLog();
        visits.Add("https://docs.wpilib.org/en/stable/docs/software/commandbased/command-scheduler.html",
            "The Command Scheduler");

        Check("visited docs page with an anchor verifies",
            visits.CheckCitation(
                "https://docs.wpilib.org/en/stable/docs/software/commandbased/command-scheduler.html#the-command-scheduler",
                config.AllowedCitationDomains) == CitationStatus.Verified);

        Check("unvisited docs page does not verify",
            visits.CheckCitation("https://docs.wpilib.org/en/stable/docs/other.html",
                config.AllowedCitationDomains) == CitationStatus.NotVisited);

        Check("non-docs domain is rejected",
            visits.CheckCitation("https://example.com/page", config.AllowedCitationDomains) ==
            CitationStatus.NotAllowedDomain);

        Check("garbage citation is rejected",
            visits.CheckCitation("not a url", config.AllowedCitationDomains) ==
            CitationStatus.InvalidUrl);

        Check("empty citation is empty",
            visits.CheckCitation("", config.AllowedCitationDomains) == CitationStatus.Empty);
    }

    private static void TestBlockedSites(AppConfig config)
    {
        Section("AI and answer-site blocking");

        Check("chatgpt.com is blocked",
            BrowserPanel.IsBlockedUrl("https://chatgpt.com/c/123", config.BlockedDomains, out _));
        Check("a subdomain of a blocked site is blocked",
            BrowserPanel.IsBlockedUrl("https://chat.openai.com/", config.BlockedDomains, out _));
        Check("gemini.google.com is blocked",
            BrowserPanel.IsBlockedUrl("https://gemini.google.com/app", config.BlockedDomains, out _));
        Check("bing.com/chat is blocked",
            BrowserPanel.IsBlockedUrl("https://www.bing.com/chat?q=x", config.BlockedDomains, out _));
        Check("a normal Bing search is allowed",
            !BrowserPanel.IsBlockedUrl("https://www.bing.com/search?q=wpilib", config.BlockedDomains, out _));
        Check("file:// URLs are blocked",
            BrowserPanel.IsBlockedUrl("file:///C:/Windows/System32/", config.BlockedDomains, out _));
        Check("the WPILib docs are allowed",
            !BrowserPanel.IsBlockedUrl("https://docs.wpilib.org/en/stable/index.html",
                config.BlockedDomains, out _));
    }

    private static void TestExport(AppConfig baseConfig)    {
        Section("Results export");

        var folder = Path.Combine(Path.GetTempPath(), "frc-selftest-" + Guid.NewGuid().ToString("N"));
        var config = AppConfig.CreateDefault();
        config.ResultsFolder = folder;

        try
        {
            var result = new SessionResult
            {
                Student = "Test, Student",
                Team = "1234",
                StartedAt = DateTimeOffset.Now.ToString("O"),
                SubmittedAt = DateTimeOffset.Now.ToString("O"),
                TimeLimitSeconds = 1800,
                TimeUsedSeconds = 900,
                Searches = 4,
                UniqueSites = 3,
                ResearchTotal = 1,
                ResearchCorrect = 1,
                ResearchEffective = 1,
                CitationsVerified = 1,
                Flags = new List<string> { "test_flag" },
                Answers = new List<AnswerRecord>
                {
                    new()
                    {
                        QuestionId = "R3",
                        Type = "research",
                        Prompt = "Which PIDController method...",
                        Answer = "atSetpoint()",
                        Citation = "https://docs.wpilib.org/x.html",
                        KeywordMatch = true,
                        CitationStatus = "Verified",
                        CitationVerified = true,
                        Credit = true,
                    },
                    new()
                    {
                        QuestionId = "P1",
                        Type = "code",
                        Prompt = "What is wrong?",
                        Snippet = "if (x = 5) { }",
                        Answer = "assignment instead of comparison",
                        Rubric = "== not =",
                    },
                },
            };

            var (jsonPath, reviewPath, csvPath) = ResultsExporter.Export(config, result, "log.jsonl");

            Check("session JSON written", File.Exists(jsonPath));
            Check("mentor review file written", File.Exists(reviewPath));
            Check("summary CSV written", File.Exists(csvPath));

            if (File.Exists(jsonPath))
            {
                var roundTrip = JsonSerializer.Deserialize<SessionResult>(
                    File.ReadAllText(jsonPath), AppConfig.JsonOptions);
                Check("JSON round-trips the student name",
                    roundTrip?.Student == "Test, Student", roundTrip?.Student ?? "(null)");
                Check("JSON keeps programming answers",
                    roundTrip?.Answers.Any(a => a.Type == "code" && a.Answer.Length > 0) == true);
            }

            if (File.Exists(csvPath))
            {
                var lines = File.ReadAllLines(csvPath).Where(l => l.Length > 0).ToList();
                Check("CSV has a header and one row", lines.Count == 2, $"{lines.Count} line(s)");
                Check("CSV quotes a name containing a comma",
                    lines.Count >= 2 && lines[1].Contains("\"Test, Student\"", StringComparison.Ordinal));
            }
        }
        catch (Exception ex)
        {
            Fail("export threw: " + ex.Message);
        }
        finally
        {
            TryDelete(folder);
        }
    }

    private static void TestMainFormConstruction()
    {
        Section("Window construction");

        try
        {
            using var form = new MainForm();
            Pass("main window builds without throwing");
        }
        catch (Exception ex)
        {
            Fail("main window failed to build: " + ex.Message);
        }
    }

    private static void TestWebView(AppConfig config)
    {
        Section("WebView2 runtime");

        var success = false;
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    Width = 640,
                    Height = 480,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-4000, -4000),
                    Opacity = 0,
                };

                var logger = new SessionLogger(Path.Combine(Path.GetTempPath(),
                    "frc-selftest-webview-" + Guid.NewGuid().ToString("N") + ".jsonl"));
                var panel = new BrowserPanel(config, new VisitLog(), logger);
                form.Controls.Add(panel);

                form.Shown += async (_, _) =>
                {
                    try
                    {
                        await panel.InitializeAsync(
                            "selftest-" + Guid.NewGuid().ToString("N"),
                            navigateHome: false);
                        success = true;
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        form.Close();
                    }
                };

                Application.Run(form);
                logger.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(30)))
        {
            Fail("WebView2 initialization timed out (runtime missing or blocked)");
            return;
        }

        if (success) Pass("WebView2 initialized and locked down");
        else Fail("WebView2 failed: " + (failure?.Message ?? "unknown error"));
    }

    private static void TestGoogleRewriteLive(AppConfig config)
    {
        Section("Live Google rewrite (network)");

        var success = false;
        var observed = "";
        Exception? failure = null;

        var thread = new Thread(() =>
        {
            try
            {
                using var form = new Form
                {
                    Width = 900,
                    Height = 700,
                    ShowInTaskbar = false,
                    StartPosition = FormStartPosition.Manual,
                    Location = new Point(-4000, -4000),
                    Opacity = 0,
                };

                var logger = new SessionLogger(Path.Combine(Path.GetTempPath(),
                    "frc-selftest-live-" + Guid.NewGuid().ToString("N") + ".jsonl"));
                var panel = new BrowserPanel(config, new VisitLog(), logger);
                form.Controls.Add(panel);

                form.Shown += async (_, _) =>
                {
                    try
                    {
                        await panel.InitializeAsync(
                            "selftest-live-" + Guid.NewGuid().ToString("N"),
                            navigateHome: false);

                        panel.NavigateTo("https://www.google.com/search?q=wpilib+command+scheduler");

                        var deadline = DateTime.UtcNow.AddSeconds(20);
                        while (DateTime.UtcNow < deadline)
                        {
                            await Task.Delay(250);
                            observed = panel.CurrentUrl;
                            if (observed.Contains("udm=14", StringComparison.Ordinal) ||
                                observed.Contains("udm=web", StringComparison.Ordinal))
                            {
                                success = true;
                                break;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        failure = ex;
                    }
                    finally
                    {
                        form.Close();
                    }
                };

                Application.Run(form);
                logger.Dispose();
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();

        if (!thread.Join(TimeSpan.FromSeconds(40)))
        {
            Fail("live Google probe timed out");
            return;
        }

        if (success)
        {
            Pass("Google search was redirected to the Web-only view");
        }
        else
        {
            Fail($"live probe did not observe udm=14 (last url: {observed})" +
                 (failure is null ? "" : " error: " + failure.Message));
        }
    }

    private static void Section(string title)
    {
        Report.AppendLine();
        Report.AppendLine("[" + title + "]");
    }

    private static void Check(string name, bool condition, string? detail = null)
    {
        if (condition)
        {
            Pass(name);
        }
        else
        {
            Fail(name + (string.IsNullOrEmpty(detail) ? "" : $"  (got: {detail})"));
        }
    }

    private static void Pass(string message) => Report.AppendLine("PASS  " + message);

    private static void Fail(string message)
    {
        _failures++;
        Report.AppendLine("FAIL  " + message);
    }

    private static int Finish()
    {
        Report.AppendLine();
        Report.AppendLine(_failures == 0
            ? "RESULT: all checks passed"
            : $"RESULT: {_failures} check(s) failed");

        var text = Report.ToString();
        File.WriteAllText(ReportPath, text);
        return _failures == 0 ? 0 : 1;
    }

    private static void TryDelete(string folder)
    {
        try
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, recursive: true);
        }
        catch
        {
            // The CSV/JSON may still be open in another process; leave it.
        }
    }
}
