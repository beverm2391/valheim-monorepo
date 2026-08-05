using System;
using System.IO;
using BenheimInventoryProtocol;

string root = Path.Combine(Path.GetTempPath(), "benheim-audit-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);

try
{
    Assert(
        InventoryTransactionAudit.Initialize(root, maximumBytes: 1024L),
        "audit initialization failed");
    string first = "first " + new string('a', 700);
    string second = "second " + new string('b', 700);
    InventoryTransactionAudit.Write("INFO", first);
    InventoryTransactionAudit.Write("WARN", second);

    string current = Path.Combine(root, "BenheimInventoryAudit.log");
    string previous = Path.Combine(root, "BenheimInventoryAudit.previous.log");
    Assert(File.Exists(current), "current audit file was not created");
    Assert(File.Exists(previous), "audit file did not rotate");
    Assert(File.ReadAllText(previous).Contains(first), "rotated audit lost the first entry");
    Assert(File.ReadAllText(current).Contains(second), "current audit lost the second entry");

    InventoryTransactionAudit.Write("INFO", "third " + new string('c', 700));
    Assert(!File.ReadAllText(previous).Contains(first), "rotation retained more than one previous file");
    Assert(File.ReadAllText(previous).Contains(second), "rotation did not preserve the newest previous file");
    Assert(InventoryTransactionAudit.GetExistingPaths().Count == 2, "audit export paths were incomplete");

    string invalidRoot = Path.Combine(root, "not-a-directory");
    File.WriteAllText(invalidRoot, "file");
    Assert(
        !InventoryTransactionAudit.Initialize(invalidRoot),
        "invalid audit root did not fail closed");
    InventoryTransactionAudit.Write("INFO", "ignored after failed initialization");
}
finally
{
    Directory.Delete(root, recursive: true);
}

Console.WriteLine("inventory transaction audit tests passed");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
