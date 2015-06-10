using Xunit;
using Microsoft.Fx.CommandLine;
using System.Reflection;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace XunitRunner
{
    public class Program
    {
        private static string s_testAssemblyName;
        private static string s_testMethodName;
        private static bool s_runInParallel = false;

        private static List<string> s_failedTests = new List<string>();

        private static List<TestResult> s_testDurations = new List<TestResult>();

        private static object _lockObj = new object();

        public static void Main(string[] args)
        {
            try
            {
                Run(args);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected error encountered: " + e.ToString());
            }
        }

        private static void Run(string[] args)
        {
            string commandLine = "XunitRunner.exe " + string.Join(" ", args);
            if (!CommandLineParser.ParseForConsoleApplication((parser) =>
                {
                    parser.DefineQualifier("testassembly", ref s_testAssemblyName, "The name of the test assembly to load.");
                    parser.DefineOptionalQualifier("methodname", ref s_testMethodName, "The (full) name of an individual test method to run");
                    parser.DefineOptionalQualifier("parallel", ref s_runInParallel, "Specifies whether to run the tests in parallel or not.");
                },
                commandLine))
            {
                return;
            }

            AssemblyName assemblyName = new AssemblyName(s_testAssemblyName);
            Assembly a = Assembly.Load(new AssemblyName(s_testAssemblyName));

            MethodInfo[] testMethods = a.DefinedTypes.SelectMany(ti => ti.DeclaredMethods)
                .Where(mi => mi.IsDefined(typeof(FactAttribute)) && !mi.IsDefined(typeof(OuterLoopAttribute)))
                .ToArray();

            if (s_testMethodName != null)
            {
                testMethods = testMethods.Where(mi => mi.Name == s_testMethodName).ToArray();
                if (testMethods.Length != 1)
                {
                    Console.Error.WriteLine("testmethod must match exactly one method name.");
                    return;
                }
            }

            List<Task> testTasks = new List<Task>(testMethods.Length);

            Console.WriteLine("Executing " + testMethods.Length + " tests.");
            if (s_runInParallel)
            {
                foreach (MethodInfo testMethod in testMethods)
                {
                    testTasks.Add(
                        Task.Run(() =>
                        {
                            ExecuteTestMethod(testMethod);
                        }));
                }
                Task.WaitAll(testTasks.ToArray());
            }
            else
            {
                foreach (MethodInfo testMethod in testMethods)
                {
                    ExecuteTestMethod(testMethod);
                }
            }

            Console.WriteLine("Execution finished. Total failures: " + s_failedTests.Count);
            using (new ConsoleColorRegion(ConsoleColor.Red))
            {
                foreach (var testName in s_failedTests)
                {
                    Console.WriteLine(">>" + testName);
                }
            }

            Console.WriteLine("Test durations:");
            s_testDurations.Sort();
            using (new ConsoleColorRegion(ConsoleColor.Green))
            {
                foreach (var result in s_testDurations)
                {
                    Console.WriteLine(result.Name + " : " + result.Duration.ToString("#0.##"));
                }
            }
        }

        private static void ExecuteTestMethod(MethodInfo testMethod)
        {
            Console.WriteLine("[" + Thread.CurrentThread.ManagedThreadId + "] " + testMethod.Name);
            try
            {
                object instance = testMethod.IsStatic ? null : Activator.CreateInstance(testMethod.DeclaringType);
                var before = DateTime.UtcNow;
                testMethod.Invoke(instance, new object[0]);
                var totalDuration = (DateTime.UtcNow - before).TotalSeconds;
                lock (_lockObj)
                {
                    s_testDurations.Add(new TestResult(testMethod.Name, totalDuration));
                }
            }
            catch (Exception e)
            {
                using (new ConsoleColorRegion(ConsoleColor.Red))
                {
                    Console.WriteLine(">> FAIL: " + e.ToString());
                }

                s_failedTests.Add(testMethod.Name);
            }
        }

        private struct ConsoleColorRegion : IDisposable
        {
            private ConsoleColor _previousColor;

            public ConsoleColorRegion(ConsoleColor color)
            {
                _previousColor = Console.ForegroundColor;
                Console.ForegroundColor = color;
            }

            public void Dispose()
            {
                Console.ForegroundColor = _previousColor;
            }
        }

        private class TestResult : IComparable<TestResult>
        {
            public string Name { get; private set; }
            public double Duration { get; private set; }

            public TestResult(string name, double duration)
            {
                Name = name;
                Duration = duration;
            }

            public int CompareTo(TestResult other)
            {
                return Duration.CompareTo(other.Duration);
            }
        }
    }
}