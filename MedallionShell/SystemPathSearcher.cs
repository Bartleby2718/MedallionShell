using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace Medallion.Shell;

internal static class SystemPathSearcher
{
    /// <summary>
    /// Given a file name, checks for all directories in the PATH environment variable and returns the fully
    /// qualified path of the first executable found, like the default shell would do on each OS.
    /// </summary>
    public static string? GetFullPathUsingSystemPathOrDefault(string fileName)
    {
        // In all OSes, a file name cannot contain the directory separator
        // https://stackoverflow.com/a/31976060
        if (fileName.Contains(Path.DirectorySeparatorChar)) { return null; }

        // Fail gracefully if the PATH environment variable is not set for this process.
        if (Environment.GetEnvironmentVariable("PATH") is not { } pathEnvironmentVariable) { return null; }

        // On Windows, check the PATHEXT environment variable to see which extensions are considered executable
        // https://superuser.com/q/228680
        var isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        var pathExtensions = isWindows
            && Environment.GetEnvironmentVariable("PATHEXT") is { } pathTextEnvironmentVariable
            // It's intentional that the empty string comes first because if a) you run myfile.exe and
            // b) both myfile.exe and myfile.exe.exe exist in the same directory on the system path,
            // myfile.exe (i.e. the file with the exact match) is executed (empirically).
            ? [string.Empty, .. pathTextEnvironmentVariable.Split(Path.PathSeparator)]
            // Unix-like systems don't use the extension for this purpose, so don't check for additional extensions.
            : new[] { string.Empty };

        // Iterate over each directory in the PATH environment variable, checking for the first executable file.
        var paths = pathEnvironmentVariable.Split(Path.PathSeparator);
        return paths.SelectMany(path => pathExtensions.Select(pathExtension => Path.Combine(path, fileName + pathExtension)))
            // On Windows, just check for the exitence (https://stackoverflow.com/q/1653472)
            // On Unix-like systems, also check if the file has the executable permissions (https://unix.stackexchange.com/a/332954)
            .FirstOrDefault(p => File.Exists(p) && (isWindows || IsFileExecutableOnUnix(p)));
    }

    private static bool IsFileExecutableOnUnix(string path)
    {
        Debug.Assert(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "shouldn't be called on Windows");
#if NET7_0_OR_GREATER // Use the built-in API in .NET 7.
        var mode = File.GetUnixFileMode(path) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute);
        return mode != UnixFileMode.None;
#elif NETSTANDARD || NETFRAMEWORK // Fail gracefully because neither the method above nor the method below works on UWP (and .NET Framework).
        return false;
#else // Call the stat function using P/Invoke on .NET Core before .NET 7 (https://learn.microsoft.com/en-us/dotnet/standard/native-interop/pinvoke)
        const int S_IXUSR = 0x0040; // User has execute permission
        const int S_IXGRP = 0x0008; // Group has execute permission
        const int S_IXOTH = 0x0001; // Others have execute permission
        return stat(path, out var statBuffer) == 0
            && ((statBuffer.st_mode & S_IXUSR) != 0
                || (statBuffer.st_mode & S_IXGRP) != 0
                || (statBuffer.st_mode & S_IXOTH) != 0);
    }

    // https://en.wikipedia.org/wiki/Stat_%28system_call%29
    [DllImport("libc", SetLastError = true, CharSet = CharSet.Unicode)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
    private static extern int stat(string pathname, out StatBuffer statBuffer);
#pragma warning restore SA1300 // Element should begin with upper-case letter

    [StructLayout(LayoutKind.Sequential)]
    private struct StatBuffer
    {
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
#pragma warning disable SA1310 // Field names should not contain underscore
        public int st_dev;
        public short st_ino;
        public short st_mode;
        public short st_nlink;
        public short st_uid;
        public short st_gid;
        public int st_rdev;
        public int st_size;
        public int st_atime;
        public int st_mtime;
        public int st_ctime;
#pragma warning restore SA1310 // Field names should not contain underscore
#pragma warning restore SA1307 // Accessible fields should begin with upper-case letter
#endif
    }
}
