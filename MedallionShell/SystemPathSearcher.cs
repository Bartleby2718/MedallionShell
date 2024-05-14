using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Medallion.Shell;

internal static class SystemPathSearcher
{
    public static string? GetFullPathUsingSystemPathOrDefault(string executable)
    {
        var paths = Environment.GetEnvironmentVariable("PATH")!.Split(Path.PathSeparator);

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var pathExtensions = Environment.GetEnvironmentVariable("PATHEXT")!
                .Split(Path.PathSeparator)
                .Concat([string.Empty])
                .ToArray();
            return paths.SelectMany(path => pathExtensions.Select(pathExtension => Path.Combine(path, executable + pathExtension)))
                .FirstOrDefault(File.Exists);
        }

        return paths.Select(path => Path.Combine(path, executable)).FirstOrDefault(File.Exists);
    }
}
