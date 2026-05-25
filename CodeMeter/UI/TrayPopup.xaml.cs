using System.Windows.Controls;
using System.Windows.Input;

namespace CodeMeter.UI;

public partial class TrayPopup : UserControl
{
    public static event EventHandler? SettingsRequested;

    public TrayPopup()
    {
        InitializeComponent();
    }

    public void MarkUpdated()
    {
        LastUpdatedText.Text = "Updated just now";
    }

    private void Settings_Click(object sender, MouseButtonEventArgs e)
        => SettingsRequested?.Invoke(this, EventArgs.Empty);
}
