using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace BenheimInventoryProtocol;

internal static class InventoryTransactionAudit
{
    private const long DefaultMaxBytes = 2L * 1024L * 1024L;
    private const string CurrentFileName = "BenheimInventoryAudit.log";
    private const string PreviousFileName = "BenheimInventoryAudit.previous.log";
    private static readonly object Sync = new object();
    private static string? currentPath;
    private static string? previousPath;
    private static long maxBytes = DefaultMaxBytes;

    internal static bool Initialize(string rootPath, long maximumBytes = DefaultMaxBytes)
    {
        lock (Sync)
        {
            try
            {
                Directory.CreateDirectory(rootPath);
                currentPath = Path.Combine(rootPath, CurrentFileName);
                previousPath = Path.Combine(rootPath, PreviousFileName);
                maxBytes = Math.Max(1024L, maximumBytes);
                return true;
            }
            catch (Exception)
            {
                currentPath = null;
                previousPath = null;
                return false;
            }
        }
    }

    internal static void Write(string level, string message)
    {
        lock (Sync)
        {
            if (currentPath == null || previousPath == null)
            {
                return;
            }

            try
            {
                string line = $"{DateTime.UtcNow:O} {level} [diag][InventoryTransaction] {message}{Environment.NewLine}";
                byte[] bytes = Encoding.UTF8.GetBytes(line);
                RotateIfNeeded(bytes.Length);
                using FileStream stream = new FileStream(
                    currentPath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.ReadWrite | FileShare.Delete);
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(flushToDisk: false);
            }
            catch (Exception)
            {
                // Diagnostics must never affect inventory behavior.
            }
        }
    }

    internal static IReadOnlyList<string> GetExistingPaths()
    {
        lock (Sync)
        {
            List<string> paths = new List<string>(2);
            if (previousPath != null && File.Exists(previousPath))
            {
                paths.Add(previousPath);
            }
            if (currentPath != null && File.Exists(currentPath))
            {
                paths.Add(currentPath);
            }
            return paths;
        }
    }

    private static void RotateIfNeeded(int additionalBytes)
    {
        if (currentPath == null
            || previousPath == null
            || !File.Exists(currentPath)
            || new FileInfo(currentPath).Length + additionalBytes <= maxBytes)
        {
            return;
        }

        if (File.Exists(previousPath))
        {
            File.Delete(previousPath);
        }
        File.Move(currentPath, previousPath);
    }
}
