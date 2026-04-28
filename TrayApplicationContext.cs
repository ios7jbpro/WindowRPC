using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using WindowRPC.Forms;
using WindowRPC.Services;

namespace WindowRPC;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ConfigurationService _configurationService;
    private readonly ForegroundWindowWatcher _windowWatcher;
    private readonly MediaSessionWatcher _mediaSessionWatcher;
    private readonly PresenceCoordinator _presenceCoordinator;
    private OverridesEditorForm? _editorForm;

    public TrayApplicationContext()
    {
        _configurationService = new ConfigurationService(AppContext.BaseDirectory);
        _windowWatcher = new ForegroundWindowWatcher();
        _mediaSessionWatcher = new MediaSessionWatcher();
        _presenceCoordinator = new PresenceCoordinator(_configurationService, _windowWatcher, _mediaSessionWatcher);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "WindowRPC",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _configurationService.Start();
        _mediaSessionWatcher.Start();
        _presenceCoordinator.Start();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open Override Editor", null, (_, _) => OpenOverrideEditor());
        menu.Items.Add("Refresh Config", null, (_, _) => _configurationService.Reload());
        menu.Items.Add("Open Config Folder", null, (_, _) => OpenConfigFolder());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApplication());
        return menu;
    }

    private void OpenConfigFolder()
    {
        var psi = new ProcessStartInfo
        {
            FileName = _configurationService.BaseDirectory,
            UseShellExecute = true
        };

        Process.Start(psi);
    }

    private static Icon LoadTrayIcon()
    {
        var iconPath = Path.Combine(AppContext.BaseDirectory, "discord_icon.png");
        if (!File.Exists(iconPath))
        {
            return SystemIcons.Application;
        }

        using var bitmap = new Bitmap(iconPath);
        var handle = bitmap.GetHicon();
        return Icon.FromHandle(handle);
    }

    private void ExitApplication()
    {
        if (_editorForm is not null && !_editorForm.IsDisposed)
        {
            _editorForm.Close();
        }

        _presenceCoordinator.Dispose();
        _mediaSessionWatcher.Dispose();
        _windowWatcher.Dispose();
        _configurationService.Dispose();
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        ExitThread();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _presenceCoordinator.Dispose();
            _mediaSessionWatcher.Dispose();
            _windowWatcher.Dispose();
            _configurationService.Dispose();
            _notifyIcon.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OpenOverrideEditor()
    {
        if (_editorForm is not null && !_editorForm.IsDisposed)
        {
            _editorForm.BringToFront();
            _editorForm.Activate();
            return;
        }

        _editorForm = new OverridesEditorForm(_configurationService, _windowWatcher);
        _editorForm.FormClosed += (_, _) => _editorForm = null;
        _editorForm.Show();
    }
}
