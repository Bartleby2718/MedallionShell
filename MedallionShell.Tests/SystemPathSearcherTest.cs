using NUnit.Framework;

namespace Medallion.Shell.Tests;

public class SystemPathSearcherTest
{
    [Platform("Win", Reason = "Tests Windows-specific executables")]
    [TestCase("dotnet", @"C:\Program Files\dotnet\dotnet.exe")]
    [TestCase("dotnet.exe", @"C:\Program Files\dotnet\dotnet.exe")]
    [TestCase("where.exe", @"C:\Windows\System32\where.exe")]
    [TestCase("cmd", @"C:\Windows\System32\cmd.exe")]
    [TestCase("cmd.exe", @"C:\Windows\System32\cmd.exe")]
    [TestCase("powershell.exe", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [TestCase("explorer.exe", @"C:\Windows\explorer.exe")]
    [TestCase("git.exe", @"C:\Program Files\Git\cmd\git.exe")]
    [TestCase("does.not.exist", null)]
    // echo is not a program on Windows but an internal command in cmd.exe or powershell.exe.
    // However, things like git may still install echo (e.g. C:\Program Files\Git\usr\bin\echo.EXE)
    // so there's no guarantee for echo on Windows.
    public void TestGetFullPathOnWindows(string executable, string? expected)
    {
        StringAssert.AreEqualIgnoringCase(expected, SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(executable));

        var command = Command.Run("where", executable);
        command.StandardOutput.ReadToEnd().Trim().ShouldEqual(
            expected ?? string.Empty,
            $"Exit code: {command.Result.ExitCode}, StdErr: '{command.Result.StandardError}'");
    }

    [Platform("Unix", Reason = "Tests Unix-specific executables")]
    [TestCase("dotnet", "/usr/bin/dotnet")]
    [TestCase("which", "/usr/bin/which")]
    [TestCase("head", "/usr/bin/head")]
    [TestCase("sh", "/bin/sh")]
    [TestCase("ls", "/bin/ls")]
    [TestCase("grep", "/bin/grep")]
    [TestCase("sleep", "/bin/sleep")]
    [TestCase("echo", "/bin/echo")]
    [TestCase("does.not.exist", null)]
    public void TestGetFullPathOnLinux(string executable, string? expected)
    {
        SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(executable).ShouldEqual(expected);

        var command = Command.Run("which", executable);
        command.StandardOutput.ReadToEnd().Trim().ShouldEqual(
            expected ?? string.Empty,
            $"Exit code: {command.Result.ExitCode}, StdErr: '{command.Result.StandardError}'");
    }
}