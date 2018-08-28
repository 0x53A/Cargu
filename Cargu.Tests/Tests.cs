using System;
using Xunit;

namespace Cargu.Tests
{
    public class Tests
    {
        class CLI_Args
        {
            public int Count { get; set; }
            public string File { get; set; }
            public Toggle Force { get; set; }
        }

        [Fact]
        public static void Parse()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var result = parser.Parse(new[] { "--count", "5", "--file", "c:\\x.txt", "--force" }, parseAppConfig: false);

            var count = result.GetResult(x => x.Count);
            if (!result.TryGetResult(x => x.File, out var file))
                Assert.False(true);
            var forceEnabled = result.Contains(x => x.Force);

            Assert.Equal(5, count);
            Assert.Equal("c:\\x.txt", file);
        }

        [Fact]
        public static void Unparse()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var unparser = parser.Unparse();
            var cmdLine = unparser.With(x => x.Count, 10).With(x => x.File, "y.pdf").With(x => x.Force).ToString();
            Assert.Equal("--count 10 --file y.pdf --force", cmdLine);
        }

        [Fact]
        public static void Unparse_Space()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var cmdLine = parser.Unparse()
                                .With(x => x.Count, 10)
                                .With(x => x.File, "x y.pdf")
                                .With(x => x.Force)
                                .ToString();
            Assert.Equal("--count 10 --file \"x y.pdf\" --force", cmdLine);
        }

        [Fact]
        public static void Unparse_Backslash()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var cmdLine = parser.Unparse()
                                .With(x => x.Count, 10)
                                .With(x => x.File, "c:\\x.txt")
                                .With(x => x.Force)
                                .ToString();
            Assert.Equal("--count 10 --file \"c:\\x.txt\" --force", cmdLine);
        }

        [Fact]
        public static void Help()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var usage = parser.PrintUsage();
            var expectedUsage = @"USAGE: testhost.x86.exe [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force 
";
            Assert.Equal(expectedUsage.Trim(), usage.Trim());
        }

        [Fact]
        public static void Help_WithAppName()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>("a.exe");
            var usage = parser.PrintUsage();
            var expectedUsage = @"USAGE: a.exe [--count <int>] [--file <string>] [--force]

OPTIONS:
    --count 
    --file  
    --force 
";
            Assert.Equal(expectedUsage.Trim(), usage.Trim());
        }
    }
}
