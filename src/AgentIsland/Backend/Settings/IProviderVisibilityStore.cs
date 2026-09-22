using System.ComponentModel;
using AgentIsland.Core;
using AgentIsland.UI.Providers;

namespace AgentIsland.Backend.Settings;

/// <summary>
/// Contract for managing enabled providers and island silhouette slots.
/// </summary>
public interface IProviderVisibilityStore : INotifyPropertyChanged
{
    /// The single provider the island bar and expanded panel render. Null only
    /// when nothing is enabled/shown. The rest of the selection stays
    /// reachable through the hover switcher.
    DisplayProvider? ActiveProvider { get; }

    /// Make `provider` the active one. Ignored when it is not enabled/shown.
    void SetActiveProvider(DisplayProvider provider);

    IReadOnlyList<DisplayProvider> SlotProviders { get; }
    IReadOnlyList<DisplayProvider> Slots { get; }
    IReadOnlyList<DisplayProvider> Enabled { get; }
    IReadOnlyList<DisplayProvider> Order { get; }
    bool ClaudeVisible { get; set; }
    bool CodexVisible { get; set; }
    bool IsShown(DisplayProvider provider);
    bool IsEnabled(DisplayProvider provider);
    bool ClaudeShown { get; }
    bool CodexShown { get; }
    bool ClaudePanelShown { get; }
    bool CodexPanelShown { get; }
    bool AntigravityPanelShown { get; }
    bool GrokPanelShown { get; }
    bool CursorPanelShown { get; }
    bool DeepSeekPanelShown { get; }
    int GuestPanelCount { get; }
    bool IsVisible(TriggerTool tool);
    void RedetectGuests();
    int SelectedCount { get; }
    bool ClaudeDetected { get; }
    bool CodexDetected { get; }
    bool AntigravityDetected { get; }
    bool GrokDetected { get; }
    bool CursorDetected { get; }
    bool DeepSeekDetected { get; }
    bool SetEnabled(DisplayProvider provider, bool enabled);
    void MoveProvider(int oldIndex, int newIndex);
    bool Toggle(DisplayProvider provider);
}
