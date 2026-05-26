using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

namespace CodeMeter.UI;

public partial class UsageWindow : Window
{
    public event EventHandler? SettingsRequested;

    private bool _forceClose;

    public UsageWindow()
    {
        InitializeComponent();
    }

    /// <summary>Drag the window when the title bar is pressed.</summary>
    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    /// <summary>Stop the mouse-down from bubbling up to the drag handler.</summary>
    private void CloseBtn_MouseDown(object sender, MouseButtonEventArgs e) =>
        e.Handled = true;

    private void CloseBtn_Click(object sender, MouseButtonEventArgs e) => Hide();

    private void Settings_Click(object sender, MouseButtonEventArgs e) =>
        SettingsRequested?.Invoke(this, EventArgs.Empty);

    /// <summary>Update the "last refreshed" timestamp in the footer.</summary>
    public void MarkUpdated() =>
        LastUpdatedText.Text = $"Updated {DateTime.Now:HH:mm:ss}";

    /// <summary>
    /// Intercept the normal close so the user just hides the window.
    /// App.OnExit calls ForceClose() to actually destroy it.
    /// </summary>
    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_forceClose)
        {
            e.Cancel = true;
            Hide();
        }
    }

    public void ForceClose()
    {
        _forceClose = true;
        Close();
    }
}
