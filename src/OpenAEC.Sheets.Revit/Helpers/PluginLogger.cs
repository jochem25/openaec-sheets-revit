using System.IO;
using System.Runtime.CompilerServices;

namespace OpenAEC.Sheets.Revit.Helpers;

/// <summary>
/// Simpele file-logger voor plugin-diagnostiek.
/// Schrijft naar %TEMP%\OpenAEC.Sheets.log
/// </summary>
internal static class PluginLogger
{
    private static readonly string LogPath = Path.Combine(Path.GetTempPath(), "OpenAEC.Sheets.log");

    private static readonly object Lock = new();

    internal static void Log(string message, [CallerMemberName] string? caller = null)
    {
        try
        {
            lock (Lock)
            {
                File.AppendAllText(LogPath,
                    $"[{DateTime.Now:HH:mm:ss.fff}] [{caller}] {message}{Environment.NewLine}");
            }
        }
        catch
        {
            // Logging mag de plugin nooit laten crashen
        }
    }

    internal static void LogException(Exception ex, [CallerMemberName] string? caller = null)
    {
        Log($"EXCEPTION: {ex.GetType().Name}: {ex.Message}", caller);
        if (ex.InnerException is not null)
            Log($"  INNER: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}", caller);
        Log($"  STACK: {ex.StackTrace}", caller);
    }
}
