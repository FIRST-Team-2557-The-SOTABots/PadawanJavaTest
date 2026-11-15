using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace FrcResearchTest;

/// <summary>
/// Embedded browser with three jobs: force Google's Web-only results (udm=14),
/// keep students away from AI tools, and record every navigation.
/// </summary>
public sealed class BrowserPanel : UserControl
{
    private const string DefaultStatus =
        "Web results only - Google AI Overviews are suppressed for this test.";

    private readonly AppConfig _config;
    private readonly VisitLog _visits;
    private readonly SessionLogger _logger;

    private readonly WebView2 _webView;
    private readonly TextBox _address;
    private readonly Button _backButton;
    private readonly Button _forwardButton;
    private readonly Button _reloadButton;
    private readonly Button _homeButton;
    private readonly Label _status;
    private readonly System.Windows.Forms.Timer _statusTimer;

    private readonly Dictionary<string, int> _rewriteCounts = new();
    private readonly Dictionary<string, DateTimeOffset> _rewriteFirstSeen = new();
    private DateTimeOffset _rewriteDisabledUntil = DateTimeOffset.MinValue;

    private bool _ready;

    public int AiAttempts { get; private set; }
    public int SearchCount { get; private set; }

    public event Action<string>? SearchPerformed;
    public event Action<string>? BlockedAttempt;

    public bool IsReady => _ready;

    public string CurrentUrl => SafeSource();

    public int RewriteCount { get; private set; }

    public BrowserPanel(AppConfig config, VisitLog visits, SessionLogger logger)
    {
        _config = config;
        _visits = visits;
        _logger = logger;

        Dock = DockStyle.Fill;
        BackColor = Color.White;

        _webView = new WebView2
        {
            Dock = DockStyle.Fill,
            AllowExternalDrop = false,
            DefaultBackgroundColor = Color.White,
        };

        _backButton = MakeToolbarButton("Back", (_, _) => GoBack());
        _forwardButton = MakeToolbarButton("Next", (_, _) => GoForward());
        _reloadButton = MakeToolbarButton("Reload", (_, _) => Reload());
        _homeButton = MakeToolbarButton("Home", (_, _) => NavigateHome());

        _address = new TextBox
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f),
            PlaceholderText = "Search the web or enter a URL",
            Margin = new Padding(4, 9, 6, 8),
        };
        _address.KeyDown += OnAddressKeyDown;

        var buttons = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
            Dock = DockStyle.Fill,
            Margin = new Padding(0),
            Padding = new Padding(0),
            BackColor = Color.Transparent,
        };
        buttons.Controls.Add(_backButton);
        buttons.Controls.Add(_forwardButton);
        buttons.Controls.Add(_reloadButton);
        buttons.Controls.Add(_homeButton);

        var toolbar = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Color.FromArgb(248, 249, 251),
            Padding = new Padding(6, 4, 6, 4),
        };
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        toolbar.Controls.Add(buttons, 0, 0);
        toolbar.Controls.Add(_address, 1, 0);

        _status = new Label
        {
            Dock = DockStyle.Fill,
            Text = DefaultStatus,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(10, 0, 10, 0),
            BackColor = Color.FromArgb(236, 242, 250),
            ForeColor = Color.FromArgb(45, 62, 90),
            Font = new Font("Segoe UI", 9f),
        };

        _statusTimer = new System.Windows.Forms.Timer { Interval = 6000 };
        _statusTimer.Tick += (_, _) =>
        {
            _statusTimer.Stop();
            ResetStatus();
        };

        var layout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
        };
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 44));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        layout.Controls.Add(toolbar, 0, 0);
        layout.Controls.Add(_webView, 0, 1);
        layout.Controls.Add(_status, 0, 2);

        Controls.Add(layout);
    }

    private static Button MakeToolbarButton(string text, EventHandler onClick)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.White,
            ForeColor = Color.FromArgb(30, 40, 55),
            Font = new Font("Segoe UI", 9f),
            Padding = new Padding(10, 4, 10, 4),
            Margin = new Padding(2, 2, 2, 2),
            TabStop = false,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(214, 220, 228);
        button.Click += onClick;
        return button;
    }

    public async Task InitializeAsync(string profileName, bool navigateHome = true)
    {
        Directory.CreateDirectory(AppPaths.ProfilesDirectory);
        var profileDirectory = Path.Combine(AppPaths.ProfilesDirectory, profileName);
        Directory.CreateDirectory(profileDirectory);

        var options = new CoreWebView2EnvironmentOptions(
            "--disable-extensions --no-first-run --disable-sync --disable-background-networking");

        var environment = await CoreWebView2Environment.CreateAsync(null, profileDirectory, options);
        await _webView.EnsureCoreWebView2Async(environment);

        var core = _webView.CoreWebView2
                   ?? throw new InvalidOperationException("WebView2 did not initialize.");

        var settings = core.Settings;
        settings.AreDevToolsEnabled = false;
        settings.AreDefaultContextMenusEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        settings.IsStatusBarEnabled = false;
        settings.IsSwipeNavigationEnabled = false;
        settings.IsPasswordAutosaveEnabled = false;
        settings.IsGeneralAutofillEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsWebMessageEnabled = false;
        settings.IsZoomControlEnabled = true;
        settings.IsBuiltInErrorPageEnabled = true;

        try
        {
            core.Profile.PreferredColorScheme = CoreWebView2PreferredColorScheme.Light;
        }
        catch (InvalidOperationException)
        {
            // Older runtimes do not expose a profile color scheme.
        }

        core.NavigationStarting += OnNavigationStarting;
        core.NavigationCompleted += OnNavigationCompleted;
        core.SourceChanged += (_, _) => UpdateAddressBar();
        core.DocumentTitleChanged += (_, _) =>
        {
            var title = core.DocumentTitle ?? "";
            if (title.Length > 0) _logger.Log("title", "", SafeSource(), title);
        };
        core.NewWindowRequested += OnNewWindowRequested;
        core.DownloadStarting += OnDownloadStarting;
        core.PermissionRequested += OnPermissionRequested;

        _ready = true;
        UpdateButtons();
        if (navigateHome) NavigateHome();
    }

    public void NavigateHome()
    {
        if (!_ready) return;
        Navigate(_config.HomeUrl);
    }

    public void NavigateTo(string url) => Navigate(url);

    public void GoBack()
    {
        if (_ready && _webView.CanGoBack) _webView.GoBack();
    }

    public void GoForward()
    {
        if (_ready && _webView.CanGoForward) _webView.GoForward();
    }

    public void Reload()
    {
        if (_ready) _webView.Reload();
    }

    public void Stop()
    {
        if (_ready) _webView.CoreWebView2?.Stop();
    }

    public void FocusAddress()
    {
        _address.Focus();
        _address.SelectAll();
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _address.Enabled = enabled;
        _backButton.Enabled = enabled;
        _forwardButton.Enabled = enabled;
        _reloadButton.Enabled = enabled;
        _homeButton.Enabled = enabled;
        if (!enabled) Stop();
    }

    private void Navigate(string url)
    {
        if (!_ready || string.IsNullOrWhiteSpace(url)) return;
        try
        {
            _webView.CoreWebView2.Navigate(url);
        }
        catch (Exception ex)
        {
            _logger.Log("navigate_failed", ex.Message, url);
        }
    }

    private void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode != Keys.Enter) return;
        e.SuppressKeyPress = true;

        var text = _address.Text.Trim();
        if (text.Length == 0) return;

        _logger.Log("address_typed", text);

        if (LooksLikeUrl(text))
        {
            var url = text.Contains("://", StringComparison.Ordinal) ? text : "https://" + text;
            Navigate(url);
        }
        else
        {
            Navigate(SearchRewriter.BuildSearchUrl(text, _config.SearchEngineUrl));
        }
    }

    private static bool LooksLikeUrl(string text)
    {
        if (text.Contains(' ')) return false;
        if (text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
            text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) return true;
        if (text.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)) return true;
        return text.Contains('.');
    }

    private void OnNavigationStarting(object? sender, CoreWebView2NavigationStartingEventArgs e)
    {
        var url = e.Uri ?? "";

        if (SearchRewriter.TryRewrite(url, out var rewritten, out var reason))
        {
            if (RewriteIsLooping(url))
            {
                _logger.Log("rewrite_loop_suppressed", url);
                ShowStatus(
                    "Google stripped the Web filter repeatedly. Standard results are shown for this search; tell a mentor.",
                    alert: true);
                return;
            }

            e.Cancel = true;
            RewriteCount++;
            _logger.Log("search_rewrite", reason, url, rewritten);
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed) Navigate(rewritten);
            }));
            return;
        }

        if (IsBlocked(url, out var rule))
        {
            e.Cancel = true;
            AiAttempts++;
            _logger.Log("ai_attempt", rule, url);
            ShowStatus($"[BLOCKED] {rule} - AI tools are disabled during this test.", alert: true);
            BlockedAttempt?.Invoke(rule);
            return;
        }

        _logger.Log("nav_start", "", url);

        if (SearchRewriter.TryGetGoogleQuery(url, out var query))
        {
            SearchCount++;
            _logger.Log("search", query, url);
            SearchPerformed?.Invoke(query);
        }
    }

    private bool RewriteIsLooping(string url)
    {
        var now = DateTimeOffset.Now;
        if (now < _rewriteDisabledUntil) return true;

        if (!_rewriteFirstSeen.TryGetValue(url, out var first) ||
            now - first > TimeSpan.FromSeconds(10))
        {
            _rewriteFirstSeen[url] = now;
            _rewriteCounts[url] = 0;
        }

        _rewriteCounts[url] = _rewriteCounts.GetValueOrDefault(url) + 1;

        if (_rewriteCounts[url] <= 3) return false;

        _rewriteDisabledUntil = now.AddSeconds(30);
        return true;
    }

    private void OnNavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs e)
    {
        var url = SafeSource();
        if (e.IsSuccess)
        {
            _visits.Add(url, _webView.CoreWebView2?.DocumentTitle ?? "");
            _logger.Log("nav_ok", "", url, _webView.CoreWebView2?.DocumentTitle ?? "");
        }
        else
        {
            _logger.Log("nav_fail", e.WebErrorStatus.ToString(), url);
        }

        UpdateAddressBar();
        UpdateButtons();
    }

    private void OnNewWindowRequested(object? sender, CoreWebView2NewWindowRequestedEventArgs e)
    {
        e.Handled = true;
        if (string.IsNullOrWhiteSpace(e.Uri)) return;

        _logger.Log("popup_redirected", "", e.Uri);
        Navigate(e.Uri);
    }

    private void OnDownloadStarting(object? sender, CoreWebView2DownloadStartingEventArgs e)
    {
        e.Cancel = true;
        _logger.Log("download_blocked", "", e.DownloadOperation?.Uri ?? "");
        ShowStatus("Downloads are disabled during this test.", alert: true);
    }

    private void OnPermissionRequested(object? sender, CoreWebView2PermissionRequestedEventArgs e)
    {
        e.State = CoreWebView2PermissionState.Deny;
        e.Handled = true;
        _logger.Log("permission_denied", e.PermissionKind.ToString(), SafeSource());
    }

    private bool IsBlocked(string url, out string rule) =>
        IsBlockedUrl(url, _config.BlockedDomains, out rule);

    public static bool IsBlockedUrl(string url, IEnumerable<string> blockedDomains, out string rule)
    {
        rule = "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;

        if (uri.Scheme is "file" or "edge" or "chrome" or "about" or "view-source" or "devtools" or "msedge")
        {
            rule = uri.Scheme + "://";
            return true;
        }

        var host = uri.Host.ToLowerInvariant();

        if ((host == "bing.com" || host == "www.bing.com") &&
            uri.AbsolutePath.StartsWith("/chat", StringComparison.OrdinalIgnoreCase))
        {
            rule = "bing.com/chat";
            return true;
        }

        foreach (var entry in blockedDomains)
        {
            var domain = (entry ?? "").Trim().TrimStart('.').TrimEnd('.').ToLowerInvariant();
            if (domain.Length == 0) continue;

            if (host == domain || host.EndsWith("." + domain, StringComparison.Ordinal))
            {
                rule = domain;
                return true;
            }
        }

        return false;
    }

    private string SafeSource() => _webView.Source?.ToString() ?? "";

    private void UpdateAddressBar()
    {
        if (_address.Focused) return;
        _address.Text = SafeSource();
    }

    private void UpdateButtons()
    {
        _backButton.Enabled = _ready && _webView.CanGoBack;
        _forwardButton.Enabled = _ready && _webView.CanGoForward;
    }

    private void ShowStatus(string message, bool alert = false)
    {
        _status.Text = message;
        _status.BackColor = alert ? Color.FromArgb(255, 238, 230) : Color.FromArgb(236, 242, 250);
        _status.ForeColor = alert ? Color.FromArgb(150, 45, 20) : Color.FromArgb(45, 62, 90);

        _statusTimer.Stop();
        _statusTimer.Start();
    }

    private void ResetStatus()
    {
        _status.Text = DefaultStatus;
        _status.BackColor = Color.FromArgb(236, 242, 250);
        _status.ForeColor = Color.FromArgb(45, 62, 90);
    }
}
