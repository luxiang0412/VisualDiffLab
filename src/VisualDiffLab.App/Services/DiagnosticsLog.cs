using System.Text;
using System.Text.Json;

namespace VisualDiffLab.App.Services;

/// <summary>
/// Minimal local diagnostics log. Deliberately never records file paths, file names or
/// pixel data. Only operation types, formats/dimensions, durations and exception types.
/// </summary>
public sealed class DiagnosticsLog
{
    private const long MaxBytes = 2 * 1024 * 1024;
    private readonly object _gate = new();
    private readonly string _path;

    private DiagnosticsLog()
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "VisualDiffLab",
            "logs");
        Directory.CreateDirectory(folder);
        _path = Path.Combine(folder, "app.log");
    }

    public static DiagnosticsLog Shared { get; } = new();

    public void Info(string message) => Write("INFO", message, null);

    public void Error(string message, Exception? exception) => Write("ERROR", message, exception);

    private void Write(string level, string message, Exception? exception)
    {
        try
        {
            string line = JsonSerializer.Serialize(new
            {
                timestamp = DateTimeOffset.Now.ToString("O"),
                level,
                message,
                exception = exception is null ? null : exception.GetType().FullName,
                hresult = exception?.HResult,
            });

            lock (_gate)
            {
                FileInfo info = new(_path);
                if (info.Exists && info.Length > MaxBytes)
                {
                    File.Move(_path, _path + ".old", overwrite: true);
                }

                File.AppendAllText(_path, line + Environment.NewLine, Encoding.UTF8);
            }
        }
        catch (IOException)
        {
            // Logging must never destabilize the app.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
