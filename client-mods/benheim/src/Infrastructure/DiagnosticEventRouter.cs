using System;

namespace BenheimQoL.Infrastructure;

internal delegate bool DiagnosticEventSelector(DiagnosticEvent diagnosticEvent);
internal delegate void DiagnosticEventDestination(DiagnosticEvent diagnosticEvent);

internal sealed class DiagnosticEventRoute
{
    private readonly DiagnosticEventSelector selector;
    private readonly DiagnosticEventDestination destination;

    internal DiagnosticEventRoute(
        DiagnosticEventSelector selector,
        DiagnosticEventDestination destination)
    {
        this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
        this.destination = destination ?? throw new ArgumentNullException(nameof(destination));
    }

    internal void Route(DiagnosticEvent diagnosticEvent)
    {
        if (selector(diagnosticEvent))
        {
            destination(diagnosticEvent);
        }
    }
}

internal sealed class DiagnosticEventRouter
{
    private readonly DiagnosticEventRoute[] routes;

    internal DiagnosticEventRouter(params DiagnosticEventRoute[] routes)
    {
        this.routes = routes == null
            ? throw new ArgumentNullException(nameof(routes))
            : (DiagnosticEventRoute[])routes.Clone();
    }

    internal void Route(DiagnosticEvent diagnosticEvent)
    {
        if (diagnosticEvent == null)
        {
            throw new ArgumentNullException(nameof(diagnosticEvent));
        }

        for (int index = 0; index < routes.Length; index++)
        {
            routes[index].Route(diagnosticEvent);
        }
    }
}
