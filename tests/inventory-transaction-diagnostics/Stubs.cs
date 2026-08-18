using System.Collections.Generic;

namespace BenheimQoL.Infrastructure
{
    internal static class Diagnostics
    {
        internal static List<DiagnosticEvent> Captured { get; } = new List<DiagnosticEvent>();
        internal static bool ThrowOnEmit { get; set; }

        internal static void Emit(DiagnosticEvent diagnosticEvent)
        {
            if (ThrowOnEmit)
            {
                throw new System.InvalidOperationException("throwing diagnostic sink control");
            }

            Captured.Add(diagnosticEvent);
        }
    }
}

namespace BenheimQoL.InventoryFeature
{
    internal static class TopLeftFeedbackHud
    {
        internal static List<string> Messages { get; } = new List<string>();

        internal static void ShowTransient(string message)
        {
            Messages.Add(message);
        }
    }
}

namespace BepInEx.Logging
{
    internal sealed class ManualLogSource
    {
        internal List<string> Info { get; } = new List<string>();
        internal List<string> Warnings { get; } = new List<string>();

        public void LogInfo(string line) => Info.Add(line);
        public void LogWarning(string line) => Warnings.Add(line);
    }
}
