using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace SwiftScroll;

public sealed class TrayIcon : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _openSettingsItem;
    private readonly ToolStripMenuItem _exitItem;

    private readonly AppSettings _settings;

    public event EventHandler? OpenSettingsRequested;
    public event EventHandler? ExitRequested;

    public TrayIcon(AppSettings settings)
    {
        _settings = settings;

        _openSettingsItem = new ToolStripMenuItem("Settings...");
        _openSettingsItem.Click += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);

        _exitItem = new ToolStripMenuItem("Exit");
        _exitItem.Click += (s, e) => ExitRequested?.Invoke(this, EventArgs.Empty);

        var cms = CreateContextMenu();
        _notifyIcon = new NotifyIcon
        {
            Visible = true,
            Text = "Swift Scroll",
            ContextMenuStrip = cms,
            Icon = LoadIconSafe()
        };
        _notifyIcon.DoubleClick += (s, e) => OpenSettingsRequested?.Invoke(this, EventArgs.Empty);
        _notifyIcon.MouseUp += (s, e) => { if (e.Button == MouseButtons.Left) OpenSettingsRequested?.Invoke(this, EventArgs.Empty); };
    }

    private ContextMenuStrip CreateContextMenu()
    {
        var cms = new ContextMenuStrip
        {
            ShowImageMargin = false,
            ShowCheckMargin = true,
            Font = new Font("Segoe UI", 9f, System.Drawing.FontStyle.Regular, GraphicsUnit.Point),
            RenderMode = ToolStripRenderMode.System,
            Padding = new Padding(4, 4, 4, 4),
            AutoSize = true,
            MinimumSize = new Size(0, 0)
        };

        cms.Items.Add(_openSettingsItem);
        cms.Items.Add(_exitItem);

        ApplyItemStyling(cms);
        return cms;
    }

    private static void ApplyItemStyling(ContextMenuStrip cms)
    {
        foreach (ToolStripItem item in cms.Items)
        {
            switch (item)
            {
                case ToolStripMenuItem menuItem:
                    menuItem.Padding = new Padding(6, 4, 6, 4);
                    break;
                case ToolStripSeparator separator:
                    separator.Margin = new Padding(0, 4, 0, 4);
                    break;
            }
        }
    }

    private static System.Drawing.Icon LoadIconSafe()
    {
        // 1) Prefer built asset from output directory so icon updates are immediate after build.
        try
        {
            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "app.ico");
            if (File.Exists(iconPath))
            {
                return new System.Drawing.Icon(iconPath);
            }
        }
        catch (Exception ex) { Debug.WriteLine($"[TrayIcon] Failed to load output icon: {ex.Message}"); }

        // 2) Try WPF embedded resource (works in single-file publish)
        try
        {
            var uri = new Uri("pack://application:,,,/Assets/app.ico", UriKind.Absolute);
            var sri = System.Windows.Application.GetResourceStream(uri);
            if (sri != null)
                return new System.Drawing.Icon(sri.Stream);
        }
        catch (Exception ex) { Debug.WriteLine($"[TrayIcon] Failed to load embedded icon: {ex.Message}"); }

        // 3) Fallback to EXE associated icon (uses ApplicationIcon)
        try
        {
            var exePath = Process.GetCurrentProcess().MainModule!.FileName!;
            var ico = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
            if (ico != null) return ico;
        }
        catch (Exception ex) { Debug.WriteLine($"[TrayIcon] Failed to extract EXE icon: {ex.Message}"); }

        // 4) Last resort: default app icon
        return System.Drawing.SystemIcons.Application;
    }

    public void UpdateEnabled(bool enabled)
    {
        // Enabled toggle removed from tray menu; no action needed.
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }
}

