using System.Diagnostics;
using System.Text;
using WindowRPC.Interop;
using WindowRPC.Models;

namespace WindowRPC.Services;

internal sealed class ForegroundWindowWatcher : IDisposable
{
    private readonly NativeMethods.WinEventDelegate _callback;
    private IntPtr _foregroundHook;
    private IntPtr _titleChangeHook;

    public ForegroundWindowWatcher()
    {
        _callback = HandleWinEvent;
    }

    public event EventHandler<WindowSnapshot>? ForegroundChanged;

    public void Start()
    {
        if (_foregroundHook != IntPtr.Zero || _titleChangeHook != IntPtr.Zero)
        {
            return;
        }

        _foregroundHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventSystemForeground,
            NativeMethods.EventSystemForeground,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        _titleChangeHook = NativeMethods.SetWinEventHook(
            NativeMethods.EventObjectNameChange,
            NativeMethods.EventObjectNameChange,
            IntPtr.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);

        EmitCurrentSnapshot();
    }

    public WindowSnapshot GetCurrentSnapshot()
    {
        var hwnd = NativeMethods.GetForegroundWindow();
        if (hwnd == IntPtr.Zero)
        {
            return new WindowSnapshot();
        }

        var titleLength = NativeMethods.GetWindowTextLength(hwnd);
        var sb = new StringBuilder(titleLength + 1);
        NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);

        NativeMethods.GetWindowThreadProcessId(hwnd, out var processId);

        string? processName = null;
        if (processId != 0)
        {
            try
            {
                using var process = Process.GetProcessById((int)processId);
                processName = process.ProcessName;
            }
            catch
            {
                processName = null;
            }
        }

        return new WindowSnapshot
        {
            Title = string.IsNullOrWhiteSpace(sb.ToString()) ? "No active window" : sb.ToString().Trim(),
            ProcessId = processId == 0 ? null : processId,
            ProcessName = processName
        };
    }

    public IReadOnlyList<string> GetVisibleWindowTitles()
    {
        var titles = new List<string>();

        NativeMethods.EnumWindows((hwnd, _) =>
        {
            if (!NativeMethods.IsWindowVisible(hwnd))
            {
                return true;
            }

            var titleLength = NativeMethods.GetWindowTextLength(hwnd);
            if (titleLength <= 0)
            {
                return true;
            }

            var sb = new StringBuilder(titleLength + 1);
            NativeMethods.GetWindowText(hwnd, sb, sb.Capacity);
            var title = sb.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(title))
            {
                titles.Add(title);
            }

            return true;
        }, IntPtr.Zero);

        return titles;
    }

    public void Dispose()
    {
        if (_foregroundHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_foregroundHook);
            _foregroundHook = IntPtr.Zero;
        }

        if (_titleChangeHook != IntPtr.Zero)
        {
            NativeMethods.UnhookWinEvent(_titleChangeHook);
            _titleChangeHook = IntPtr.Zero;
        }
    }

    private void HandleWinEvent(
        IntPtr hWinEventHook,
        uint eventType,
        IntPtr hwnd,
        int idObject,
        int idChild,
        uint idEventThread,
        uint dwmsEventTime)
    {
        if (eventType == NativeMethods.EventObjectNameChange)
        {
            var currentForeground = NativeMethods.GetForegroundWindow();
            if (hwnd == IntPtr.Zero || currentForeground == IntPtr.Zero || hwnd != currentForeground)
            {
                return;
            }
        }

        EmitCurrentSnapshot();
    }

    private void EmitCurrentSnapshot()
    {
        ForegroundChanged?.Invoke(this, GetCurrentSnapshot());
    }
}
