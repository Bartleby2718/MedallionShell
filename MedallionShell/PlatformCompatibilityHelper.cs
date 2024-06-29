using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Medallion.Shell
{
    internal static class PlatformCompatibilityHelper
    {
        public static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        /// <summary>
        /// See https://github.com/dotnet/runtime/issues/81896 and
        /// https://github.com/madelson/MedallionShell/issues/94
        /// </summary>
        public static bool ProcessStreamsUseSyncIO => IsWindows;

        public static bool ProcessStreamWriteThrowsOnProcessEnd => !IsWindows;

        public static readonly CommandLineSyntax DefaultCommandLineSyntax = new WindowsCommandLineSyntax();

        /// <summary>
        /// Starts the given <paramref name="process"/> and captures the standard IO streams.
        /// </summary>
        public static bool SafeStart(this Process process, out StreamWriter? standardInput, out StreamReader? standardOutput, out StreamReader? standardError)
        {
            var redirectStandardInput = process.StartInfo.RedirectStandardInput;
            var redirectStandardOutput = process.StartInfo.RedirectStandardOutput;
            var redirectStandardError = process.StartInfo.RedirectStandardError;

            try
            {
                process.Start();

                // adding this code allows for a sort-of replication of
                // https://github.com/madelson/MedallionShell/issues/22 on non-Android platforms
                // process.StandardInput.BaseStream.Write(new byte[1000], 0, 1000);
                // process.StandardInput.BaseStream.Flush();
            }
            catch
            {
                standardInput = redirectStandardInput ? new StreamWriter(Stream.Null, Console.InputEncoding) { AutoFlush = true } : null;
                standardOutput = redirectStandardOutput ? new StreamReader(Stream.Null, Console.OutputEncoding) : null;
                standardError = redirectStandardError ? new StreamReader(Stream.Null, Console.OutputEncoding) : null;
                return false;
            }

            standardInput = redirectStandardInput ? process.StandardInput : null;
            standardOutput = redirectStandardOutput ? process.StandardOutput : null;
            standardError = redirectStandardError ? process.StandardError : null;
            return true;
        }
    }
}
