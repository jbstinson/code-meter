using System.Windows.Controls;
using System.Windows.Input;

namespace CodeMeter.UI;

public partial class TrayPopup : UserControl
{
    public static event EventHandler? SettingsRequested;

    private DateTime _lastUpdated = DateTime.Now;

    public TrayPopup()
    {
        InitializeComponent();
    }

    public void MarkUpdated()
    {
        _lastUpdated = DateTime.Now;
        LastUpdatedText.Text = "Updated just now";
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
        => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
