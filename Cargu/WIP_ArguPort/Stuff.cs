using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cargu.WIP_ArguPort
{

    /// <summary>
    /// Hides argument from command line argument usage string.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class HiddenAttribute : Attribute
    {
    }

    /// <summary>
    /// Denotes that the given argument should accummulate any unrecognized arguments it encounters.
    /// Must contain a single field of type string
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class GatherUnrecognizedAttribute : Attribute
    {
    }

    /// <summary>
    /// Requires that CLI parameters should not override AppSettings parameters.
    /// Will return parsed results from both AppSettings and CLI.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class GatherAllSourcesAttribute : Attribute
    {
    }

    /// <summary>
    /// Disable CLI parsing for this argument. Use for AppSettings parsing only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class NoCommandLineAttribute : Attribute
    {
    }

    /// <summary>
    /// Disable AppSettings parsing for this branch. Use for CLI parsing only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false)]
    public class NoAppSettingsAttribute : Attribute
    {
    }

    /// <summary>
    /// Predefined CLI prefixes to be added
    /// </summary>
    public static class CliPrefix
    {
        /// <summary>
        /// No Cli Prefix
        /// </summary>
        public const string None = "";
        /// <summary>
        /// Single Dash prefix '-'
        /// </summary>
        public const string Dash = "-";
        /// <summary>
        /// Double Dash prefix '--'
        /// </summary>
        public const string DoubleDash = "--";
    }

    /// <summary>
    /// Error codes reported by Argu
    /// </summary>
    public enum ErrorCode
    {
        HelpText = 0,
        AppSettings = 1,
        CommandLine = 2,
        PostProcess = 3
    }

    internal class CarguParseException : Exception
    {
        public ErrorCode ErrorCode { get; }

        public CarguParseException(string message, ErrorCode errorCode) : base(message)
        {
            this.ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Interface that must be implemented by all Argu template types
    /// </summary>
    public interface IArgParserTemplate
    {
        /// <summary>
        /// returns a usage string for every union case. Use it with `nameof(...)`.
        /// </summary>
        /// <param name="name">the name of the property</param>
        /// <returns>the help text for the property</returns>
        string Usage(string name);
    }

    /// <summary>
    /// An interface for error handling in the argument parser
    /// </summary>
    public interface IExiter
    {
        /// <summary>
        /// IExiter identifier
        /// </summary>
        string Name { get; }
        /// <summary>
        /// handle error of given message and error code
        /// </summary>
        void Exit(string msg, ErrorCode errorCode);
    }

    public class ExceptionExiter : IExiter
    {
        string IExiter.Name => "CarguException Exiter";

        void IExiter.Exit(string msg, ErrorCode errorCode)
        {
            throw new CarguParseException(msg, errorCode);
        }
    }

    public class ProcessExiter : IExiter
    {
        string IExiter.Name => "Process Exiter";

        void IExiter.Exit(string msg, ErrorCode errorCode)
        {
            bool isError = errorCode == ErrorCode.HelpText;
            Console.ForegroundColor = isError ? ConsoleColor.Yellow : ConsoleColor.Red;
            var writer = isError ? Console.Out : Console.Error;
            writer.WriteLine(msg);
            writer.Flush();
            Environment.Exit((int)errorCode);
        }
    }

    public interface IConfigurationReader
    {
        string Name { get; }
        string GetValue(string key);
    }
}
