using System.Collections.Concurrent;
using Aurora.Core.Models;
using Aurora.Core.Primitives;
using Aurora.Core.Tokens;

namespace Aurora.Core.Events;

public interface IAuroraEvent
{
    DateTimeOffset TimestampUtc { get; }
}

public sealed record WindowCatalogUpdatedEvent(IReadOnlyList<AppWindowEntry> Windows, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record WindowActivatedEvent(nint Hwnd, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record MonitorTopologyChangedEvent(IReadOnlyList<MonitorDescriptor> Monitors, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record AudioStateChangedEvent(double MasterVolume, bool IsMuted, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record ThemePreviewStartedEvent(AuroraThemeTokens PreviewTheme, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record ThemePreviewDiscardedEvent(AuroraThemeTokens RestoredTheme, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record ThemeAppliedEvent(AuroraThemeTokens ActiveTheme, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record ProfileChangedEvent(WorkspaceProfile ActiveProfile, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record AuroraNotificationReceivedEvent(AuroraNotification Notification, bool ShowToast, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record NotificationsUpdatedEvent(IReadOnlyList<AuroraNotification> Notifications, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record TaskbarLayoutChangedEvent(TaskbarSettings Settings, DateTimeOffset TimestampUtc) : IAuroraEvent;
public sealed record SafeModeStateChangedEvent(bool IsSafeMode, string Reason, DateTimeOffset TimestampUtc) : IAuroraEvent;

public interface IAuroraEventBus : IDisposable
{
    IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IAuroraEvent;
    void Publish<TEvent>(TEvent evt) where TEvent : class, IAuroraEvent;
    int GetSubscriberCount<TEvent>() where TEvent : class, IAuroraEvent;
}

public sealed class BoundedEventBus : IAuroraEventBus
{
    private readonly ConcurrentDictionary<Type, ConcurrentDictionary<Guid, Delegate>> _handlers = new();
    private bool _disposed;

    public IDisposable Subscribe<TEvent>(Action<TEvent> handler) where TEvent : class, IAuroraEvent
    {
        ArgumentNullException.ThrowIfNull(handler);
        ObjectDisposedException.ThrowIf(_disposed, this);

        var id = Guid.NewGuid();
        var bucket = _handlers.GetOrAdd(typeof(TEvent), _ => new ConcurrentDictionary<Guid, Delegate>());
        bucket[id] = handler;

        return new SubscriptionToken(() =>
        {
            if (_handlers.TryGetValue(typeof(TEvent), out var map))
            {
                map.TryRemove(id, out _);
            }
        });
    }

    public void Publish<TEvent>(TEvent evt) where TEvent : class, IAuroraEvent
    {
        if (_disposed || evt is null)
            return;

        if (!_handlers.TryGetValue(typeof(TEvent), out var bucket) || bucket.IsEmpty)
            return;

        foreach (var entry in bucket.Values.ToArray())
        {
            if (entry is Action<TEvent> typed)
            {
                try
                {
                    typed(evt);
                }
                catch
                {
                    // Individual subscriber faults must never tear down the event bus or shell host.
                }
            }
        }
    }

    public int GetSubscriberCount<TEvent>() where TEvent : class, IAuroraEvent =>
        _handlers.TryGetValue(typeof(TEvent), out var bucket) ? bucket.Count : 0;

    public void Dispose()
    {
        _disposed = true;
        _handlers.Clear();
    }

    private sealed class SubscriptionToken(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;
        public void Dispose() => Interlocked.Exchange(ref _onDispose, null)?.Invoke();
    }
}
