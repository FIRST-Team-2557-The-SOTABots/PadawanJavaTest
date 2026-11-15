namespace FrcResearchTest;

public sealed class PinPromptDialog : Form
{
    private readonly AppConfig _config;
    private readonly TextBox _pinBox;
    private readonly Label _error;

    public PinPromptDialog(AppConfig config, string title)
    {
        _config = config;

        Text = title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleMode = AutoScaleMode.Dpi;
        TopMost = true;
        Padding = new Padding(20);
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
        };

        layout.Controls.Add(new Label
        {
            Text = "Enter the mentor PIN.",
            AutoSize = true,
            Font = new Font("Segoe UI", 10f),
            Margin = new Padding(0, 0, 0, 8),
        });

        _pinBox = new TextBox
        {
            Width = 260,
            Font = new Font("Segoe UI", 13f),
            PasswordChar = '\u2022',
            MaxLength = 64,
            Margin = new Padding(0, 0, 0, 6),
        };
        _pinBox.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                TryAccept();
            }
        };
        layout.Controls.Add(_pinBox);

        _error = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Color.Firebrick,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 0, 10),
        };
        layout.Controls.Add(_error);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 4, 0, 0),
        };

        var okButton = new Button
        {
            Text = "Unlock",
            DialogResult = DialogResult.None,
            Width = 96,
            Height = 32,
            Margin = new Padding(6, 0, 0, 0),
        };
        okButton.Click += (_, _) => TryAccept();

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 96,
            Height = 32,
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void TryAccept()
    {
        if (_config.VerifyMentorPin(_pinBox.Text))
        {
            DialogResult = DialogResult.OK;
            Close();
            return;
        }

        _error.Text = "Incorrect PIN.";
        _pinBox.SelectAll();
        _pinBox.Focus();
    }
}

public sealed class SetPinDialog : Form
{
    private readonly AppConfig _config;
    private readonly TextBox _first;
    private readonly TextBox _second;
    private readonly Label _error;

    public SetPinDialog(AppConfig config, bool firstRun)
    {
        _config = config;

        Text = firstRun ? "Set the mentor PIN" : "Change the mentor PIN";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleMode = AutoScaleMode.Dpi;
        TopMost = true;
        Padding = new Padding(20);
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
        };

        layout.Controls.Add(new Label
        {
            Text = "This PIN protects mentor actions: exiting the app, opening the config, " +
                   "and seeing the results folder. Pick something the students do not know.",
            AutoSize = true,
            MaximumSize = new Size(380, 0),
            Font = new Font("Segoe UI", 9.5f),
            ForeColor = Color.FromArgb(70, 80, 95),
            Margin = new Padding(0, 0, 0, 12),
        });

        layout.Controls.Add(FieldLabel("PIN (at least 4 characters)"));
        _first = new TextBox
        {
            Width = 260,
            Font = new Font("Segoe UI", 12f),
            PasswordChar = '\u2022',
            MaxLength = 64,
            Margin = new Padding(0, 0, 0, 8),
        };
        layout.Controls.Add(_first);

        layout.Controls.Add(FieldLabel("Confirm PIN"));
        _second = new TextBox
        {
            Width = 260,
            Font = new Font("Segoe UI", 12f),
            PasswordChar = '\u2022',
            MaxLength = 64,
            Margin = new Padding(0, 0, 0, 6),
        };
        layout.Controls.Add(_second);

        _error = new Label
        {
            Text = "",
            AutoSize = true,
            ForeColor = Color.Firebrick,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 0, 10),
        };
        layout.Controls.Add(_error);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            Dock = DockStyle.Fill,
        };

        var okButton = new Button
        {
            Text = "Save PIN",
            Width = 100,
            Height = 32,
            Margin = new Padding(6, 0, 0, 0),
        };
        okButton.Click += (_, _) => TrySave();

        var cancelButton = new Button
        {
            Text = "Cancel",
            DialogResult = DialogResult.Cancel,
            Width = 96,
            Height = 32,
        };

        buttons.Controls.Add(okButton);
        buttons.Controls.Add(cancelButton);
        layout.Controls.Add(buttons);

        Controls.Add(layout);
        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void TrySave()
    {
        var first = _first.Text;
        var second = _second.Text;

        if (first.Length < 4)
        {
            _error.Text = "Use at least 4 characters.";
            _first.Focus();
            return;
        }

        if (!string.Equals(first, second, StringComparison.Ordinal))
        {
            _error.Text = "The two PINs do not match.";
            _second.SelectAll();
            _second.Focus();
            return;
        }

        try
        {
            _config.SetMentorPin(first);
        }
        catch (Exception ex)
        {
            _error.Text = "Could not save config.json: " + ex.Message;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private static Label FieldLabel(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Font = new Font("Segoe UI", 9.5f),
        ForeColor = Color.FromArgb(70, 80, 95),
        Margin = new Padding(0, 0, 0, 3),
    };
}

public sealed class MentorMenuDialog : Form
{
    public bool ExitRequested { get; private set; }

    public MentorMenuDialog(AppConfig config)
    {
        Text = "Mentor options";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        MaximizeBox = false;
        ShowInTaskbar = false;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        AutoScaleMode = AutoScaleMode.Dpi;
        TopMost = true;
        Padding = new Padding(20);
        BackColor = Color.White;

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            BackColor = Color.White,
        };

        layout.Controls.Add(new Label
        {
            Text = "Mentor tools",
            AutoSize = true,
            Font = new Font("Segoe UI Semibold", 13f),
            Margin = new Padding(0, 0, 0, 10),
        });

        layout.Controls.Add(MenuButton("Open config.json", () => MainForm.OpenPath(AppPaths.ConfigPath)));
        layout.Controls.Add(MenuButton("Open questions.json", () => MainForm.OpenPath(AppPaths.QuestionsPath)));
        layout.Controls.Add(MenuButton("Open results folder", () =>
        {
            Directory.CreateDirectory(config.EffectiveResultsFolder);
            MainForm.OpenPath(config.EffectiveResultsFolder);
        }));
        layout.Controls.Add(MenuButton("Change mentor PIN", () =>
        {
            using var dialog = new SetPinDialog(config, firstRun: false);
            dialog.ShowDialog(this);
        }));

        var exitButton = MenuButton("Exit the test app", () =>
        {
            var confirm = MessageBox.Show(
                this,
                "Close the test application? Unsaved answers will be lost.",
                "Exit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);

            if (confirm != DialogResult.Yes) return;

            ExitRequested = true;
            Close();
        });
        exitButton.ForeColor = Color.Firebrick;

        layout.Controls.Add(exitButton);

        var closeButton = new Button
        {
            Text = "Close",
            Width = 100,
            Height = 32,
            Margin = new Padding(0, 14, 0, 0),
            DialogResult = DialogResult.Cancel,
        };
        layout.Controls.Add(closeButton);

        Controls.Add(layout);
        CancelButton = closeButton;
    }

    private static Button MenuButton(string text, Action onClick)
    {
        var button = new Button
        {
            Text = text,
            Width = 300,
            Height = 36,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 0, 0),
            Margin = new Padding(0, 0, 0, 6),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(248, 249, 251),
        };
        button.FlatAppearance.BorderColor = Color.FromArgb(214, 220, 228);
        button.Click += (_, _) => onClick();
        return button;
    }
}
