using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CodeMeter.Alerts;
using CodeMeter.Core;
using CodeMeter.Settings;
using CodeMeter.UI;
using H.NotifyIcon;
using H.NotifyIcon.Core;
using Microsoft.Toolkit.Uwp.Notifications;

namespace CodeMeter;

public partial class App : Application
{
    private TaskbarIcon? _tray;
    private TrayViewModel? _vm;
    private TrayPopup? _popup;
    private PollingService? _poller;
    private Func<Task>? _pollCallback;
    private SettingsStore? _store;
    private AlertService? _alerts;
    private DispatcherTimer? _pulseTimer;
    private bool _pulseOn;
    private IntPtr _lastIconHandle = IntPtr.Zero;

    private static string SettingsPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "CodeMeter", "settings.json");

    private static string ClaudeDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                     ".claude", "projects");

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Catch any unhandled exceptions so the app doesn't silently disappear
        DispatcherUnhandledException += (_, ex) =>
        {
            MessageBox.Show(ex.Exception.ToString(), "Code Meter — Startup Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) =>
            MessageBox.Show(ex.ExceptionObject?.ToString(), "Code Meter — Fatal Error",
                MessageBoxButton.OK, MessageBoxImage.Error);

        _store = new SettingsStore(SettingsPath);
        if (!_store.Exists())
            new SettingsWindow(_store).ShowDialog();

        var reader = new UsageReader();
        var agg    = new UsageAggregator();
        _vm        = new TrayViewModel();
        _popup     = new TrayPopup { DataContext = _vm };
        _alerts    = new AlertService(ShowToast);

        // Retrieve the TaskbarIcon from App.xaml resources — it must live there so that
        // WPF fires its Loaded event and H.NotifyIcon registers with the Windows taskbar.
        _tray = (TaskbarIcon)FindResource("TrayIcon");
        _tray.TrayPopup   = _popup;
        _tray.ContextMenu = BuildContextMenu();

        // Set an initial green icon immediately so the tray circle is visible at once.
        SetIcon(Color.FromArgb(166, 227, 161));

        TrayPopup.SettingsRequested += (_, _) => OpenSettings();

        _pollCallback = async () =>
        {
            var settings = _store.Load();
            var entries  = reader.ReadAll(ClaudeDir).ToList();
            var now      = DateTime.Now;
            var daily    = agg.ComputeDaily(entries, settings, now);
            var weekly   = agg.ComputeWeekly(entries, settings, now);

            await Dispatcher.InvokeAsync(() =>
            {
                _vm.Update(daily, weekly);
                _popup.MarkUpdated();
                RefreshIcon(_vm.IconColor);
                _alerts.Check(daily, weekly, settings);
            });
        };

        var settings = _store.Load();
        _poller = new PollingService(settings.PollIntervalSeconds, _pollCallback);
    }

    private void RefreshIcon(Color color)
    {
        if (color == Color.FromArgb(139, 0, 0))
        {
            EnsurePulse();
            return;
        }
        StopPulse();
        SetIcon(color);
    }

    private void EnsurePulse()
    {
        if (_pulseTimer is { IsEnabled: true }) return;
        _pulseTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _pulseTimer.Tick += (_, _) =>
        {
            _pulseOn = !_pulseOn;
            SetIcon(_pulseOn ? Color.FromArgb(139, 0, 0) : Color.FromArgb(40, 0, 0));
        };
        _pulseTimer.Start();
    }

    private void StopPulse()
    {
        _pulseTimer?.Stop();
        _pulseTimer = null;
    }

    private void SetIcon(Color color)
    {
        using var bmp = new Bitmap(16, 16);
        using var g   = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.Clear(Color.Transparent);
        using var brush = new SolidBrush(color);
        g.FillEllipse(brush, 1, 1, 14, 14);
        var hIcon = bmp.GetHicon();
        _tray!.Icon = Icon.FromHandle(hIcon);
        if (_lastIconHandle != IntPtr.Zero)
            DestroyIcon(_lastIconHandle);
        _lastIconHandle = hIcon;
    }

    private System.Windows.Controls.ContextMenu BuildContextMenu()
    {
        var cm       = new System.Windows.Controls.ContextMenu();
        var settings = new System.Windows.Controls.MenuItem { Header = "Settings" };
        var refresh  = new System.Windows.Controls.MenuItem { Header = "Refresh Now" };
        var exit     = new System.Windows.Controls.MenuItem { Header = "Exit" };

        settings.Click += (_, _) => OpenSettings();
        refresh.Click  += async (_, _) => { if (_poller != null) await _poller.ForceRunAsync(); };
        exit.Click     += (_, _) => { StopPulse(); _poller?.Dispose(); _tray?.Dispose(); Shutdown(); };

        cm.Items.Add(settings);
        cm.Items.Add(refresh);
        cm.Items.Add(new System.Windows.Controls.Separator());
        cm.Items.Add(exit);
        return cm;
    }

    private void OpenSettings()
    {
        new SettingsWindow(_store!).ShowDialog();
        _poller?.Dispose();
        var settings = _store!.Load();
        _poller = new PollingService(settings.PollIntervalSeconds, _pollCallback!);
    }

    private static void ShowToast(string title, string body)
    {
        try
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(body)
                .Show();
        }
        catch { /* toasts not available in this environment */ }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        StopPulse();
        _poller?.Dispose();
        _tray?.Dispose();
        if (_lastIconHandle != IntPtr.Zero)
            DestroyIcon(_lastIconHandle);
        base.OnExit(e);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
