using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CodeMeter.Settings;

namespace CodeMeter.UI;

public partial class SettingsWindow : Window
{
    private readonly SettingsStore _store;

    public SettingsWindow(SettingsStore store)
    {
        InitializeComponent();
        _store = store;

        var s = store.Load();
        PollIntervalBox.Text = s.PollIntervalSeconds.ToString();
        TokenBudgetBox.Text  = s.FiveHourTokenBudget.ToString();
        foreach (var t in s.AlertThresholds)
            AddThresholdRow(t);
    }

    private void AddThresholdRow(int value = 75)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 4) };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        var box = new TextBox
        {
            Text            = value.ToString(),
            Background      = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground      = new SolidColorBrush(Color.FromRgb(205, 214, 244))
        };
        var btn = new Button
        {
            Content = "✕",
            Padding = new Thickness(4, 0, 4, 0),
            Margin  = new Thickness(4, 0, 0, 0)
        };
        btn.Click += (_, _) => ThresholdsPanel.Children.Remove(row);

        Grid.SetColumn(btn, 1);
        row.Children.Add(box);
        row.Children.Add(btn);
        ThresholdsPanel.Children.Add(row);
    }

    private void AddThreshold_Click(object sender, RoutedEventArgs e) => AddThresholdRow();

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (!int.TryParse(PollIntervalBox.Text, out var poll) || poll < 10)
            { Err("Poll interval must be at least 10 seconds."); return; }

        if (!decimal.TryParse(TokenBudgetBox.Text, out var budget) || budget <= 0)
            { Err("Token budget must be a positive number."); return; }

        var thresholds = new List<int>();
        foreach (Grid row in ThresholdsPanel.Children)
        {
            var box = (TextBox)row.Children[0];
            if (!int.TryParse(box.Text, out var t) || t < 1 || t > 100)
                { Err($"Threshold '{box.Text}' must be 1–100."); return; }
            thresholds.Add(t);
        }

        _store.Save(new AppSettings
        {
            PollIntervalSeconds = poll,
            FiveHourTokenBudget = budget,
            AlertThresholds     = thresholds.Distinct().OrderBy(t => t).ToList()
        });

        DialogResult = true;
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) { DialogResult = false; Close(); }

    private void Err(string msg) =>
        MessageBox.Show(msg, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);

}
