using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Cargu
{
    // ------------------------------------------------------------------------------------
    // Exceptions
    // ------------------------------------------------------------------------------------

    public class ArgumentNotFoundException : Exception
    {
    }

    public class RequiredArgumentNotSuppliedException : Exception
    {
        public string[] Missing { get; }
        public RequiredArgumentNotSuppliedException(string[] missing) : base($"Missing mandatory props: '{string.Join(", ", missing)}'!")
        {
            Missing = missing;
        }
    }

    public class UnrecognizedArgumentException : Exception
    {
        public string Argument { get; set; }
        public UnrecognizedArgumentException(string arg) : base($"Encountered unrecognized argument '{arg}'")
        {
            Argument = arg;
        }
    }

    public class DuplicateArgumentException : Exception
    {
        public string Argument { get; set; }
        public DuplicateArgumentException(string arg) : base($"Encountered unique argument '{arg}' twice")
        {
            Argument = arg;
        }
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
    /// Demands that the argument should be specified at most once; a parse exception is raised otherwise.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class UniqueAttribute : Attribute
    {
    }

    /// <summary>
    /// Demands that the argument should be specified exactly once; a parse exception is raised otherwise.
    /// Equivalent to attaching both the Mandatory and Unique attribute on the parameter.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class ExactlyOnceAttribute : Attribute
    {
    }

    /// <summary>
    /// Declares a custom default CLI identifier for the current parameter.
    /// Replaces the auto-generated identifier name.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class CustomCommandLineAttribute : Attribute
    {
        public string Cli { get; }
        public CustomCommandLineAttribute(string cli)
        {
            Cli = cli;
        }
    }

    /// <summary>
    /// Declares a set of secondary CLI identifiers for the current parameter.
    /// Does not replace the default identifier which is either auto-generated
    /// or specified by the CustomCommandLine attribute.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class AltCommandLineAttribute : Attribute
    {
    }


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

    public interface IParser<TTemplate>
    {

    }



    // ------------------------------------------------------------------------------------
    // Implementations
    // ------------------------------------------------------------------------------------

    public static class ArgumentParser
    {
        public static IArgumentParser<TTemplate> Create<TTemplate>()
        {
            return new ArgumentParser<TTemplate>();
        }
    }

    internal class ArgumentParser<TTemplate> : IArgumentParser<TTemplate>
    {
        IParseResults<TTemplate> IArgumentParser<TTemplate>.Parse(string[] cliArgs, bool parseAppConfig)
        {
            if (parseAppConfig)
                throw new NotImplementedException("app.config parser is not yet supported!");

            var model = TemplateAnalyzer<TTemplate>.Instance;
            HashSet<AnalyzedProperty> mandatoryProps = new HashSet<AnalyzedProperty>();
            foreach (var p in model.Properties.Values)
                if (p.IsMandatory) mandatoryProps.Add(p);

            AnalyzedProperty state = null;
            List<string> tupleElements = new List<string>();
            LoookUp<PropertyInfo, object> result = new LoookUp<PropertyInfo, object>();
            for (int i = 0; i < cliArgs.Length; i++)
            {
                var current = cliArgs[i];
                if (state == null)
                {
                    var prop = model.Properties.Values.SingleOrDefault(p => p.CliArgs.Contains(current));

                    if (prop == null)
                        throw new UnrecognizedArgumentException(current);

                    if (prop.IsUnique && result.ContainsKey(prop.PropertyInfo))
                        throw new DuplicateArgumentException(current);

                    if (prop.IsMandatory)
                        mandatoryProps.Remove(prop);

                    if (prop.Type == typeof(Toggle))
                        result.Add(prop.PropertyInfo, null);
                    else
                        state = prop;
                }
                else
                {
                    if (state.IsTupleType)
                    {
                        tupleElements.Add(current);
                        if (tupleElements.Count == state.TupleElementTypes.Length)
                        {
                            var val = state.ParseTuple(tupleElements.ToArray());
                            result.Add(state.PropertyInfo, val);
                            state = null;
                            tupleElements.Clear();
                        }
                    }
                    else
                    {
                        var val = state.Parse(current);
                        result.Add(state.PropertyInfo, val);
                        state = null;
                    }
                }
            }

            if (state != null)
                throw new Exception($"expected value for '{state.CliArgs.First()}'!");

            if (mandatoryProps.Count != 0)
                throw new RequiredArgumentNotSuppliedException(mandatoryProps.Select(x => x.CliArgs.First()).ToArray());


            return new ParseResults<TTemplate>(result.ToResult());
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
            AnalyzedProperty aprop = TemplateAnalyzer<TTemplate>.Instance.Properties[prop];
            var key = aprop.CliArgs.First();
            _tokens.Add(key);
            if (false == aprop.IsTupleType)
            {
                var val = aprop.Unparse(value);
                _tokens.Add(val);
            }
            else
            {
                var values = aprop.UnparseTuple(value);
                _tokens.AddRange(values);
            }

            return this;
        }

        IUnparser<TTemplate> IUnparser<TTemplate>.With(Expression<Func<TTemplate, Toggle>> expr)
        {
            var prop = Utils.GetPropFromExpression(expr);

            var toggle = TemplateAnalyzer<TTemplate>.Instance.Properties[prop].CliArgs.First();
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
        public bool IsUnique { get; set; }
        public string[] CliArgs { get; }

        public bool IsTupleType { get; }
        public Type[] TupleElementTypes { get; }

        public AnalyzedProperty(PropertyInfo p)
        {
            PropertyInfo = p;
            Type = p.PropertyType;

            bool hasMandatoryAttribute = p.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType == typeof(MandatoryAttribute));
            bool hasUniqueAttribute = p.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType == typeof(UniqueAttribute));
            bool hasExactlyOnceAttribute = p.GetCustomAttributesData().Any(a => a.Constructor.DeclaringType == typeof(ExactlyOnceAttribute));
            var customCommandLine = (string)p.GetCustomAttributesData().FirstOrDefault(a => a.Constructor.DeclaringType == typeof(CustomCommandLineAttribute))?.ConstructorArguments.Single().Value;
            var additionalCommandLines = p.GetCustomAttributesData().Where(a => a.Constructor.DeclaringType == typeof(AltCommandLineAttribute)).Select(a => (string)a.ConstructorArguments.Single().Value).ToArray();

            IsMandatory = hasMandatoryAttribute || hasExactlyOnceAttribute;
            IsUnique = hasUniqueAttribute || hasExactlyOnceAttribute;

            var cliArgs = new List<string>();
            if (customCommandLine != null)
                cliArgs.Add(customCommandLine);
            else
                cliArgs.Add("--" + p.Name.ToLower().Replace('_', '-'));
            cliArgs.AddRange(additionalCommandLines);
            CliArgs = cliArgs.ToArray();

            if (Type.FullName.StartsWith("System.ValueTuple`"))
            {
                IsTupleType = true;
                TupleElementTypes = Type.GetGenericArguments();
            }
        }

        public string Unparse(object o)
        {
            // yeah, that is probably not enough. But good enough for a first step.
            return o.ToString();
        }

        public string[] UnparseTuple(object o)
        {
            throw new NotImplementedException();
        }

        private static object Parse(Type t, string s)
        {
            // whatever, add unit tests, fix what breaks.
            if (t == typeof(string))
                return s;
            if (t == typeof(int))
                return int.Parse(s);
            if (t == typeof(double))
                return double.Parse(s);  // note: take care of . vs ,!
            if (t == typeof(float))
                return float.Parse(s);  // note: take care of . vs ,!
            return Convert.ChangeType(s, t);
        }

        public object Parse(string s)
        {
            return Parse(Type, s);
        }

        public object ParseTuple(string[] strings)
        {
            if (false == IsTupleType)
                throw new InvalidOperationException("This is not a tuple prop");
            if (strings.Length != TupleElementTypes.Length)
                throw new InvalidOperationException("Wrong tuple arity");
            var objects = strings.Select((s, i) => Parse(TupleElementTypes[i], s)).ToArray();
            switch (TupleElementTypes.Length)
            {
                case 1:
                    return typeof(ValueTuple<>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 2:
                    return typeof(ValueTuple<,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 3:
                    return typeof(ValueTuple<,,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 4:
                    return typeof(ValueTuple<,,,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 5:
                    return typeof(ValueTuple<,,,,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 6:
                    return typeof(ValueTuple<,,,,,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                case 7:
                    return typeof(ValueTuple<,,,,,,>).MakeGenericType(typeArguments: TupleElementTypes).GetConstructor(types: TupleElementTypes).Invoke(parameters: objects);
                default:
                    throw new NotImplementedException("Tuple arities > 7 are not yet supported.");
            }
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
