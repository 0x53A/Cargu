using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace Cargu
{
    // ------------------------------------------------------------------------------------
    // Exceptions
    // ------------------------------------------------------------------------------------

    public class CommandLineHelpException : Exception
    {
        internal CommandLineHelpException(string message) : base(message)
        {

        }
    }

    public class ArgumentNotFoundException : Exception
    {
    }

    public class RequiredArgumentNotSuppliedException : Exception
    {
    }


    // ------------------------------------------------------------------------------------
    // Attributes
    // ------------------------------------------------------------------------------------

    /// <summary>
    /// Demands at least one parsed result for this argument; a parse exception is raised otherwise.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class MandatoryAttribute : Attribute
    {
    }

    /// <summary>
    /// Demands at least one parsed result for this argument; a parse exception is raised otherwise.
    /// </summary>
    //[AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    //public class DescriptionAttribute : Attribute
    //{
    //    DescriptionAttribute
    //}


    // ------------------------------------------------------------------------------------
    // Types
    // ------------------------------------------------------------------------------------

    public class Toggle
    {
        private Toggle() { }
    }


    // ------------------------------------------------------------------------------------
    // Interfaces
    // ------------------------------------------------------------------------------------
    public interface IArgumentParser<TTemplate>
    {
        IParseResults<TTemplate> Parse(string[] cliArgs, bool parseAppConfig);
        IUnparser<TTemplate> Unparse();
        string PrintUsage();
    }

    public interface IParseResults<TTemplate>
    {
        bool Contains<TResult>(Expression<Func<TTemplate, TResult>> expr);
        TResult[] GetResults<TResult>(Expression<Func<TTemplate, TResult>> expr);
        TResult GetResult<TResult>(Expression<Func<TTemplate, TResult>> expr);
        bool TryGetResult<TResult>(Expression<Func<TTemplate, TResult>> expr, out TResult value);
    }

    public interface IUnparser<TTemplate>
    {
        IUnparser<TTemplate> With<TValue>(Expression<Func<TTemplate, TValue>> expr, TValue value);
        IUnparser<TTemplate> With(Expression<Func<TTemplate, Toggle>> expr);
        string ToWindowsCommandLine();
        string ToUnixCommandLine();
        string ToString();
    }


    // ------------------------------------------------------------------------------------
    // Implementations
    // ------------------------------------------------------------------------------------

    public static class ArgumentParser
    {
        public static IArgumentParser<TTemplate> Create<TTemplate>(string appName = null)
        {
            return new ArgumentParser<TTemplate>(appName);
        }
    }

    internal class ArgumentParser<TTemplate> : IArgumentParser<TTemplate>
    {
        private readonly string _appName;

        public ArgumentParser(string appName)
        {
            _appName = appName;
        }

        IParseResults<TTemplate> IArgumentParser<TTemplate>.Parse(string[] cliArgs, bool parseAppConfig)
        {
            if (parseAppConfig)
                throw new NotImplementedException("app.config parser is not yet supported!");

            var model = TemplateAnalyzer<TTemplate>.Instance;
            HashSet<AnalyzedProperty> mandatoryProps = new HashSet<AnalyzedProperty>();
            foreach (var p in model.Properties.Values)
                if (p.IsMandatory) mandatoryProps.Add(p);

            AnalyzedProperty state = null;
            LoookUp<PropertyInfo, object> result = new LoookUp<PropertyInfo, object>();
            for (int i = 0; i < cliArgs.Length; i++)
            {
                var current = cliArgs[i];
                if (state == null)
                {
                    var prop = model.Properties.Values.SingleOrDefault(p => p.AllCliArgs.Contains(current));
                    if (prop == null)
                        throw new CommandLineHelpException((this as IArgumentParser<TTemplate>).PrintUsage());
                    if (prop.IsMandatory) mandatoryProps.Remove(prop);

                    if (prop.Type == typeof(Toggle))
                        result.Add(prop.PropertyInfo, null);
                    else
                    {
                        state = prop;
                    }
                }
                else
                {
                    var val = state.Parse(current);
                    result.Add(state.PropertyInfo, val);
                    state = null;
                }
            }

            if (state != null)
                throw new Exception($"expected value for '{state.DefaultCliArg}'!");

            if (mandatoryProps.Count != 0)
                throw new Exception($"Missing mandatory props: '{string.Join(", ", mandatoryProps)}'!");


            return new ParseResults<TTemplate>(result.ToResult());
        }

        string IArgumentParser<TTemplate>.PrintUsage()
        {
            string appName = _appName ?? (Assembly.GetEntryAssembly()?.FullName ?? Process.GetCurrentProcess().MainModule.ModuleName);
            
            var model = TemplateAnalyzer<TTemplate>.Instance;
            var sb = new StringBuilder();

            string AnalyzedPropertyToString(AnalyzedProperty p)
            {
                var sbp = new StringBuilder();
                if (false == p.IsMandatory)
                    sbp.Append("[");

                sbp.Append(p.DefaultCliArg);

                if (p.Type != typeof(Toggle))
                {
                    // todo: tuple types
                    sbp.Append(" ");
                    sbp.Append(Utils.TypeToTypeHelp(p.Type));
                }

                if (false == p.IsMandatory)
                    sbp.Append("]");
                return sbp.ToString();
            }

            sb.AppendLine($"USAGE: {appName} {string.Join(" ", model.Properties.Values.Select(AnalyzedPropertyToString))}");
            sb.AppendLine();
            sb.AppendLine("OPTIONS:");
            var maxValueLength = model.Properties.Values.Max(p => p.DefaultCliArg.Length);
            foreach (var p in model.Properties.Values)
            {
                sb.Append($"    {p.DefaultCliArg} ");
                for (int i = 0; i < (maxValueLength - p.DefaultCliArg.Length); i++)
                    sb.Append(" ");
                // TODO: values
                if (p.Description != null)
                    sb.Append(p.Description);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        IUnparser<TTemplate> IArgumentParser<TTemplate>.Unparse()
        {
            return new Unparser<TTemplate>();
        }
    }

    internal class Unparser<TTemplate> : IUnparser<TTemplate>
    {
        private List<string> _tokens = new List<string>();

        string IUnparser<TTemplate>.ToString()
        {
            return (this as IUnparser<TTemplate>).ToWindowsCommandLine();
        }

        string IUnparser<TTemplate>.ToUnixCommandLine()
        {
            throw new NotImplementedException();
        }

        string IUnparser<TTemplate>.ToWindowsCommandLine()
        {
            return Utils.FlattenCliTokens(_tokens);
        }

        IUnparser<TTemplate> IUnparser<TTemplate>.With<TValue>(Expression<Func<TTemplate, TValue>> expr, TValue value)
        {
            var prop = Utils.GetPropFromExpression(expr);

            var key = TemplateAnalyzer<TTemplate>.Instance.Properties[prop].DefaultCliArg;
            var val = TemplateAnalyzer<TTemplate>.Instance.Properties[prop].Unparse(value);
            _tokens.Add(key);
            _tokens.Add(val);

            return this;
        }

        IUnparser<TTemplate> IUnparser<TTemplate>.With(Expression<Func<TTemplate, Toggle>> expr)
        {
            var prop = Utils.GetPropFromExpression(expr);

            var toggle = TemplateAnalyzer<TTemplate>.Instance.Properties[prop].DefaultCliArg;
            _tokens.Add(toggle);

            return this;
        }
    }

    internal class ParseResults<TTemplate> : IParseResults<TTemplate>
    {
        private Dictionary<PropertyInfo, object[]> _values;

        public ParseResults(Dictionary<PropertyInfo, object[]> values)
        {
            _values = values;
        }

        bool IParseResults<TTemplate>.Contains<TResult>(Expression<Func<TTemplate, TResult>> expr)
        {
            var p = Utils.GetPropFromExpression(expr);
            return _values.ContainsKey(p);
        }

        TResult IParseResults<TTemplate>.GetResult<TResult>(Expression<Func<TTemplate, TResult>> expr)
        {
            var p = Utils.GetPropFromExpression(expr);
            object[] values;
            if (_values.TryGetValue(p, out values))
            {
                return (TResult)values.First();
            }
            throw new ArgumentNotFoundException();
        }

        TResult[] IParseResults<TTemplate>.GetResults<TResult>(Expression<Func<TTemplate, TResult>> expr)
        {
            var p = Utils.GetPropFromExpression(expr);
            object[] values;
            if (_values.TryGetValue(p, out values))
            {
                return values.Cast<TResult>().ToArray();
            }
#if NET40
            return new TResult[] { };
#else
            return Array.Empty<TResult>();
#endif
        }

        bool IParseResults<TTemplate>.TryGetResult<TResult>(Expression<Func<TTemplate, TResult>> expr, out TResult value)
        {
            var p = Utils.GetPropFromExpression(expr);
            object[] values;
            if (_values.TryGetValue(p, out values))
            {
                value = (TResult)values.First();
                return true;
            }
            value = default(TResult);
            return false;
        }
    }


    // ------------------------------------------------------------------------------------
    // Analyzer
    // ------------------------------------------------------------------------------------

    internal class AnalyzedProperty
    {
        public PropertyInfo PropertyInfo { get; set; }
        public Type Type { get; set; }
        public bool IsMandatory { get; set; }
        public string DefaultCliArg { get; }
        public string[] AllCliArgs { get; }
        /// <summary>
        /// Can be null if no description set
        /// </summary>
        public string Description { get; set; }

        public AnalyzedProperty(PropertyInfo p)
        {
            PropertyInfo = p;
            Type = p.PropertyType;
            DefaultCliArg = "--" + p.Name.ToLower().Replace('_', '-');
            AllCliArgs = new[] { DefaultCliArg };
            IsMandatory = p.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType == typeof(MandatoryAttribute));
            Description = (string) p.GetCustomAttributesData().FirstOrDefault(a => a.Constructor.DeclaringType == typeof(System.ComponentModel.DescriptionAttribute) && a.ConstructorArguments.Count == 1)?.ConstructorArguments[0].Value;
        }

        public string Unparse(object o)
        {
            // yeah, that is probably not enough. But good enough for a first step.
            return o.ToString();
        }

        public object Parse(string s)
        {
            // whatever, add unit tests, fix what breaks.
            if (Type == typeof(string))
                return s;
            if (Type == typeof(int))
                return int.Parse(s);
            if (Type == typeof(double))
                return double.Parse(s);  // note: take care of . vs ,!
            if (Type == typeof(float))
                return float.Parse(s);  // note: take care of . vs ,!
            return Convert.ChangeType(s, Type);
        }
    }

    internal class AnalyzedTemplate
    {
        public Dictionary<PropertyInfo, AnalyzedProperty> Properties { get; }
        public AnalyzedTemplate(Type t)
        {
            var props = t.GetProperties();
            Properties = props.ToDictionary(p => p, p => new AnalyzedProperty(p));
        }
    }

    // the JIT ensures that each type is only analyzed once.
    // Important: this must never throw an exception!
    internal static class TemplateAnalyzer<TTemplate>
    {
        public static AnalyzedTemplate Instance = new AnalyzedTemplate(typeof(TTemplate));
    }


    // ------------------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------------------

    internal static class Utils
    {
        // in: System.Int32
        // out: <int>
        public static string TypeToTypeHelp(Type t)
        {
            if (t == typeof(int))
                return "<int>";
            else if (t == typeof(string))
                return "<string>";
            else if (t == typeof(bool))
                return "<true|false>";
            else if (t == typeof(double) ||
                     t == typeof(float))
                return "<number>";
            return t.Name;
        }

        public static PropertyInfo GetPropFromExpression<TTemplate, TResult>(Expression<Func<TTemplate, TResult>> expr)
        {
            // shamelessly copy-pasted from https://stackoverflow.com/a/672212/1872399

            Type type = typeof(TTemplate);
            MemberExpression member = expr.Body as MemberExpression;
            if (member == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a method, not a property.",
                    expr.ToString()));

            PropertyInfo propInfo = member.Member as PropertyInfo;
            if (propInfo == null)
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a field, not a property.",
                    expr.ToString()));

            if (type != propInfo.ReflectedType &&
                !type.IsSubclassOf(propInfo.ReflectedType))
                throw new ArgumentException(string.Format(
                    "Expression '{0}' refers to a property that is not from type {1}.",
                    expr.ToString(),
                    type));

            return propInfo;
        }

        public static string EscapeCliString(string value)
        {
            var invalidChars = new[] { (char)0 };
            var peskyChars = new[] { '"', '\t', ' ', '\\' };
            bool doesStringContain(IEnumerable<char> chars, string s)
            {
                return s.Any(c => chars.Contains(c));
            }

            if (value == null)
                throw new ArgumentNullException(nameof(value));
            else if (value == "")
                return "\"\"";
            else if (doesStringContain(invalidChars, value))
                throw new InvalidOperationException("The string can not be roundtripped.");
            else if (false == doesStringContain(peskyChars, value))
                return value;

            // well, well, we need to take special care ...
            var escapedChars = new List<char>();
            escapedChars.Add('"');
            foreach (var (i, c) in value.Select((c, i) => (i, c)))
            {
                if (c == '"')
                {
                    escapedChars.Add('\\');
                    escapedChars.Add('"');
                }
                else if (c == '\\')
                {
                    /* The rules for " and \ are stupid. Source: https://github.com/ArildF/masters/blob/1542218180f2f462c604173ce8925f419155f19c/trunk/sscli/clr/src/vm/util.cpp#L1013
                     * 2N backslashes + " ==> N backslashes and begin/end quote
                     * 2N+1 backslashes + " ==> N backslashes + literal "
                     * N backslashes ==> N backslashes
                     */

                    var nextCharAfterBackslashes = value.Skip(i + 1).Where(x => x != '\\').FirstOrDefault();
                    if (nextCharAfterBackslashes == '"' || nextCharAfterBackslashes == default(char))
                    {
                        escapedChars.Add('\\');
                        escapedChars.Add('\\');
                    }
                    else
                        escapedChars.Add('\\');
                }
                else
                    escapedChars.Add(c);
            }
            escapedChars.Add('"');
            return new string(escapedChars.ToArray());
        }

        public static string FlattenCliTokens(IEnumerable<string> tokens)
        {
            return string.Join(" ", tokens.Select(EscapeCliString));
        }
    }
}
