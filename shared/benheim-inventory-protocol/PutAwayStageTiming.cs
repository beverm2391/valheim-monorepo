using System.Diagnostics;

namespace BenheimInventoryProtocol;

/// <summary>
/// Monotonic timing for bounded Put Away stage diagnostics. Timing is
/// observational only: no protocol decision or transaction progress depends
/// on a timestamp.
/// </summary>
internal static class PutAwayStageTiming
{
    internal static long Start() => Stopwatch.GetTimestamp();

    internal static double ElapsedMilliseconds(long startedAt) =>
        ElapsedMilliseconds(startedAt, Stopwatch.GetTimestamp());

    internal static double ElapsedMilliseconds(long startedAt, long completedAt)
    {
        if (startedAt <= 0L || completedAt <= startedAt)
        {
            return 0d;
        }

        return (completedAt - startedAt) * 1000d / Stopwatch.Frequency;
    }
}
