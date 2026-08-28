using System;
using System.Collections.Generic;

namespace BenheimQoL.Infrastructure;

/// <summary>
/// Dispatches immutable gameplay facts to explicitly registered in-process
/// subscribers. Publishing is synchronous and preserves subscription order so
/// controllers finish their decisions before later diagnostic projections run.
/// </summary>
internal sealed class LocalGameEventBus
{
    private readonly Dictionary<Type, IEventRoute> routes =
        new Dictionary<Type, IEventRoute>();
    private readonly Action<Type, Exception> subscriberFailure;

    internal LocalGameEventBus(Action<Type, Exception> subscriberFailure)
    {
        this.subscriberFailure = subscriberFailure
            ?? throw new ArgumentNullException(nameof(subscriberFailure));
    }

    internal IDisposable Subscribe<TEvent>(Action<TEvent> subscriber)
        where TEvent : class
    {
        if (subscriber == null)
        {
            throw new ArgumentNullException(nameof(subscriber));
        }

        Type eventType = typeof(TEvent);
        if (!routes.TryGetValue(eventType, out IEventRoute? route))
        {
            route = new EventRoute<TEvent>(subscriberFailure);
            routes.Add(eventType, route);
        }

        return ((EventRoute<TEvent>)route).Subscribe(subscriber);
    }

    internal void Publish<TEvent>(TEvent gameEvent)
        where TEvent : class
    {
        if (gameEvent == null)
        {
            throw new ArgumentNullException(nameof(gameEvent));
        }

        if (routes.TryGetValue(typeof(TEvent), out IEventRoute? route))
        {
            ((EventRoute<TEvent>)route).Publish(gameEvent);
        }
    }

    internal void Reset()
    {
        routes.Clear();
    }

    private interface IEventRoute
    {
    }

    private sealed class EventRoute<TEvent> : IEventRoute
        where TEvent : class
    {
        private readonly List<Action<TEvent>> subscribers =
            new List<Action<TEvent>>();
        private readonly Action<Type, Exception> subscriberFailure;

        internal EventRoute(Action<Type, Exception> subscriberFailure)
        {
            this.subscriberFailure = subscriberFailure;
        }

        internal IDisposable Subscribe(Action<TEvent> subscriber)
        {
            subscribers.Add(subscriber);
            return new Subscription(this, subscriber);
        }

        internal void Publish(TEvent gameEvent)
        {
            // A snapshot makes subscription changes during delivery apply only
            // to the next fact. This keeps one publish deterministic.
            Action<TEvent>[] current = subscribers.ToArray();
            for (int index = 0; index < current.Length; index++)
            {
                try
                {
                    current[index](gameEvent);
                }
                catch (Exception exception)
                {
                    try
                    {
                        subscriberFailure(typeof(TEvent), exception);
                    }
                    catch
                    {
                        // Reporting a failed subscriber is diagnostic output.
                        // It cannot interrupt later gameplay subscribers.
                    }
                }
            }
        }

        private void Unsubscribe(Action<TEvent> subscriber)
        {
            subscribers.Remove(subscriber);
        }

        private sealed class Subscription : IDisposable
        {
            private EventRoute<TEvent>? route;
            private Action<TEvent>? subscriber;

            internal Subscription(EventRoute<TEvent> route, Action<TEvent> subscriber)
            {
                this.route = route;
                this.subscriber = subscriber;
            }

            public void Dispose()
            {
                EventRoute<TEvent>? currentRoute = route;
                Action<TEvent>? currentSubscriber = subscriber;
                route = null;
                subscriber = null;
                if (currentRoute != null && currentSubscriber != null)
                {
                    currentRoute.Unsubscribe(currentSubscriber);
                }
            }
        }
    }
}
