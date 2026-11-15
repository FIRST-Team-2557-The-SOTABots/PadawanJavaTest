using System.Diagnostics;
using System.Runtime.InteropServices;

namespace FrcResearchTest;

public sealed class MainForm : Form
{
    private const int WmHotkey = 0x0312;
    private const int MentorHotkeyId = 0x4D31;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint VkF12 = 0x7B;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private AppConfig _config = null!;
    private QuestionBankFile _bank = new();
    private VisitLog _visits = new();
    private SessionLogger? _logger;
    private BrowserPanel? _browser;

    private DateTimeOffset _startedAt;
    private int _remainingSeconds;
    private bool _submitted;
    private bool _allowClose;
    private string _student = "";
    private string _team = "";
    private string _sessionStamp = "";

    private Panel _startPanel = null!;
    private Panel _mainPanel = null!;
    private Panel _donePanel = null!;
    private Panel _browserHost = null!;
    private SplitContainer _split = null!;
    private TabControl _tabs = null!;
    private FlowLayoutPanel _researchFlow = null!;
    private FlowLayoutPanel _codeFlow = null!;
    private TextBox _nameBox = null!;
    private TextBox _teamBox = null!;
    private Label _timerLabel = null!;
    private Label _progressLabel = null!;
    private Label _startDetail = null!;
    private Label _doneDetail = null!;
    private Button _startButton = null!;
    private Button _submitButton = null!;
    private readonly System.Windows.Forms.Timer _clock;
    private readonly List<QuestionCard> _cards = new();

    public MainForm()
    {
        _config = AppConfig.Load();

        _clock = new System.Windows.Forms.Timer { Interval = 1000 };
        _clock.Tick += (_, _) => OnClockTick();

        Text = "FRC Research & Debugging Assessment";
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(243, 245, 248);
        KeyPreview = true;

        if (_config.Kiosk)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Maximized;
            MaximizedBounds = Screen.PrimaryScreen?.Bounds ?? new Rectangle(0, 0, 1280, 800);
            TopMost = true;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
            TopMost = false;
        }

        _startPanel = BuildStartPanel();
        _mainPanel = BuildMainPanel();
        _donePanel = BuildDonePanel();

        Controls.Add(_mainPanel);
        Controls.Add(_donePanel);
        Controls.Add(_startPanel);

        _mainPanel.Visible = false;
        _donePanel.Visible = false;
        _startPanel.Visible = true;
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

#if DEBUG
        var demo = Environment.GetEnvironmentVariable("FRC_DEMO") == "1";
        if (demo && !_config.HasMentorPin)
        {
            try
            {
                _config.SetMentorPin("1234");
            }
            catch
            {
                // Demo mode is best-effort only.
            }
        }
#endif

        if (!_config.HasMentorPin)
        {
            using var dialog = new SetPinDialog(_config, firstRun: true);
            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                _allowClose = true;
                Close();
                return;
            }
        }

        SetInitialSplitter();
        BeginInvoke(new Action(() =>
        {
            SetInitialSplitter();
            ResizeCards();
        }));

#if DEBUG
        if (demo)
        {
            _nameBox.Text = "Demo Student";
            _teamBox.Text = "1234";
            BeginInvoke(new Action(StartSessionAsync));
        }
#endif
    }

    private void SetInitialSplitter()    {
        try
        {
            var width = _split.Width;
            if (width <= 400) return;

            _split.Panel1MinSize = 1;
            _split.Panel2MinSize = 1;
            _split.SplitterDistance = Math.Clamp((int)(width * 0.46), 20, width - 20);

            _split.Panel1MinSize = Math.Min(340, Math.Max(120, width / 4));
            _split.Panel2MinSize = Math.Min(420, Math.Max(160, width / 4));
        }
        catch (InvalidOperationException)
        {
            // Splitter not laid out yet; the default position is fine.
        }
    }

    private Panel BuildStartPanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(243, 245, 248) };

        var card = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(40, 32, 40, 32),
            Margin = new Padding(0),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        var title = new Label
        {
            Text = "FRC Research & Debugging Assessment",
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(780, 0),
            Font = new Font("Segoe UI Semibold", 24f),
            ForeColor = Color.FromArgb(24, 32, 44),
            Margin = new Padding(0, 0, 0, 6),
        };
        card.Controls.Add(title);

        var subtitle = new Label
        {
            Text = "Section A: research (auto-scored)  |  Section B: Java debugging (leadership review)",
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(780, 0),
            Font = new Font("Segoe UI", 11f),
            ForeColor = Color.FromArgb(80, 92, 110),
            Margin = new Padding(0, 0, 0, 18),
        };
        card.Controls.Add(subtitle);

        var rules = new Label
        {
            Text =
                "Rules\r\n" +
                "  -  Section A has 10 research questions. Every answer needs the URL of the page where you found it.\r\n" +
                "  -  Section B has 10 Java debugging questions. Leadership grades these afterwards.\r\n" +
                "  -  The browser shows Google's text-only Web results. AI tools and answer sites are blocked and logged.\r\n" +
                "  -  Your searches, visited pages, and blocked attempts are recorded for this session.\r\n" +
                "  -  The timer starts when you press Start. You may submit early.",
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(780, 0),
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(45, 55, 70),
            Margin = new Padding(0, 0, 0, 20),
        };
        card.Controls.Add(rules);

        _startDetail = new Label
        {
            Text = "",
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(780, 0),
            Font = new Font("Segoe UI", 9f),
            ForeColor = Color.FromArgb(120, 60, 40),
            Margin = new Padding(0, 0, 0, 12),
        };
        card.Controls.Add(_startDetail);

        card.Controls.Add(FieldLabel("Your name"));
        _nameBox = new TextBox
        {
            Width = 420,
            Font = new Font("Segoe UI", 12f),
            Margin = new Padding(0, 0, 0, 12),
        };
        card.Controls.Add(_nameBox);

        card.Controls.Add(FieldLabel("Team number"));
        _teamBox = new TextBox
        {
            Width = 160,
            Font = new Font("Segoe UI", 12f),
            Margin = new Padding(0, 0, 0, 20),
        };
        card.Controls.Add(_teamBox);

        var startButton = new Button
        {
            Text = "Start the test",
            Width = 200,
            Height = 44,
            BackColor = Color.FromArgb(30, 90, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 11f),
            Margin = new Padding(0, 0, 0, 14),
        };
        startButton.FlatAppearance.BorderSize = 0;
        startButton.Click += (_, _) => StartSessionAsync();
        _startButton = startButton;
        card.Controls.Add(startButton);

        var mentorLink = new LinkLabel
        {
            Text = "Mentor setup / exit",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(90, 100, 115),
            Margin = new Padding(0),
        };
        mentorLink.LinkClicked += (_, _) => ShowMentorMenu();
        card.Controls.Add(mentorLink);

        CenterCard(outer, card, 940);
        return outer;
    }

    /// <summary>
    /// Centers an auto-sized card inside a panel without a TableLayoutPanel host.
    /// A percent/auto-size host fights PerMonitorV2 DPI scaling and can push the
    /// card off-screen, so the card is sized and positioned manually and
    /// re-centered whenever either side changes size.
    /// </summary>
    private static void CenterCard(Panel outer, Control card, int maxWidth, int maxHeight = 0)
    {
        var layingOut = false;

        void Layout()
        {
            if (layingOut || outer.IsDisposed || card.IsDisposed) return;
            if (outer.ClientSize.Width <= 0 || outer.ClientSize.Height <= 0) return;

            layingOut = true;
            try
            {
                var width = Math.Min(maxWidth, Math.Max(280, outer.ClientSize.Width - 48));

                var preferred = card.GetPreferredSize(new Size(width, int.MaxValue));
                var height = Math.Max(preferred.Height, 120);

                var available = Math.Max(120, outer.ClientSize.Height - 24);
                var limit = maxHeight > 0 ? Math.Min(maxHeight, available) : available;
                height = Math.Min(height, limit);

                card.SetBounds(
                    Math.Max(0, (outer.ClientSize.Width - width) / 2),
                    Math.Max(0, (outer.ClientSize.Height - height) / 2),
                    width,
                    height);
            }
            finally
            {
                layingOut = false;
            }
        }

        outer.Resize += (_, _) => Layout();
        outer.HandleCreated += (_, _) => Layout();
        outer.Controls.Add(card);
        Layout();
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9.5f),
        ForeColor = Color.FromArgb(90, 100, 115),
        Margin = new Padding(0, 0, 0, 3),
    };

    private Panel BuildMainPanel()
    {
        var root = new Panel { Dock = DockStyle.Fill };

        var grid = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 62));

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 6,
            BackColor = Color.FromArgb(221, 226, 233),
        };

        var left = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(10, 10, 4, 4),
            BackColor = Color.FromArgb(243, 245, 248),
        };

        _tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 10f) };
        _researchFlow = MakeFlowPanel();
        _codeFlow = MakeFlowPanel();

        var tabResearch = new TabPage("Section A - Research")
        {
            BackColor = Color.White,
        };
        tabResearch.Controls.Add(_researchFlow);

        var tabCode = new TabPage("Section B - Programming")
        {
            BackColor = Color.White,
        };
        tabCode.Controls.Add(_codeFlow);

        _tabs.TabPages.Add(tabResearch);
        _tabs.TabPages.Add(tabCode);
        _tabs.SelectedIndexChanged += (_, _) => ResizeCards();

        left.Controls.Add(_tabs);
        _split.Panel1.Controls.Add(left);

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 10, 10, 4),
            BackColor = Color.FromArgb(243, 245, 248),
        };

        _browserHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
        };
        right.Controls.Add(_browserHost);
        _split.Panel2.Controls.Add(right);

        grid.Controls.Add(_split, 0, 0);
        grid.Controls.Add(BuildFooter(), 0, 1);
        root.Controls.Add(grid);
        return root;
    }

    private FlowLayoutPanel MakeFlowPanel()
    {
        var flow = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Padding = new Padding(8),
            BackColor = Color.White,
        };
        flow.Resize += (_, _) => ResizeCards();
        flow.ControlAdded += (_, _) => ResizeCards();
        return flow;
    }

    private Panel BuildFooter()
    {
        var footer = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1,
            BackColor = Color.White,
            Padding = new Padding(16, 9, 16, 9),
        };
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 24));
        footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

        _progressLabel = new Label
        {
            Text = "0 / 0 answered",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Font = new Font("Segoe UI", 10f),
            ForeColor = Color.FromArgb(70, 80, 95),
        };

        _timerLabel = new Label
        {
            Text = "Time 30:00",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleRight,
            Font = new Font("Segoe UI Semibold", 13f),
            ForeColor = Color.FromArgb(45, 55, 70),
        };

        _submitButton = new Button
        {
            Text = "Submit test",
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(30, 90, 160),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI Semibold", 10.5f),
            Enabled = false,
        };
        _submitButton.FlatAppearance.BorderSize = 0;
        _submitButton.Click += (_, _) => ConfirmSubmit();

        footer.Controls.Add(_progressLabel, 0, 0);
        footer.Controls.Add(_timerLabel, 1, 0);
        footer.Controls.Add(_submitButton, 3, 0);
        return footer;
    }

    private Panel BuildDonePanel()
    {
        var outer = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(243, 245, 248) };

        var card = new TableLayoutPanel
        {
            AutoSize = false,
            ColumnCount = 1,
            BackColor = Color.White,
            Padding = new Padding(40, 32, 40, 32),
            Margin = new Padding(0),
        };
        card.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        card.Controls.Add(new Label
        {
            Text = "Submitted",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 24f),
            ForeColor = Color.FromArgb(24, 32, 44),
            Margin = new Padding(0, 0, 0, 14),
        });

        _doneDetail = new Label
        {
            Text = "",
            AutoSize = true,
            UseMnemonic = false,
            MaximumSize = new Size(700, 0),
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(60, 70, 85),
            Margin = new Padding(0, 0, 0, 18),
        };
        card.Controls.Add(_doneDetail);

        var mentorLink = new LinkLabel
        {
            Text = "Mentor options",
            AutoSize = true,
            Font = new Font("Segoe UI", 9f),
            LinkColor = Color.FromArgb(90, 100, 115),
        };
        mentorLink.LinkClicked += (_, _) => ShowMentorMenu();
        card.Controls.Add(mentorLink);

        CenterCard(outer, card, 820, 700);
        return outer;
    }

    private async void StartSessionAsync()
    {
        _startButton.Enabled = false;

        var name = _nameBox.Text.Trim();
        var team = _teamBox.Text.Trim();

        if (name.Length < 2)
        {
            _startDetail.Text = "Enter your name before starting.";
            _startButton.Enabled = true;
            _nameBox.Focus();
            return;
        }

        if (team.Length == 0)
        {
            _startDetail.Text = "Enter your team number before starting.";
            _startButton.Enabled = true;
            _teamBox.Focus();
            return;
        }

        try
        {
            _config = AppConfig.Load();
            _bank = QuestionBank.Load();
        }
        catch (Exception ex)
        {
            _startDetail.Text = "Could not load config.json / questions.json: " + ex.Message;
            _startButton.Enabled = true;
            return;
        }

        if (!_config.HasMentorPin)
        {
            using var pinDialog = new SetPinDialog(_config, firstRun: true);
            if (pinDialog.ShowDialog(this) != DialogResult.OK)
            {
                _startDetail.Text = "A mentor PIN is required before the test can start.";
                _startButton.Enabled = true;
                return;
            }
        }

        _student = name;
        _team = team;
        _startedAt = DateTimeOffset.Now;
        _remainingSeconds = Math.Max(1, _config.TimeLimitMinutes) * 60;
        _sessionStamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");

        var safeName = NameUtil.Safe(name);
        var resultsFolder = _config.EffectiveResultsFolder;
        Directory.CreateDirectory(resultsFolder);

        _visits = new VisitLog();
        _logger = new SessionLogger(Path.Combine(
            resultsFolder,
            $"{safeName}-{_sessionStamp}.log.jsonl"));
        _logger.Log("session_start", $"{name} / team {team}", Application.StartupPath, "");

        BuildCards();
        UpdateProgress();
        UpdateTimerLabel();

        _startPanel.Visible = false;
        _mainPanel.Visible = true;
        _mainPanel.BringToFront();
        _submitButton.Enabled = false;

        _browser = new BrowserPanel(_config, _visits, _logger);
        _browserHost.Controls.Clear();
        _browserHost.Controls.Add(_browser);

        try
        {
            await _browser.InitializeAsync($"{safeName}-{_sessionStamp}");
        }
        catch (Exception ex)
        {
            _logger.Log("browser_init_failed", ex.Message);
            MessageBox.Show(
                this,
                "The embedded browser could not start.\r\n\r\n" +
                "This usually means the Microsoft Edge WebView2 Runtime is missing.\r\n" +
                "Install it from https://go.microsoft.com/fwlink/p/?LinkId=2124703, then run the test again.\r\n\r\n" +
                ex.Message,
                "Browser unavailable",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            _allowClose = true;
            Close();
            return;
        }

        _logger.Log("browser_ready");
        _submitButton.Enabled = true;
        _browser.FocusAddress();
        _clock.Start();

        BeginInvoke(new Action(() =>
        {
            SetInitialSplitter();
            ResizeCards();
        }));

#if DEBUG
        if (Environment.GetEnvironmentVariable("FRC_DEMO_TAB") is "B" or "b" && _tabs.TabPages.Count > 1)
        {
            _tabs.SelectedIndex = 1;
        }

        if (Environment.GetEnvironmentVariable("FRC_DEMO_SUBMIT") == "1")
        {
            var submitTimer = new System.Windows.Forms.Timer { Interval = 2500 };
            submitTimer.Tick += (_, _) =>
            {
                submitTimer.Stop();
                submitTimer.Dispose();
                Submit(auto: false);
            };
            submitTimer.Start();
        }
#endif
    }

    private void BuildCards()
    {
        _cards.Clear();
        _researchFlow.Controls.Clear();
        _codeFlow.Controls.Clear();

        var researchNumber = 0;
        var codeNumber = 0;

        foreach (var question in _bank.Questions)
        {
            if (question.Kind == QuestionType.Code)
            {
                codeNumber++;
            }
            else
            {
                researchNumber++;
            }

            var card = new QuestionCard(question, _visits, _config);
            card.AnswerChanged += UpdateProgress;
            _cards.Add(card);

            if (question.Kind == QuestionType.Code)
            {
                _codeFlow.Controls.Add(card);
            }
            else
            {
                _researchFlow.Controls.Add(card);
            }
        }

        if (_tabs.TabPages.Count >= 2)
        {
            _tabs.TabPages[0].Text = $"Section A - Research [{researchNumber}]";
            _tabs.TabPages[1].Text = $"Section B - Programming [{codeNumber}]";
        }

        ResizeCards();
    }

    private void ResizeCards()
    {
        foreach (var card in _cards)
        {
            if (card.Parent is not FlowLayoutPanel parent) continue;

            var width = parent.ClientSize.Width
                        - SystemInformation.VerticalScrollBarWidth
                        - parent.Padding.Horizontal
                        - 6;

            card.Width = Math.Max(320, width);
            card.RefreshHeight();
        }
    }

    private void UpdateProgress()
    {
        var answered = _cards.Count(card => card.AnswerText.Length > 0);
        _progressLabel.Text = $"{answered} / {_cards.Count} answered";
    }

    private void OnClockTick()
    {
        _remainingSeconds = Math.Max(0, _remainingSeconds - 1);
        UpdateTimerLabel();

        if (_remainingSeconds <= 0)
        {
            Submit(auto: true);
        }
    }

    private void UpdateTimerLabel()
    {
        var span = TimeSpan.FromSeconds(Math.Max(0, _remainingSeconds));
        _timerLabel.Text = $"Time {span.Minutes:00}:{span.Seconds:00}";
        _timerLabel.ForeColor = _remainingSeconds <= 300
            ? Color.Firebrick
            : Color.FromArgb(45, 55, 70);
    }

    private void ConfirmSubmit()
    {
        if (_submitted) return;

        var unanswered = _cards.Count(card => card.AnswerText.Length == 0);
        var message = unanswered > 0
            ? $"You still have {unanswered} unanswered question(s).\r\n\r\nSubmit the test now? You cannot change answers afterwards."
            : "Submit the test now? You cannot change answers afterwards.";

        var result = MessageBox.Show(
            this,
            message,
            "Submit test",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result == DialogResult.Yes) Submit(auto: false);
    }

    private void Submit(bool auto)
    {
        if (_submitted) return;
        _submitted = true;
        _clock.Stop();

        _browser?.SetInteractionEnabled(false);
        _logger?.Log("submit", auto ? "auto (timer expired)" : "manual");

        var result = BuildResult(auto);

        var jsonPath = "";
        var reviewPath = "";
        try
        {
            (jsonPath, reviewPath, _) = ResultsExporter.Export(_config, result, _logger?.Path ?? "");
            _logger?.Log("results_saved", jsonPath);
        }
        catch (Exception ex)
        {
            _logger?.Log("results_save_failed", ex.ToString());
            MessageBox.Show(
                this,
                "Your answers could not be saved automatically:\r\n\r\n" + ex.Message +
                "\r\n\r\nPlease call a mentor before closing this window.",
                "Save failed",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        var used = TimeSpan.FromSeconds(result.TimeUsedSeconds);
        var lines = new List<string>
        {
            $"Student: {_student}   |   Team: {_team}",
            $"Time used: {used.Minutes:00}:{used.Seconds:00}   |   Searches: {result.Searches}",
        };

        if (_config.ShowScoreOnSubmit)
        {
            lines.Add($"Section A: {result.ResearchEffective} / {result.ResearchTotal} " +
                      $"({result.CitationsVerified} citation(s) verified)");
        }

        lines.Add("Section B answers were saved for leadership review.");

        if (jsonPath.Length > 0)
        {
            lines.Add("Saved as: " + Path.GetFileName(jsonPath));
        }
        else if (reviewPath.Length > 0)
        {
            lines.Add("Saved as: " + Path.GetFileName(reviewPath));
        }

        lines.Add("");
        lines.Add("Leave this window open and tell a mentor you are finished.");

        _doneDetail.Text = string.Join("\r\n", lines);
        _mainPanel.Visible = false;
        _donePanel.Visible = true;
        _donePanel.BringToFront();
    }

    private SessionResult BuildResult(bool auto)
    {
        var now = DateTimeOffset.Now;
        var result = new SessionResult
        {
            Student = _student,
            Team = _team,
            StartedAt = _startedAt.ToString("O"),
            SubmittedAt = now.ToString("O"),
            TimeLimitSeconds = _config.TimeLimitMinutes * 60,
            TimeUsedSeconds = Math.Max(0, (int)(now - _startedAt).TotalSeconds),
            AutoSubmitted = auto,
            AiAttempts = _browser?.AiAttempts ?? 0,
            Searches = _browser?.SearchCount ?? 0,
            UniqueSites = _visits.UniqueHosts,
            LogFile = _logger?.Path ?? "",
            ResultsFolder = _config.EffectiveResultsFolder,
        };

        foreach (var card in _cards)
        {
            var question = card.Question;
            var record = new AnswerRecord
            {
                QuestionId = question.Id,
                Type = question.Kind == QuestionType.Code ? "code" : "research",
                Points = question.Points,
                Prompt = question.Prompt,
                Snippet = question.Snippet,
                Rubric = question.Rubric,
                Answer = card.AnswerText,
                Citation = card.CitationText,
            };

            if (question.Kind == QuestionType.Research)
            {
                record.KeywordMatch = Scoring.MatchesKeyword(question.Accept, record.Answer);
                var status = _visits.CheckCitation(record.Citation, _config.AllowedCitationDomains);
                record.CitationStatus = status.ToString();
                record.CitationVerified = status == CitationStatus.Verified;
                record.Credit = record.KeywordMatch && record.CitationVerified;

                result.ResearchTotal++;
                if (record.KeywordMatch) result.ResearchCorrect++;
                if (record.CitationVerified) result.CitationsVerified++;
                if (record.Credit) result.ResearchEffective++;
            }

            if (record.Answer.Length == 0) result.Unanswered++;
            result.Answers.Add(record);
        }

        result.Flags = ComputeFlags(result);
        return result;
    }

    private List<string> ComputeFlags(SessionResult result)
    {
        var flags = new List<string>();

        if (result.AutoSubmitted) flags.Add("auto_submitted");
        if (result.Searches == 0) flags.Add("no_searches");
        if (result.AiAttempts > 0) flags.Add($"ai_attempts:{result.AiAttempts}");
        if (result.Unanswered > 0) flags.Add($"unanswered:{result.Unanswered}");
        if (result.ResearchEffective < _config.ResearchPassCorrect) flags.Add("research_below_pass");
        if (result.TimeUsedSeconds < 240) flags.Add("very_fast");

        var verifiedHosts = result.Answers
            .Where(answer => answer.Type == "research" && answer.CitationVerified)
            .Select(answer => Uri.TryCreate(answer.Citation, UriKind.Absolute, out var uri)
                ? uri.Host.ToLowerInvariant()
                : "")
            .Where(host => host.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (result.ResearchCorrect >= 3 && verifiedHosts.Count == 1) flags.Add("single_source");

        return flags;
    }

    private void ShowMentorMenu()
    {
        if (_config.HasMentorPin)
        {
            using var pin = new PinPromptDialog(_config, "Mentor access");
            if (pin.ShowDialog(this) != DialogResult.OK) return;
        }

        using var menu = new MentorMenuDialog(_config);
        menu.ShowDialog(this);

        if (menu.ExitRequested)
        {
            _logger?.Log("mentor_exit");
            _allowClose = true;
            Close();
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        RegisterHotKey(Handle, MentorHotkeyId, ModControl | ModShift, VkF12);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        UnregisterHotKey(Handle, MentorHotkeyId);
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmHotkey && m.WParam.ToInt32() == MentorHotkeyId)
        {
            ShowMentorMenu();
            return;
        }

        base.WndProc(ref m);
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (_allowClose || e.CloseReason == CloseReason.WindowsShutDown)
        {
            _clock.Stop();
            _logger?.Dispose();
            base.OnFormClosing(e);
            return;
        }

        e.Cancel = true;

        if (!_config.HasMentorPin)
        {
            _allowClose = true;
            Close();
            return;
        }

        using var pin = new PinPromptDialog(_config, "Mentor PIN required to exit");
        if (pin.ShowDialog(this) != DialogResult.OK) return;

        var result = MessageBox.Show(
            this,
            "End the session and close the application?",
            "Mentor",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button2);

        if (result != DialogResult.Yes) return;

        _logger?.Log("mentor_exit");
        _allowClose = true;
        Close();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _clock.Dispose();
            _logger?.Dispose();
        }

        base.Dispose(disposing);
    }

    private static void OpenWithShell(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            else
            {
                Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
            }
        }
        catch
        {
            try
            {
                Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch
            {
                MessageBox.Show("Could not open:\r\n" + path, "Open file", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }

    internal static void OpenPath(string path) => OpenWithShell(path);
}
