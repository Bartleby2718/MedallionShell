using System;
using System.Linq.Expressions;
using System.Threading.Tasks;
using NUnit.Framework;
using SampleCommand;

namespace Medallion.Shell.Tests
{
    public class PlatformCompatibilityTest
    {
        [Test]
        public Task TestReadAfterExit() => RunTestAsync(() => PlatformCompatibilityTests.TestReadAfterExit());

        // TODO: fix in https://github.com/madelson/MedallionShell/issues/117
        //[Test]
        //public Task TestWriteAfterExit() => RunTestAsync(() => PlatformCompatibilityTests.TestWriteAfterExit());

        [Test]
        public Task TestFlushAfterExit() => RunTestAsync(() => PlatformCompatibilityTests.TestFlushAfterExit());

        [Test]
        public Task TestExitWithMinusOne() => RunTestAsync(() => PlatformCompatibilityTests.TestExitWithMinusOne());

        [Test]
        public Task TestExitWithOne() => RunTestAsync(() => PlatformCompatibilityTests.TestExitWithOne());

        [Test]
        public Task TestBadProcessFile() => RunTestAsync(() => PlatformCompatibilityTests.TestBadProcessFile());

        [Test]
        public Task TestAttaching() => RunTestAsync(() => PlatformCompatibilityTests.TestAttaching());

        [Test]
        public Task TestWriteToStandardInput() => RunTestAsync(() => PlatformCompatibilityTests.TestWriteToStandardInput());

        [Test]
        public Task TestArgumentsRoundTrip() => RunTestAsync(() => PlatformCompatibilityTests.TestArgumentsRoundTrip());

        [Test]
        public Task TestKill() => RunTestAsync(() => PlatformCompatibilityTests.TestKill());

        private static async Task RunTestAsync(Expression<Action> testMethod)
        {
            var compiled = testMethod.Compile();
            Assert.DoesNotThrow(() => compiled(), "should run on current platform");

            await Task.CompletedTask; // make the compiler happy
        }
    }
}
