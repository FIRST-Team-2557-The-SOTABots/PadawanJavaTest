using System.Text.Json;

namespace FrcResearchTest;

public sealed class SessionLogger : IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public string Path { get; }

    public SessionLogger(string path)
    {
        Path = path;
        var directory = System.IO.Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

        _writer = new StreamWriter(new FileStream(
            path,
            FileMode.Append,
            FileAccess.Write,
            FileShare.Read))
        {
            AutoFlush = true,
        };
    }

    public void Log(string type, string detail = "", string url = "", string title = "")
    {
        var record = new
        {
            ts = DateTimeOffset.Now.ToString("O"),
            type,
            url,
            title,
            detail,
        };

        var line = JsonSerializer.Serialize(record, AppConfig.JsonOptions);
        lock (_lock)
        {
            try
            {
                _writer.WriteLine(line);
            }
            catch (IOException)
            {
                // Never let logging take down the test.
            }
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }
}
