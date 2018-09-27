using Expecto;
using Expecto.CSharp;
using ExpressionToCodeLib;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;
using Xunit;

namespace Cargu.Tests
{
    public class Tests
    {
        public static int Main(string[] args)
        {
            var tests = Runner.TestList("TestClass", DiscoverTestMethods<Tests>());
            var config =
                Impl.ExpectoConfig.defaultConfig
                .AddJUnitSummary("testresults.junit.xml", "Cargu.Tests");
            return Runner.RunTestsWithArgs(config, args, tests);
        }



        private static IEnumerable<Test> DiscoverTestMethods<T>()
        {
            var t = typeof(T);
            foreach (var m in t.GetMethods())
            {
                var isTaskReturning = typeof(Task).IsAssignableFrom(m.ReturnType);
                if (m.GetCustomAttribute<FTestsAttribute>() != null)
                {
                    if (isTaskReturning)
                        yield return Runner.FocusedTestCase(m.Name, () => (Task)m.Invoke(null, Array.Empty<object>()));
                    else
                        yield return Runner.FocusedTestCase(m.Name, () => m.Invoke(null, Array.Empty<object>()));
                }
                else if (m.GetCustomAttribute<PTestsAttribute>() != null)
                {
                    if (isTaskReturning)
                        yield return Runner.PendingTestCase(m.Name, () => (Task)m.Invoke(null, Array.Empty<object>()));
                    else
                        yield return Runner.PendingTestCase(m.Name, () => m.Invoke(null, Array.Empty<object>()));
                }
                else if (m.GetCustomAttribute<TestsAttribute>() != null)
                {
                    if (isTaskReturning)
                        yield return Runner.TestCase(m.Name, () => (Task)m.Invoke(null, Array.Empty<object>()));
                    else
                        yield return Runner.TestCase(m.Name, () => m.Invoke(null, Array.Empty<object>()));
                }
            }
        }

        class CLI_Args
        {
            public int Count { get; set; }
            public string File { get; set; }
            [Description("Hello Description!")]
            public Toggle Force { get; set; }
        }

        [Tests]
        public static void Parse()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var result = parser.Parse(new[] { "--count", "5", "--file", "c:\\x.txt", "--force" }, parseAppConfig: false);

            var count = result.GetResult(x => x.Count);
            var isOk = result.TryGetResult(x => x.File, out var file);
            PAssert.That(() => isOk);
            var forceEnabled = result.Contains(x => x.Force);

            PAssert.That(() => 5 == count);
            PAssert.That(() => "c:\\x.txt" == file);
        }

        [Tests]
        public static void Parse_Wrong_ShouldThrow()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var exn = Assert.Throws<CommandLineHelpException>(() => parser.Parse(new[] { "--countx", "5", "--file", "c:\\x.txt", "--force" }, parseAppConfig: false));

            const string expected = @"USAGE: Cargu.Tests [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force Hello Description!
";
            PAssert.That(() => expected == exn.Message);
        }

        [Tests]
        public static void Parse_Help_ShouldThrow()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var exn = Assert.Throws<CommandLineHelpException>(() => parser.Parse(new[] { "--help" }, parseAppConfig: false));

            const string expected = @"USAGE: Cargu.Tests [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force Hello Description!
";
            PAssert.That(() => expected == exn.Message);
        }

        [Tests]
        public static void Unparse()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var unparser = parser.Unparse();
            var cmdLine = unparser.With(x => x.Count, 10).With(x => x.File, "y.pdf").With(x => x.Force).ToString();
            PAssert.That(() => "--count 10 --file y.pdf --force" == cmdLine);
        }

        [Tests]
        public static void Unparse_Space()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var cmdLine = parser.Unparse()
                                .With(x => x.Count, 10)
                                .With(x => x.File, "x y.pdf")
                                .With(x => x.Force)
                                .ToString();
            PAssert.That(() => "--count 10 --file \"x y.pdf\" --force" == cmdLine);
        }

        [Tests]
        public static void Unparse_Backslash()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var cmdLine = parser.Unparse()
                                .With(x => x.Count, 10)
                                .With(x => x.File, "c:\\x.txt")
                                .With(x => x.Force)
                                .ToString();
            PAssert.That(() => "--count 10 --file \"c:\\x.txt\" --force" == cmdLine);
        }

        [Tests]
        public static void Help()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var usage = parser.PrintUsage();
            var expectedUsage = @"USAGE: Cargu.Tests [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force Hello Description!
";
            PAssert.That(() => expectedUsage.Trim() == usage.Trim());
        }

        [Tests]
        public static void Help_WithAppName()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>("a.exe");
            var usage = parser.PrintUsage();
            var expectedUsage = @"USAGE: a.exe [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force Hello Description!
";
            PAssert.That(() => expectedUsage.Trim() == usage.Trim());
        }
    }
}
