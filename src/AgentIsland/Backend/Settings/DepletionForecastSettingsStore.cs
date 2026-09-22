using System.ComponentModel;
using AgentIsland.Core;

namespace AgentIsland.Backend.Settings;

/// Controls the optional top-bar estimate for how long the active provider's
/// remaining quota or account balance will last. A zero-minute window means
/// automatic: a short horizon while the provider is working, and no stale
/// countdown while the local session is paused.
public sealed class DepletionForecastSettingsStore : INotifyPropertyChanged
{
    private const string EnabledKey = "AgentIsland.depletionForecastEnabled";
    private const string ThresholdKey = "AgentIsland.depletionForecastThreshold";
    private const string WindowKey = "AgentIsland.depletionForecastWindowMinutes";

    public static readonly int[] WindowPresets = { 0, 5, 10, 15, 30, 60 };

    private bool _enabled;
    private int _thresholdPercent;
    private int _windowMinutes;

    public DepletionForecastSettingsStore()
    {
        _enabled = Preferences.Get<bool?>(EnabledKey) ?? false;
        _thresholdPercent = Math.Clamp(Preferences.Get<int?>(ThresholdKey) ?? 20, 1, 99);
        var savedWindow = Preferences.Get<int?>(WindowKey) ?? 0;
        _windowMinutes = WindowPresets.Contains(savedWindow) ? savedWindow : 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (_enabled == value) return;
            _enabled = value;
            Preferences.Set(EnabledKey, value);
            Raise(nameof(Enabled));
        }
    }

    /// Remaining quota percentage below which a percentage-based provider
    /// receives a forecast. Balance providers have no known maximum, so their
    /// estimate is shown whenever a reliable decline is available.
    public int ThresholdPercent
    {
        get => _thresholdPercent;
        set
        {
            var clamped = Math.Clamp(value, 1, 99);
            if (_thresholdPercent == clamped) return;
            _thresholdPercent = clamped;
            Preferences.Set(ThresholdKey, clamped);
            Raise(nameof(ThresholdPercent));
        }
    }

    /// Zero is automatic; otherwise one of WindowPresets.
    public int WindowMinutes
    {
        get => _windowMinutes;
        set
        {
            var normalized = WindowPresets.Contains(value) ? value : 0;
            if (_windowMinutes == normalized) return;
            _windowMinutes = normalized;
            Preferences.Set(WindowKey, normalized);
            Raise(nameof(WindowMinutes));
        }
    }

    private void Raise(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
