using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using System.Reflection;
using System.Text;

internal static class ArgumentParser
{
    public static ArgumentParseResult Parse(string[] args)
    {
        if (args is ["--version"])
        {
            return new ParseSuccess(new VersionCommand(GetVersionLine()));
        }

        using var output = new StringWriter();
        using var error = new StringWriter();
        CliCommand? command = null;
        var rootCommand = Build(parsedCommand => command = parsedCommand, error);
        var exitCode = rootCommand.Invoke(args, new TextWriterConsole(output, error));

        if (command is not null)
        {
            return new ParseSuccess(command);
        }

        if (exitCode == 0 && output.ToString().Length > 0)
        {
            return new ParseSuccess(new HelpCommand(output.ToString()));
        }

        return new ParseFailure(exitCode, Combine(output.ToString(), error.ToString()));
    }

    public static RootCommand Build(Action<CliCommand> setCommand, TextWriter error)
    {
        var fileArgument = new Argument<string>(
            name: "file",
            description: "Path to input DICOM or DICOMweb JSON file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var formatOption = new Option<string>(
            aliases: ["-f", "--format"],
            getDefaultValue: () => "text",
            description: "Output format: text or json")
            .FromAmong("text", "json");

        var binaryFormatOption = new Option<string>(
            aliases: ["--binary-format"],
            description: "Binary value format: summary, hex, or base64")
            .FromAmong("summary", "hex", "base64");

        var compactOption = new Option<bool>(
            aliases: ["--compact"],
            description: "Emit JSON without indentation.");

        var outputOption = new Option<string?>(
            aliases: ["-o", "--output"],
            description: "Path to output DICOM file. When present, input file must be DICOMweb JSON.");

        var forceOption = new Option<bool>(
            aliases: ["--force"],
            description: "Overwrite the output DICOM file when writing.");

        var compareCommand = BuildCompareCommand(setCommand);

        var rootCommand = new RootCommand("Reads DICOM files and writes DICOM files from DICOMweb JSON")
        {
            fileArgument,
            formatOption,
            binaryFormatOption,
            compactOption,
            outputOption,
            forceOption
        };
        rootCommand.Name = GetProductName();
        rootCommand.AddCommand(compareCommand);

        rootCommand.SetHandler((InvocationContext context) =>
        {
            var command = ParseCommandFromContext(
                context,
                fileArgument,
                formatOption,
                binaryFormatOption,
                compactOption,
                outputOption,
                forceOption,
                error);

            if (command is null)
            {
                context.ExitCode = 1;
                return;
            }

            setCommand(command);
            context.ExitCode = 0;
        });

        return rootCommand;
    }

    private static Command BuildCompareCommand(Action<CliCommand> setCommand)
    {
        var leftArgument = new Argument<string>(
            name: "left",
            description: "Path to left DICOM file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var rightArgument = new Argument<string>(
            name: "right",
            description: "Path to right DICOM file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var compareCommand = new Command("compare", "Compare two DICOM files")
        {
            leftArgument,
            rightArgument
        };

        compareCommand.SetHandler((InvocationContext context) =>
        {
            setCommand(new CompareCommand(
                context.ParseResult.GetValueForArgument(leftArgument),
                context.ParseResult.GetValueForArgument(rightArgument)));
            context.ExitCode = 0;
        });

        return compareCommand;
    }

    private static CliCommand? ParseCommandFromContext(
        InvocationContext context,
        Argument<string> fileArg,
        Option<string> formatOpt,
        Option<string> binaryOpt,
        Option<bool> compactOpt,
        Option<string?> outputOpt,
        Option<bool> forceOpt,
        TextWriter error)
    {
        var outputPath = context.ParseResult.GetValueForOption(outputOpt);
        if (context.ParseResult.FindResultFor(outputOpt)?.Tokens.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
            {
                error.WriteLine("-o/--output requires an output DICOM path.");
                return null;
            }

            if (context.ParseResult.FindResultFor(formatOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--format cannot be used when writing with -o/--output.");
                return null;
            }

            if (context.ParseResult.FindResultFor(binaryOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--binary-format cannot be used when writing with -o/--output.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(compactOpt))
            {
                error.WriteLine("--compact cannot be used when writing with -o/--output.");
                return null;
            }

            var inputPath = context.ParseResult.GetValueForArgument(fileArg);
            return new WriteCommand(inputPath, outputPath, context.ParseResult.GetValueForOption(forceOpt));
        }

        if (context.ParseResult.GetValueForOption(forceOpt))
        {
            error.WriteLine("--force can only be used when writing with -o/--output.");
            return null;
        }

        var filePath = context.ParseResult.GetValueForArgument(fileArg);
        var formatStr = context.ParseResult.GetValueForOption(formatOpt) ?? "text";
        var binaryFormatStr = ResolveBinaryFormatDefault(formatStr, context.ParseResult.GetValueForOption(binaryOpt));

        return new ReadCommand(
            filePath,
            ParseOutputFormat(formatStr),
            ParseBinaryFormat(binaryFormatStr),
            context.ParseResult.GetValueForOption(compactOpt));
    }

    private static string ResolveBinaryFormatDefault(string format, string? binaryFormat)
    {
        return binaryFormat ?? (format == "json" ? "base64" : "summary");
    }

    private static OutputFormat ParseOutputFormat(string value)
    {
        return value == "json" ? OutputFormat.Json : OutputFormat.Text;
    }

    private static BinaryFormat ParseBinaryFormat(string value)
    {
        return value switch
        {
            "base64" => BinaryFormat.Base64,
            "hex" => BinaryFormat.Hex,
            _ => BinaryFormat.Summary
        };
    }

    private static string Combine(string output, string error)
    {
        if (string.IsNullOrEmpty(output))
        {
            return error;
        }

        if (string.IsNullOrEmpty(error))
        {
            return output;
        }

        var builder = new StringBuilder(output);
        if (!output.EndsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            builder.AppendLine();
        }

        builder.Append(error);
        return builder.ToString();
    }

    private static string GetProductName()
    {
        var assembly = typeof(ArgumentParser).Assembly;
        var product = assembly.GetCustomAttribute<AssemblyProductAttribute>()?.Product;

        return NonEmpty(product, "DicomCli");
    }

    private static string GetVersionLine()
    {
        var assembly = typeof(ArgumentParser).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? assembly.GetName().Version?.ToString();

        return $"{GetProductName()} {NonEmpty(version, "0.1.1")}";
    }

    private static string NonEmpty(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }

    private sealed class TextWriterConsole(TextWriter output, TextWriter error) : IConsole
    {
        public IStandardStreamWriter Out { get; } = new TextWriterStreamWriter(output);

        public bool IsOutputRedirected => true;

        public IStandardStreamWriter Error { get; } = new TextWriterStreamWriter(error);

        public bool IsErrorRedirected => true;

        public bool IsInputRedirected => true;
    }

    private sealed class TextWriterStreamWriter(TextWriter writer) : IStandardStreamWriter
    {
        public void Write(string? value)
        {
            writer.Write(value);
        }
    }
}
