using System.Windows.Threading;
using PodBridge.Core.Bluetooth;
using PodBridge.Core.Models;
using PodBridge.Core.Popup;
using PodBridge.Core.Protocol;

namespace PodBridge.App;

/// <summary>
/// Wires the connection-gated <see cref="IDeviceStateProvider"/> to the
/// <see cref="PopupWindow"/>: on every published state it asks the Core
/// <see cref="PopupPolicy"/> whether a pop-up is due and, if so, builds the Core
/// <see cref="PopupContent"/> for the identified model and shows the window. State
/// events arrive on a WinRT/thread-pool thread, so they are marshalled to the WPF UI
/// thread first — which also keeps the policy's per-state calls in order. At most one
/// pop-up is on screen: a new one replaces the previous. Owns only the event
/// subscription and the open window; the provider's lifetime belongs to the DI container.
/// </summary>
public sealed class PopupController : IDisposable
{
    private readonly IDeviceStateProvider _provider;
    private readonly IModelRegistry _models;
    private readonly PopupPolicy _policy;
    private readonly Dispatcher _dispatcher;
    private readonly string? _title;

    private PopupWindow? _window;

    private PopupController(
        IDeviceStateProvider provider,
        IModelRegistry models,
        PopupPolicy policy,
        Dispatcher dispatcher,
        string? title)
    {
        _provider = provider;
        _models = models;
        _policy = policy;
        _dispatcher = dispatcher;
        _title = title;
    }

    /// <summary>
    /// Creates a controller binding <paramref name="provider"/> to the pop-up. Call
    /// <see cref="Start"/> to begin listening.
    /// </summary>
    /// <param name="provider">The connection-gated device-state source.</param>
    /// <param name="models">Resolves the identified model to its display shape.</param>
    /// <param name="policy">Decides when a pop-up is due.</param>
    /// <param name="dispatcher">The UI dispatcher state events are marshalled to.</param>
    /// <param name="title">
    /// Optional title to show instead of the model name (for example the owner's device name);
    /// <see langword="null"/> shows the model name.
    /// </param>
    /// <returns>The not-yet-started controller.</returns>
    public static PopupController Create(
        IDeviceStateProvider provider,
        IModelRegistry models,
        PopupPolicy policy,
        Dispatcher dispatcher,
        string? title = null)
        => new(provider, models, policy, dispatcher, title);

    /// <summary>Subscribes to state changes. Call on the UI thread (e.g. from app startup).</summary>
    public void Start() => _provider.StateChanged += OnStateChanged;

    /// <summary>Unsubscribes and closes any pop-up still on screen. Call on the UI thread.</summary>
    public void Dispose()
    {
        _provider.StateChanged -= OnStateChanged;
        _window?.Close();
        _window = null;
    }

    private void OnStateChanged(object? sender, DeviceState state)
        => _dispatcher.InvokeAsync(() => Apply(state));

    private void Apply(DeviceState state)
    {
        if (!_policy.ShouldShow(state))
        {
            return;
        }

        var content = PopupContent.ForState(state, _models.Resolve(state.Model), _title);

        // Replace a pop-up that is still on screen rather than stacking them.
        _window?.Close();

        var window = new PopupWindow(content);
        window.Closed += (_, _) =>
        {
            if (ReferenceEquals(_window, window))
            {
                _window = null;
            }
        };
        _window = window;
        window.Show();
    }
}
