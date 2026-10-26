using PodBridge.Core.Models;

namespace PodBridge.Core.Popup;

/// <summary>
/// Decides when the connect / case-open pop-up should appear. The pop-up is shown on
/// the transition from "not live" to "live" in the connection-gated
/// <see cref="DeviceState"/> stream — that is, when the AirPods connect, or start
/// advertising again after going quiet (for example when the case lid is reopened
/// while they are still connected). Because <see cref="DeviceState.IsLive"/> is only
/// ever true for the connected AirPods (the tracker gates on the connection monitor),
/// nearby strangers' AirPods can never trigger it.
/// <para>
/// A short cooldown suppresses repeat pop-ups when the state flaps (live → stale →
/// live) in quick succession. OS-free and clock-injectable so it is fully unit-testable
/// without a device (constitution: Core is platform-neutral).
/// </para>
/// </summary>
public sealed class PopupPolicy
{
    /// <summary>Default minimum gap between two pop-ups.</summary>
    public static readonly TimeSpan DefaultCooldown = TimeSpan.FromSeconds(10);

    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _cooldown;
    private readonly Lock _gate = new();

    private bool _wasLive;
    private DateTimeOffset? _lastShown;

    /// <summary>Creates a policy with an optional clock and cooldown.</summary>
    /// <param name="timeProvider">Clock source; defaults to <see cref="TimeProvider.System"/>.</param>
    /// <param name="cooldown">Minimum gap between pop-ups; defaults to <see cref="DefaultCooldown"/>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="cooldown"/> is negative.</exception>
    public PopupPolicy(TimeProvider? timeProvider = null, TimeSpan? cooldown = null)
    {
        var effectiveCooldown = cooldown ?? DefaultCooldown;
        ArgumentOutOfRangeException.ThrowIfLessThan(effectiveCooldown, TimeSpan.Zero, nameof(cooldown));

        _timeProvider = timeProvider ?? TimeProvider.System;
        _cooldown = effectiveCooldown;
    }

    /// <summary>
    /// Feeds the next published state and reports whether a pop-up should be shown for it.
    /// Call once per published <see cref="DeviceState"/>, in order.
    /// </summary>
    /// <param name="state">The state that was just published.</param>
    /// <returns><see langword="true"/> when the pop-up should appear now.</returns>
    public bool ShouldShow(DeviceState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        lock (_gate)
        {
            var becameLive = state.IsLive && !_wasLive;
            _wasLive = state.IsLive;
            if (!becameLive)
            {
                return false;
            }

            var now = _timeProvider.GetUtcNow();
            if (_lastShown is { } last && now - last < _cooldown)
            {
                return false; // flapping: a pop-up was shown moments ago
            }

            _lastShown = now;
            return true;
        }
    }
}
