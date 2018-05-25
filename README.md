# What
A simple command line parser with an Api ~stolen~ inspired from [Argu](https://github.com/fsprojects/Argu)

# Example

```C#
        class CLI_Args
        {
            public int Count { get; set; }
            public string File { get; set; }
            public Toggle Force { get; set; }
        }
```

## Parsing

```C#
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
```

## Unparsing

__Yes, this does correctly escape spaces / backslashes on windows, which is a PITA!__

```C#
        [Fact]
        public static void Unparse()
        {
            var parser = Cargu.ArgumentParser.Create<CLI_Args>();
            var unparser = parser.Unparse();
            var cmdLine = unparser.With(x => x.Count, 10).With(x => x.File, "y.pdf").With(x => x.Force).ToString();
            Assert.Equal("--count 10 --file y.pdf --force", cmdLine);
        }
```

# Why?

## Why not Argu?

I needed something for a (currently) C# only project.
Using Argu would require adding two DLLs (Argu and FSharp.Core), which is not too bad.

It would also require adding a F#-source project for the CLI parsing,
which would increase the complexity for a marginal gain
and require all Developers to install F# with VS.

## Why not _(any other cli parser)_?

Most (all for c# I could find) cli parser libs model the parameters as a class which gets initialized from the command line.

Simplified example: ``CLI_Args parsed = CliLib.Parse<CLI_Args>(args);``

At first glance this looks perfect and exactly like what you want, but:

* You don't know anymore whether the user actually specified something, or whether it was initialised to the default value (0 for int, false for bool, ...).
* Unparsing is awkward for the same reason.
* I'm think the interface of Argu is absolutely great. Using anything else feels like sakrileg.
