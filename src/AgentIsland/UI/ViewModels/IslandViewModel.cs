using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AgentIsland.Core;
using AgentIsland.Backend.Monitoring;
using AgentIsland.Backend.Settings;
using AgentIsland.UI.Providers;

namespace AgentIsland.UI.ViewModels;

/// <summary>
/// Core ViewModel managing the Dynamic Island's state, animations, and active session presentation.
/// Keeps low-level Win32 windowing code decoupled from high-level business rules.
/// </summary>
public sealed partial class IslandViewModel : ObservableObject, IDisposable
{
    [ObservableProperty]
    private IslandState _state = IslandState.Compact;

    [ObservableProperty]
    private bool _isDeliberatelyHidden;

    [ObservableProperty]
    private DisplayProvider? _activeProvider;

    [ObservableProperty]
    private ActivityState _activeActivity = ActivityState.Idle;

    [ObservableProperty]
    private string? _activeThreadTitle;

    [ObservableProperty]
    private bool _hasNeedsYou;

    private readonly IProviderVisibilityStore _visibilityStore;
    private readonly IActivityMonitor _activityMonitor;
    private readonly IIslandModel _islandModel;
    private readonly AgentIsland.Core.Navigation.IWindowService? _windowService;

    public IslandViewModel() : this(
        (App.Instance?.Services?.GetService(typeof(IProviderVisibilityStore)) as IProviderVisibilityStore) ?? new ProviderVisibilityStore(),
        (App.Instance?.Services?.GetService(typeof(IActivityMonitor)) as IActivityMonitor) ?? new ActivityMonitor(),
        (App.Instance?.Services?.GetService(typeof(IIslandModel)) as IIslandModel) ?? new IslandModel(),
        App.Instance?.Services?.GetService(typeof(AgentIsland.Core.Navigation.IWindowService)) as AgentIsland.Core.Navigation.IWindowService)
    {
    }

    public IslandViewModel(
        IProviderVisibilityStore visibilityStore,
        IActivityMonitor activityMonitor,
        IIslandModel islandModel,
        AgentIsland.Core.Navigation.IWindowService? windowService = null)
    {
        _visibilityStore = visibilityStore ?? throw new ArgumentNullException(nameof(visibilityStore));
        _activityMonitor = activityMonitor ?? throw new ArgumentNullException(nameof(activityMonitor));
        _islandModel = islandModel ?? throw new ArgumentNullException(nameof(islandModel));
        _windowService = windowService;


        _visibilityStore.PropertyChanged += OnVisibilityChanged;
        _activityMonitor.PropertyChanged += OnActivityChanged;
        _islandModel.PropertyChanged += OnIslandModelChanged;

        State = _islandModel.State;
        RefreshState();
    }

    private void OnIslandModelChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(IIslandModel.State))
        {
            State = _islandModel.State;
        }
    }

    private void OnVisibilityChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshState();
    }

    private void OnActivityChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        RefreshState();
    }

    public void RefreshState()
    {
        // One active provider drives the bar and the expanded panel; the
        // remaining enabled providers are reached from the hover switcher.
        var slots = _visibilityStore.SlotProviders;
        var active = slots.Count > 0 ? slots[0] : (DisplayProvider?)null;
        ActiveProvider = active;

        if (active is { } provider)
        {
            var tool = provider.ToTriggerTool();
            ActiveActivity = _activityMonitor.StateFor(tool);
            ActiveThreadTitle = _activityMonitor.ThreadFor(tool)?.Label;
        }
        else
        {
            ActiveActivity = ActivityState.Idle;
            ActiveThreadTitle = null;
        }

        HasNeedsYou = ActiveActivity == ActivityState.NeedsYou;
    }

    [RelayCommand]
    private void ToggleExpand()
    {
        var newState = State == IslandState.Expanded ? IslandState.Compact : IslandState.Expanded;
        State = newState;
        _islandModel.State = newState;
    }

    [RelayCommand]
    private void OpenSettings()
    {
        if (_windowService is not null)
        {
            _windowService.OpenSettings();
        }
        else
        {
            SettingsWindow.Open();
        }
    }


    public void Dispose()
    {
        _visibilityStore.PropertyChanged -= OnVisibilityChanged;
        _activityMonitor.PropertyChanged -= OnActivityChanged;
        _islandModel.PropertyChanged -= OnIslandModelChanged;
    }
}
