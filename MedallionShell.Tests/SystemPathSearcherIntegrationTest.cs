﻿using System;
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

#if NET8_0
    [Test, Platform("Unix", Reason = "Tests a Unix-specific executable")]
    public void TestExcludeFilesWithExecutableBitsUnset()
    {
        Debug.Assert(!RuntimeInformation.IsOSPlatform(OSPlatform.Windows), "not on windows");
        var originalPath = Environment.GetEnvironmentVariable("PATH");
        // Write to console because this can be annoying if the test is aborted without executing the revert in the finally block
        Console.WriteLine(originalPath);

        // Copy executable to a temp directory, where we have write access
        var tempDirectory = Path.Combine(Path.GetTempPath(), "newPath");
        Directory.CreateDirectory(tempDirectory);
        const string WhichExecutableFullPath = "/usr/bin/which";
        var newFilePath = Path.Combine(tempDirectory, Path.GetFileName(WhichExecutableFullPath));

        // Add the temp directory at the beginning of the path, so that it takes precedence.
        var temporaryPath = $"{tempDirectory}{Path.PathSeparator}{originalPath}";
        Environment.SetEnvironmentVariable("PATH", temporaryPath);

        var temporaryWhichExecutableFullPath = Path.Combine(tempDirectory, Path.GetFileName(WhichExecutableFullPath));
        try
        {
            // temporarily move to a non-path directory
            File.Move(WhichExecutableFullPath, temporaryWhichExecutableFullPath);
            File.Copy(WhichExecutableFullPath, newFilePath);
            File.SetAttributes(newFilePath, FileAttributes.ReadOnly);
            var currentMode = File.GetUnixFileMode(newFilePath);
            File.SetUnixFileMode(newFilePath, currentMode & ~(UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute));
            SystemPathSearcher.GetFullPathUsingSystemPathOrDefault("which")
                .ShouldEqual(null);
        }
        finally
        {
            File.Delete(newFilePath);
            File.Move(WhichExecutableFullPath, temporaryWhichExecutableFullPath);
            Environment.SetEnvironmentVariable("PATH", originalPath);
        }
    }
#endif

    [Test, Platform("Win", Reason = "Tests a Windows-specific executable")]
    public void TestReturnNullWhenPathExtensionEnvironmentVariableIsEmptyOnWindows() => TestInvalidInput("PATHEXT", "where");

    [Test, Platform("Win", Reason = "Tests a Windows-specific executable")]

    public void TestReturnNullWhenPathEnvironmentVariableIsEmptyOnWindows() => TestInvalidInput("PATH", "where");

    [Test, Platform("Unix", Reason = "Tests a Unix-specific executable")]
    public void TestReturnNullWhenPathEnvironmentVariableIsEmptyOnUnix() => TestInvalidInput("PATH", "which");

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
