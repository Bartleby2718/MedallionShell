using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Medallion.Shell;

internal static class SystemPathSearcher
{
    public static string? GetFullPathUsingSystemPathOrDefault(string executable)
    {
        if (executable.Contains(Path.DirectorySeparatorChar)) { return null; }
        if (Environment.GetEnvironmentVariable("PATH") is not { } pathEnvironmentVariable) { return null; }
        
        var paths = pathEnvironmentVariable.Split(Path.PathSeparator);
        var pathExtensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            && Environment.GetEnvironmentVariable("PATHEXT") is { } pathTextEnvironmentVariable
            ? [.. pathTextEnvironmentVariable.Split(Path.PathSeparator), string.Empty]
            : new[] { string.Empty };

        return paths.SelectMany(path => pathExtensions.Select(pathExtension => Path.Combine(path, executable + pathExtension)))
                .FirstOrDefault(File.Exists);
    }
}
