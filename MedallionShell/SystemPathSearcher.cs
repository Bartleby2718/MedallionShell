using System;
using System.Diagnostics;
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

        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var paths = pathEnvironmentVariable.Split(Path.PathSeparator);
        var pathExtensions = isWindows
            && Environment.GetEnvironmentVariable("PATHEXT") is { } pathTextEnvironmentVariable
            ? [.. pathTextEnvironmentVariable.Split(Path.PathSeparator), string.Empty]
            : new[] { string.Empty };

        return paths.SelectMany(path => pathExtensions.Select(pathExtension => Path.Combine(path, executable + pathExtension)))
                .FirstOrDefault(p => File.Exists(p) && (isWindows || IsFileExecutableOnUnix(p)));
    }

    private static bool IsFileExecutableOnUnix(string path)
    {
        Debug.Assert(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "shouldn't be called on Windows");
#if NETFRAMEWORK || NETSTANDARD
        return false;
#elif NET7_0_OR_GREATER
        var mode = File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        return mode != UnixFileMode.None;
#else
        // User has execute permission
        const int S_IXUSR = 0x40;
        return stat(path, out var statBuffer) == 0 && (statBuffer.st_mode & S_IXUSR) != 0;
#endif
    }

#if !NET7_0_OR_GREATER && !NETSTANDARD
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
    private static extern int stat(string pathname, out StatBuffer statBuffer);
#pragma warning restore SA1300 // Element should begin with upper-case letter

    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
        // ID of device containing file
        public uint st_dev;
        // Inode number
        public uint st_ino;
        // File type and mode
        public uint st_mode;
        // ignore other fields
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
    }
#endif
}
