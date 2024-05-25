using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using NUnit.Framework;

namespace Medallion.Shell.Tests;

[NonParallelizable] // updates environment variables
public class SystemPathSearcherIntegrationTest
{
    [Test, Platform("Win", Reason = "Tests a Windows-specific executable")]
    public void TestPrioritizeExactMatchOnWindows()
    {
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        // Write to console because this can be annoying if the test is aborted without executing the revert in the finally block
        Console.WriteLine(originalPath);

        // Copy explorer.exe to a temp directory, where we have write access
        var tempDirectory = Path.GetTempPath();
        const string ExplorerExeFullPath = @"C:\Windows\explorer.exe";
        var newFilePath = Path.Combine(tempDirectory, Path.GetFileName(ExplorerExeFullPath));
        var newFilePathWithAdditionalExtension = $"{newFilePath}.exe";

        // Add the temp directory at the beginning of the path, so that it takes precedence.
        var temporaryPath = $"{tempDirectory}{Path.PathSeparator}{originalPath}";
        Environment.SetEnvironmentVariable("PATH", temporaryPath);

        try
        {
            File.Copy(ExplorerExeFullPath, newFilePath);
            File.Copy(ExplorerExeFullPath, newFilePathWithAdditionalExtension);

            SystemPathSearcher.GetFullPathUsingSystemPathOrDefault("explorer.exe")
                .ShouldEqual(newFilePath); // not newFilePathWithAdditionalExtension
        }
        finally
        {
            File.Delete(newFilePath);
            File.Delete(newFilePathWithAdditionalExtension);

            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }

#if !NETSTANDARD && !NETFRAMEWORK
    [Test, Platform("Unix", Reason = "Tests a Unix-specific executable")]
    public void TestExcludeFilesWithExecutableBitsUnset()
    {
        Debug.Assert(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "not on windows");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        // Write to console because this can be annoying if the test is aborted without executing the revert in the finally block
        Console.WriteLine(originalPath);

        // Create a file in a temp directory, where we have write access
        var tempDirectory = Path.GetTempPath();
        const string FileName = "hello.world";
        var newFilePath = Path.Combine(tempDirectory, FileName);

        // Add the temp directory to the PATH
        Environment.SetEnvironmentVariable("PATH", $"{tempDirectory}{Path.PathSeparator}{originalPath}");

        File.WriteAllText(newFilePath, $"#!/usr/bin/env bash{Environment.NewLine}echo hello world");
        try
        {
            // Not found because a new file created by .NET is not executable by default
            SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(FileName)
                .ShouldEqual(null);

            // Set the user-executable bit.
#if NET7_0_OR_GREATER
            var currentMode = File.GetUnixFileMode(newFilePath);
            File.SetUnixFileMode(newFilePath, currentMode | UnixFileMode.UserExecute);
#else
            Assert.That(chmod(newFilePath, 0x100 | 0x80 | 0x40 | 0x20 | 0x10 | 0x8 | 0x4 | 0x2 | 0x1), Is.EqualTo(0));
#endif

            // Now the file is user-executable, the path should be returned.
            SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(FileName)
                .ShouldEqual(newFilePath);
        }
        finally
        {
            File.Delete(newFilePath);
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
#if !NET7_0_OR_GREATER
    [DllImport("libc", SetLastError = true)]
#pragma warning disable SA1300 // Element should begin with upper-case letter
    private static extern int chmod(string pathname, int mode);
#pragma warning restore SA1300 // Element should begin with upper-case letter
#endif
#endif

    [Test, Platform("Win", Reason = "Tests a Windows-specific executable")]
    public void TestReturnNullWhenPathExtensionEnvironmentVariableIsEmptyOnWindows() => TestInvalidInput("PATHEXT", "where");

    [Test, Platform("Win", Reason = "Tests a Windows-specific executable")]

    public void TestReturnNullWhenPathEnvironmentVariableIsEmptyOnWindows() => TestInvalidInput("PATH", "where");

#if !NETSTANDARD && !NETFRAMEWORK
    [Test, Platform("Unix", Reason = "Tests a Unix-specific executable")]
    public void TestReturnNullWhenPathEnvironmentVariableIsEmptyOnUnix() => TestInvalidInput("PATH", "which");
#endif

    private static void TestInvalidInput(string environmentVariableName, string executable)
    {
        var originalEnvironmentVariable = Environment.GetEnvironmentVariable(environmentVariableName);
        // Write to console because this can be annoying if the test is aborted without executing the revert in the finally block
        Console.WriteLine(originalEnvironmentVariable);
        Environment.SetEnvironmentVariable(environmentVariableName, string.Empty);
        try
        {
            SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(executable)
                .ShouldEqual(null);
        }
        finally
        {
            Environment.SetEnvironmentVariable(environmentVariableName, originalEnvironmentVariable);
        }
    }
}
