using System.Collections.Generic;

namespace BenheimQoL.Infrastructure
{
    internal sealed class DiagnosticEvent
    {
        internal static DiagnosticEvent Create(string domain, string name) => new DiagnosticEvent();
        internal DiagnosticEvent Integer(string name, int value) => this;
        internal DiagnosticEvent Boolean(string name, bool value) => this;
        internal DiagnosticEvent Number(string name, float value) => this;
    }

    internal static class Diagnostics
    {
        internal static void Emit(DiagnosticEvent value) { }
    }
}
