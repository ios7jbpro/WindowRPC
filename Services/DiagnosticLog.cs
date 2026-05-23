using System.Diagnostics;

namespace WindowRPC.Services;

internal static class DiagnosticLog
{
    private static readonly object Sync = new();

    public static void Write(string message)
    {
        var line = $"{DateTimeOffset.Now:O} {message}";
        Debug.WriteLine(line);

        try
        {
            lock (Sync)
            {
                File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "windowrpc.log"), line + Environment.NewLine);
            }
        }
        catch
        {
            // Diagnostics must never interfere with presence updates.
        }
    }
}
