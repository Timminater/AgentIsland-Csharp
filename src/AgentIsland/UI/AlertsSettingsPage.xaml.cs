using System.Windows.Controls;
using System.Windows.Input;
using AgentIsland.Backend.Settings;
using AgentIsland.UI.Localization;

namespace AgentIsland.UI;

public sealed partial class AlertsSettingsPage : UserControl
{
    private readonly bool _initialized;
    private readonly AlertThresholdStore _thresholdStore;
    private readonly DepletionForecastSettingsStore _forecastStore;

    public AlertsSettingsPage() : this(null, null) { }

    public AlertsSettingsPage(
        AlertThresholdStore? thresholdStore = null,
        DepletionForecastSettingsStore? forecastStore = null)
    {
        _thresholdStore = thresholdStore
            ?? (App.Instance?.Services?.GetService(typeof(AlertThresholdStore)) as AlertThresholdStore)
            ?? new AlertThresholdStore();
        _forecastStore = forecastStore
            ?? (App.Instance?.Services?.GetService(typeof(DepletionForecastSettingsStore)) as DepletionForecastSettingsStore)
            ?? new DepletionForecastSettingsStore();

        InitializeComponent();

        WarningLabelBlock.Text = L10n.Tr("Warning");
        CriticalLabelBlock.Text = L10n.Tr("Critical");

        var store = _thresholdStore;
        AlertsToggle.IsOn = store.Enabled;
        WarningField.Text = store.WarningPercent.ToString();
        CriticalField.Text = store.CriticalPercent.ToString();

        ForecastThresholdLabel.Text = L10n.Tr("Show when remaining is below");
        ForecastWindowLabel.Text = L10n.Tr("Average window");
        ForecastToggle.IsOn = _forecastStore.Enabled;
        ForecastThresholdField.Text = _forecastStore.ThresholdPercent.ToString();
        var windowLabels = new[]
        {
            L10n.Tr("Dynamic"), "5 min", "10 min", "15 min", "30 min", "60 min",
        };
        foreach (var label in windowLabels) ForecastWindowCombo.Items.Add(label);
        ForecastWindowCombo.SelectedIndex = Math.Max(
            0, Array.IndexOf(DepletionForecastSettingsStore.WindowPresets, _forecastStore.WindowMinutes));

        AlertsHost.Opacity = store.Enabled ? 1.0 : 0.40;
        AlertsHost.IsEnabled = store.Enabled;

        void ApplyForecastEnabled(bool enabled)
        {
            ForecastHost.Opacity = enabled ? 1.0 : 0.40;
            ForecastHost.IsEnabled = enabled;
        }
        ApplyForecastEnabled(_forecastStore.Enabled);

        AlertsToggle.Toggled += enabled =>
        {
            if (!_initialized) return;
            store.Enabled = enabled;
            AlertsHost.Opacity = enabled ? 1.0 : 0.40;
            AlertsHost.IsEnabled = enabled;
        };

        void CommitWarning()
        {
            if (!_initialized) return;
            if (int.TryParse(WarningField.Text, out var value))
            {
                store.WarningPercent = value;
            }
            WarningField.Text = store.WarningPercent.ToString();
        }

        WarningField.LostFocus += (_, _) => CommitWarning();
        WarningField.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) CommitWarning();
        };

        void CommitCritical()
        {
            if (!_initialized) return;
            if (int.TryParse(CriticalField.Text, out var value))
            {
                store.CriticalPercent = value;
            }
            CriticalField.Text = store.CriticalPercent.ToString();
        }

        CriticalField.LostFocus += (_, _) => CommitCritical();
        CriticalField.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) CommitCritical();
        };

        ForecastToggle.Toggled += enabled =>
        {
            if (!_initialized) return;
            _forecastStore.Enabled = enabled;
            ApplyForecastEnabled(enabled);
        };

        void CommitForecastThreshold()
        {
            if (!_initialized) return;
            if (int.TryParse(ForecastThresholdField.Text, out var value))
            {
                _forecastStore.ThresholdPercent = value;
            }
            ForecastThresholdField.Text = _forecastStore.ThresholdPercent.ToString();
        }

        ForecastThresholdField.LostFocus += (_, _) => CommitForecastThreshold();
        ForecastThresholdField.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) CommitForecastThreshold();
        };
        ForecastWindowCombo.SelectionChanged += (_, _) =>
        {
            if (!_initialized || ForecastWindowCombo.SelectedIndex < 0) return;
            _forecastStore.WindowMinutes =
                DepletionForecastSettingsStore.WindowPresets[ForecastWindowCombo.SelectedIndex];
        };

        _initialized = true;
    }
}
