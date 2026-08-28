using System;
using System.IO;
using System.Text;

namespace BenheimQoL.Infrastructure;

internal static class RuntimePrimitiveSnapshotFile
{
    internal static void WriteAtomically(string path, Action<TextWriter> write)
    {
        string temporaryPath = path + ".tmp";
        try
        {
            using (StreamWriter writer =
                new StreamWriter(temporaryPath, append: false, new UTF8Encoding(false)))
            {
                write(writer);
                writer.Flush();
            }

            if (File.Exists(path))
            {
                File.Replace(temporaryPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(temporaryPath, path);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
