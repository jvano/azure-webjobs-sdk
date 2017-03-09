﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using Microsoft.Azure.WebJobs.Host.Diagnostics;
using System;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.Azure.WebJobs.Host.UnitTests
{
    public class ExceptionFormatterTests
    {
        [Fact]
        public void FormatException_WithAsyncMethods_ReturnsExpectedStackTrace()
        {
            string expectedTracePattern = @"System\.AggregateException : One or more errors occurred\. ---> Crash!
   at System\.Threading\.Tasks\.Task\.ThrowIfExceptional\(Boolean includeTaskCanceledExceptions\)
   at System\.Threading\.Tasks\.Task\.Wait\(Int32 millisecondsTimeout,CancellationToken cancellationToken\)
   at System\.Threading\.Tasks\.Task\.Wait\(\)
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run\(String arg\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.FormatException_WithAsyncMethods_ReturnsExpectedStackTrace\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?
---> \(Inner Exception #0\) System\.Exception : Crash!
   at async Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.CrashAsync\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*
   at async Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run2Async\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*
   at async Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run1Async\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*<---";

            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);

                Assert.Matches(expectedTracePattern, formattedException);
            }
        }

        [Fact]
        public void FormatException_WithNonAsyncMethods_ReturnsExpectedStackTrace()
        {
            string expectedTracePattern = @"System\.Exception : Crash! ---> System\.Exception : Sync crash!
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run1\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run\(String arg\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*? 
   End of inner exception
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.TestClass\.Run\(String arg\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?
   at Microsoft\.Azure\.WebJobs\.Host\.UnitTests\.ExceptionFormatterTests\.FormatException_WithNonAsyncMethods_ReturnsExpectedStackTrace\(\) at .*?\\test\\Microsoft\.Azure\.WebJobs\.Host\.UnitTests\\ExceptionFormatterTests\.cs : \d*?";

            try
            {
                var test = new TestClass();
                test.Run("Test3");
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);

                Assert.Matches(expectedTracePattern, formattedException);
            }
        }

        [Fact]
        public void FormatException_RemovesAsyncFrames()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);

                Assert.DoesNotMatch("d__.\\.MoveNext()", formattedException);
                Assert.DoesNotContain("TaskAwaiter", formattedException);
            }
        }

        [Fact]
        public void FormatException_ResolvesAsyncMethodNames()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);

                string typeName = $"{typeof(TestClass).DeclaringType.FullName}.{ nameof(TestClass)}";
                Assert.Contains($"async {typeName}.{nameof(TestClass.Run1Async)}()", formattedException);
                Assert.Contains($"async {typeName}.{nameof(TestClass.Run2Async)}()", formattedException);
                Assert.Contains($"async {typeName}.{nameof(TestClass.CrashAsync)}()", formattedException);
            }
        }

        [Fact]
        public void FormatException_OutputsMethodParameters()
        {
            try
            {
                var test = new TestClass();
                test.Run();
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);
                
                Assert.Contains($"{nameof(TestClass.Run)}(String arg)", formattedException);
            }
        }

        [Fact]
        public void FormatException_OutputsExpectedAsyncMethodParameters()
        {
            try
            {
                var test = new TestClass();
                test.Run("Test2");
            }
            catch (Exception exc)
            {
                string formattedException = ExceptionFormatter.GetFormattedException(exc);

                Assert.Contains($"{nameof(TestClass.Run4Async)}(String arg)", formattedException);

                // When unable to resolve, the '??' token is used
                
                Assert.Contains($"{nameof(TestClass.Run5Async)}(??)", formattedException);
            }
        }

        private class TestClass
        {
            public void Run()
            {
                Run("Test1");
            }

            public void Run(string arg)
            {
                if (string.Equals(arg, "Test1"))
                {
                    Run1Async().Wait();
                }
                else if (string.Equals(arg, "Test2"))
                {
                    Run4Async("Arg").Wait();
                }
                else if (string.Equals(arg, "Test3"))
                {
                    try
                    {
                        Run1();
                    }
                    catch (Exception exc)
                    {
                        // Test with inner exception
                        throw new Exception("Crash!", exc);
                    }
                }
            }

            private void Run1()
            {
                throw new Exception("Sync crash!");
            }

            public async Task Run1Async()
            {
                await Run2Async();
            }

            public async Task Run2Async()
            {
                await CrashAsync();
            }

            public async Task CrashAsync()
            {
                await Task.Yield();
                throw new Exception("Crash!");
            }

            public async Task Run4Async(string arg)
            {
                await Run5Async();
            }

            public async Task Run5Async()
            {
                await CrashAsync();
            }

            public async Task Run5Async(string arg)
            {
                await CrashAsync();
            }
        }
    }
}