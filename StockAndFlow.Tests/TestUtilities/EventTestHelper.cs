namespace StockAndFlow.Tests.TestUtilities;

/// <summary>
/// Helper class for testing event-raising behavior.
/// Provides utilities to verify that events are raised correctly.
/// </summary>
public static class EventTestHelper
{
    /// <summary>
    /// Subscribes to an event, executes an action, and verifies the event was raised.
    /// Returns captured event arguments if any.
    /// </summary>
    public static TEventArgs? VerifyEventRaised<TEventArgs>(
        Action<EventHandler<TEventArgs>> subscribe,
        Action action) where TEventArgs : EventArgs
    {
        var eventRaised = false;
        TEventArgs? capturedArgs = default;

        EventHandler<TEventArgs> handler = (sender, args) =>
        {
            eventRaised = true;
            capturedArgs = args;
        };

        subscribe(handler);
        action();

        if (!eventRaised)
        {
            throw new Exception("Expected event was not raised");
        }

        return capturedArgs;
    }

    /// <summary>
    /// Subscribes to an event (EventHandler), executes an action, and verifies the event was raised.
    /// </summary>
    public static void VerifyEventRaised(
        Action<EventHandler> subscribe,
        Action action)
    {
        var eventRaised = false;

        EventHandler handler = (sender, args) => eventRaised = true;

        subscribe(handler);
        action();

        if (!eventRaised)
        {
            throw new Exception("Expected event was not raised");
        }
    }

    /// <summary>
    /// Subscribes to an event, executes an action, and verifies the event was NOT raised.
    /// </summary>
    public static void VerifyEventNotRaised(
        Action<EventHandler> subscribe,
        Action action)
    {
        var eventRaised = false;

        EventHandler handler = (sender, args) => eventRaised = true;

        subscribe(handler);
        action();

        if (eventRaised)
        {
            throw new Exception("Event was raised but should not have been");
        }
    }

    /// <summary>
    /// Creates an event monitor that can track multiple event raises.
    /// Useful for testing scenarios where an event should fire multiple times.
    /// </summary>
    public static EventMonitor CreateMonitor()
    {
        return new EventMonitor();
    }

    public class EventMonitor
    {
        private int _eventCount = 0;
        private readonly List<object?> _capturedArgs = new();

        public int EventCount => _eventCount;
        public IReadOnlyList<object?> CapturedArgs => _capturedArgs.AsReadOnly();

        public EventHandler CreateHandler()
        {
            return (sender, args) =>
            {
                _eventCount++;
                _capturedArgs.Add(args);
            };
        }

        public EventHandler<T> CreateHandler<T>() where T : EventArgs
        {
            return (sender, args) =>
            {
                _eventCount++;
                _capturedArgs.Add(args);
            };
        }

        public void Reset()
        {
            _eventCount = 0;
            _capturedArgs.Clear();
        }
    }
}
