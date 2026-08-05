using System.IO;
using BepInEx;
using BenheimInventoryProtocol;
using UnityEngine;

namespace BenheimQoL.Infrastructure;

internal static class DiagnosticLogExporter
{
    internal static void Update()
    {
        if (!InputState.IsKeyDown(KeyCode.F7))
        {
            return;
        }

        Export();
    }

    private static void Export()
    {
        try
        {
            string sourcePath = Path.Combine(Paths.BepInExRootPath, "LogOutput.log");
            string desktopPath = System.Environment.GetFolderPath(
                System.Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrWhiteSpace(desktopPath))
            {
                desktopPath = Path.Combine(
                    System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal),
                    "Desktop");
            }

            Directory.CreateDirectory(desktopPath);
            string fileName = $"Benheim-log-{System.DateTime.Now:yyyyMMdd-HHmmss-fff}.txt";
            string destinationPath = Path.Combine(desktopPath, fileName);
            using (var destination = new FileStream(
                destinationPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.Read))
            {
                CopyLog(destination, sourcePath, "Active BepInEx log");
                foreach (string auditPath in InventoryTransactionAudit.GetExistingPaths())
                {
                    CopyLog(destination, auditPath, Path.GetFileName(auditPath));
                }
            }

            Diagnostics.Event("Core", "log_exported", $"file=\"{fileName}\"");
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                $"Benheim log saved to Desktop\n{fileName}");
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError($"Could not export Benheim log: {ex}");
            Player.m_localPlayer?.Message(
                MessageHud.MessageType.Center,
                "Could not export Benheim log; check LogOutput.log");
        }
    }

    private static void CopyLog(FileStream destination, string sourcePath, string title)
    {
        if (!File.Exists(sourcePath))
        {
            return;
        }

        byte[] header = System.Text.Encoding.UTF8.GetBytes(
            $"\n===== {title} =====\n");
        destination.Write(header, 0, header.Length);
        using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        source.CopyTo(destination);
    }
}
