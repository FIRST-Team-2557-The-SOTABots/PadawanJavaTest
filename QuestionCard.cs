using System.Text;

namespace FrcResearchTest;

public sealed class QuestionCard : Panel
{
    private readonly AppConfig _config;
    private readonly VisitLog _visits;
    private readonly TextBox _answer;
    private readonly TextBox? _citation;
    private readonly Label? _citeState;
    private readonly Label? _promptLabel;
    private readonly TableLayoutPanel? _layout;

    public Question Question { get; }

    public string AnswerText => _answer.Text.Trim();
    public string CitationText => _citation?.Text.Trim() ?? "";

    public event Action? AnswerChanged;

    public QuestionCard(Question question, VisitLog visits, AppConfig config)
    {
        Question = question;
        _visits = visits;
        _config = config;

        BackColor = Color.White;
        BorderStyle = BorderStyle.FixedSingle;
        Padding = new Padding(12, 10, 12, 10);
        Margin = new Padding(0, 0, 0, 10);

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Color.White,
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _layout = layout;

        var header = new Label
        {
            Text = $"{question.Id}    {question.Points} pt    {(question.Kind == QuestionType.Code ? "LEADERSHIP REVIEW" : "AUTO-SCORED")}",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 9f),
            ForeColor = question.Kind == QuestionType.Code
                ? Color.FromArgb(120, 70, 20)
                : Color.FromArgb(30, 80, 140),
            Margin = new Padding(0, 0, 0, 4),
        };
        AddRow(layout, header, SizeType.AutoSize, 0);

        var prompt = new Label
        {
            Text = question.Prompt,
            AutoSize = true,
            Font = new Font("Segoe UI", 10.5f),
            ForeColor = Color.FromArgb(24, 30, 40),
            Margin = new Padding(0, 0, 0, 6),
        };
        _promptLabel = prompt;
        AddRow(layout, prompt, SizeType.AutoSize, 0);

        if (question.Kind == QuestionType.Code)
        {
            var snippet = new RichTextBox
            {
                Text = NumberLines(question.Snippet),
                ReadOnly = true,
                Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                BackColor = Color.FromArgb(249, 250, 252),
                ForeColor = Color.FromArgb(22, 28, 38),
                Font = new Font("Consolas", 10f),
                WordWrap = false,
                ScrollBars = RichTextBoxScrollBars.Both,
                DetectUrls = false,
                TabStop = false,
                Margin = new Padding(0, 2, 0, 8),
            };
            AddRow(layout, snippet, SizeType.Absolute, Px(170));

            AddRow(layout, SectionLabel("Your answer - name the bug and write the fix"), SizeType.AutoSize, 0);

            _answer = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 0),
            };
            AddRow(layout, _answer, SizeType.Absolute, Px(130));
        }
        else
        {
            AddRow(layout, SectionLabel("Your answer"), SizeType.AutoSize, 0);

            _answer = new TextBox
            {
                Multiline = true,
                Dock = DockStyle.Fill,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Segoe UI", 10.5f),
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(0, 2, 0, 6),
            };
            AddRow(layout, _answer, SizeType.Absolute, Px(70));

            AddRow(layout, SectionLabel("Source URL - the page where you found the answer"), SizeType.AutoSize, 0);

            _citation = new TextBox
            {
                Dock = DockStyle.Fill,
                Font = new Font("Segoe UI", 10f),
                BorderStyle = BorderStyle.FixedSingle,
                PlaceholderText = "https://docs.wpilib.org/...",
                Margin = new Padding(0, 2, 0, 4),
            };
            AddRow(layout, _citation, SizeType.Absolute, Px(30));

            _citeState = new Label
            {
                Dock = DockStyle.Fill,
                Text = "",
                Font = new Font("Segoe UI", 9f),
                ForeColor = Color.DimGray,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(0),
            };
            AddRow(layout, _citeState, SizeType.Absolute, Px(22));

            _citation.TextChanged += (_, _) => UpdateCitationState();
        }

        _answer.TextChanged += (_, _) => AnswerChanged?.Invoke();

        Controls.Add(layout);
        UpdatePromptWidth();
        RefreshHeight();
    }

    private int Px(int designPixels) =>
        (int)Math.Round(designPixels * Math.Max(96, DeviceDpi) / 96.0);

    /// <summary>
    /// Sizes the card to the height its contents actually need. Fixed pixel
    /// heights would clip labels as soon as Windows display scaling is not 100%.
    /// </summary>
    public void RefreshHeight()
    {
        if (_layout is null) return;

        var width = Math.Max(320, ClientSize.Width - Padding.Horizontal);
        var preferred = _layout.GetPreferredSize(new Size(width, int.MaxValue));
        Height = preferred.Height + Padding.Vertical + 4;
    }

    private void AddRow(TableLayoutPanel layout, Control control, SizeType sizeType, float height)
    {
        var index = layout.RowStyles.Count;
        layout.RowStyles.Add(sizeType == SizeType.Absolute
            ? new RowStyle(SizeType.Absolute, height)
            : new RowStyle(SizeType.AutoSize));
        layout.RowCount = layout.RowStyles.Count;
        layout.Controls.Add(control, 0, index);
    }

    private static Label SectionLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9f),
        ForeColor = Color.FromArgb(95, 105, 120),
        Margin = new Padding(0, 2, 0, 2),
    };

    private void UpdateCitationState()
    {
        if (_citation is null || _citeState is null) return;

        var status = _visits.CheckCitation(_citation.Text, _config.AllowedCitationDomains);

        switch (status)
        {
            case CitationStatus.Empty:
                _citeState.Text = "";
                _citeState.ForeColor = Color.DimGray;
                break;
            case CitationStatus.Verified:
                _citeState.Text = "Verified - this page was opened in this session.";
                _citeState.ForeColor = Color.FromArgb(20, 110, 55);
                break;
            case CitationStatus.NotVisited:
                _citeState.Text = "This page has not been opened in this browser session.";
                _citeState.ForeColor = Color.FromArgb(170, 95, 15);
                break;
            case CitationStatus.NotAllowedDomain:
                _citeState.Text = "Not an approved documentation site.";
                _citeState.ForeColor = Color.FromArgb(160, 40, 40);
                break;
            default:
                _citeState.Text = "Enter a full URL starting with https://";
                _citeState.ForeColor = Color.FromArgb(160, 40, 40);
                break;
        }
    }

    public void SetReadOnly()
    {
        _answer.ReadOnly = true;
        if (_citation is not null) _citation.ReadOnly = true;
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdatePromptWidth();
    }

    private void UpdatePromptWidth()
    {
        if (_promptLabel is null) return;
        _promptLabel.MaximumSize = new Size(Math.Max(260, ClientSize.Width - 34), 0);
    }

    private static string NumberLines(string code)
    {
        var lines = (code ?? "").Replace("\r\n", "\n").Split('\n');
        var width = lines.Length.ToString().Length;
        var builder = new StringBuilder();

        for (var i = 0; i < lines.Length; i++)
        {
            builder.Append((i + 1).ToString().PadLeft(width));
            builder.Append("  ");
            builder.AppendLine(lines[i]);
        }

        return builder.ToString().TrimEnd();
    }
}
