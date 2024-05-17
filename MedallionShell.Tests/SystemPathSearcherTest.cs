using System;
#if NETFRAMEWORK
using System.Text.RegularExpressions;
#endif
using NUnit.Framework;

namespace Medallion.Shell.Tests;

public class SystemPathSearcherTest
{
    [Platform("Win", Reason = "Tests Windows-specific executables")]
    #region worked before adding system path support
    [TestCase("dotnet", @"C:\Program Files\dotnet\dotnet.exe")]
    [TestCase("dotnet.exe", @"C:\Program Files\dotnet\dotnet.exe")]
    [TestCase("where", @"C:\Windows\System32\where.exe")]
    [TestCase("where.exe", @"C:\Windows\System32\where.exe")]
    [TestCase("cmd", @"C:\Windows\System32\cmd.exe")]
    [TestCase("cmd.exe", @"C:\Windows\System32\cmd.exe")]
    [TestCase("powershell", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [TestCase("powershell.exe", @"C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe")]
    [TestCase("explorer", @"C:\Windows\explorer.exe")]
    [TestCase("explorer.exe", @"C:\Windows\explorer.exe")]
    [TestCase("git", @"C:\Program Files\Git\cmd\git.exe")]
    [TestCase("git.exe", @"C:\Program Files\Git\cmd\git.exe")]
    #endregion
    #region didn't work before adding system path support
    [TestCase("osk", @"C:\Windows\System32\osk.exe")]
    [TestCase("osk.exe", @"C:\Windows\System32\osk.exe")]
    [TestCase("compmgmt", @"C:\Windows\System32\compmgmt.msc")]
    [TestCase("compmgmt.msc", @"C:\Windows\System32\compmgmt.msc")]
    [TestCase("regedit", @"C:\Windows\regedit.exe")]
    [TestCase("regedit.exe", @"C:\Windows\regedit.exe")]
    #endregion
    [TestCase("does.not.exist", null)]
    // echo is not a program on Windows but an internal command in cmd.exe or powershell.exe.
    // However, things like git may still install echo (e.g. C:\Program Files\Git\usr\bin\echo.EXE)
    // so there's no guarantee for echo on Windows.
    public void TestGetFullPathOnWindows(string executable, string? expected)
    {
        StringAssert.AreEqualIgnoringCase(expected, SystemPathSearcher.GetFullPathUsingSystemPathOrDefault(executable));

        var command = Command.Run("where", executable);
        var standardOutput = command.StandardOutput.ReadToEnd().Trim();
        if (expected == null)
        {
            standardOutput.ShouldEqual(
                string.Empty,
                $"Exit code: {command.Result.ExitCode}, StdErr: '{command.Result.StandardError}'");
        }
        else
        {
            Assert.That(
#if NETFRAMEWORK
                Regex.Split(standardOutput, Regex.Escape(Environment.NewLine)),
#else
                standardOutput.Split(Environment.NewLine),
#endif
                Does.Contain(expected));
        }
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