using System.CommandLine;
using System.CommandLine.IO;
using System.CommandLine.Invocation;
using System.Globalization;
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

        return new ParseFailure(ExitCode.InvalidArguments, Combine(output.ToString(), error.ToString()));
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

        var compareOption = new Option<string?>(
            aliases: ["-c", "--compare"],
            description: "Path to DICOM file to compare with input file.");

        var extractOption = new Option<string?>(
            aliases: ["--extract"],
            description: "Extract a binary DICOM tag as <tag>:base64, <tag>:hex, or <tag>:xml.");

        var rootCommand = new RootCommand("""
            Reads, compares, and writes DICOM files

            Exit codes:
              0  Success
              1  Validation failure
              2  Invalid arguments or options
              3  Input file missing or unreadable
              4  Invalid DICOM input
              5  Invalid JSON or DICOMweb JSON
              6  Write failure
              7  Compare found differences
            """)
        {
            fileArgument,
            formatOption,
            binaryFormatOption,
            compactOption,
            outputOption,
            forceOption,
            compareOption,
            extractOption
        };
        rootCommand.Name = GetProductName();

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
                compareOption,
                extractOption,
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

    private static CliCommand? ParseCommandFromContext(
        InvocationContext context,
        Argument<string> fileArg,
        Option<string> formatOpt,
        Option<string> binaryOpt,
        Option<bool> compactOpt,
        Option<string?> outputOpt,
        Option<bool> forceOpt,
        Option<string?> compareOpt,
        Option<string?> extractOpt,
        TextWriter error)
    {
        var inputPath = context.ParseResult.GetValueForArgument(fileArg);
        var comparePath = context.ParseResult.GetValueForOption(compareOpt);
        var hasExtractOption = context.ParseResult.FindResultFor(extractOpt)?.Tokens.Count > 0;
        if (context.ParseResult.FindResultFor(compareOpt)?.Tokens.Count > 0)
        {
            if (string.IsNullOrWhiteSpace(comparePath))
            {
                error.WriteLine("-c/--compare requires a DICOM file path.");
                return null;
            }

            if (context.ParseResult.FindResultFor(outputOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("-o/--output cannot be used when comparing with -c/--compare.");
                return null;
            }

            if (hasExtractOption)
            {
                error.WriteLine("--extract cannot be used when comparing with -c/--compare.");
                return null;
            }

            if (context.ParseResult.FindResultFor(formatOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--format cannot be used when comparing with -c/--compare.");
                return null;
            }

            if (context.ParseResult.FindResultFor(binaryOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--binary-format cannot be used when comparing with -c/--compare.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(compactOpt))
            {
                error.WriteLine("--compact cannot be used when comparing with -c/--compare.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(forceOpt))
            {
                error.WriteLine("--force cannot be used when comparing with -c/--compare.");
                return null;
            }

            return new CompareCommand(inputPath, comparePath);
        }

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

            if (hasExtractOption)
            {
                error.WriteLine("--extract cannot be used when writing with -o/--output.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(compactOpt))
            {
                error.WriteLine("--compact cannot be used when writing with -o/--output.");
                return null;
            }

            return new WriteCommand(inputPath, outputPath, context.ParseResult.GetValueForOption(forceOpt));
        }

        if (hasExtractOption)
        {
            if (context.ParseResult.FindResultFor(formatOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--format cannot be used when extracting with --extract.");
                return null;
            }

            if (context.ParseResult.FindResultFor(binaryOpt)?.Tokens.Count > 0)
            {
                error.WriteLine("--binary-format cannot be used when extracting with --extract.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(compactOpt))
            {
                error.WriteLine("--compact cannot be used when extracting with --extract.");
                return null;
            }

            if (context.ParseResult.GetValueForOption(forceOpt))
            {
                error.WriteLine("--force cannot be used when extracting with --extract.");
                return null;
            }

            var extractValue = context.ParseResult.GetValueForOption(extractOpt);
            return TryParseExtractValue(extractValue, error, out var group, out var element, out var extractFormat)
                ? new ExtractCommand(inputPath, group, element, extractFormat)
                : null;
        }

        if (context.ParseResult.GetValueForOption(forceOpt))
        {
            error.WriteLine("--force can only be used when writing with -o/--output.");
            return null;
        }

        var formatStr = context.ParseResult.GetValueForOption(formatOpt) ?? "text";
        var binaryFormatStr = ResolveBinaryFormatDefault(formatStr, context.ParseResult.GetValueForOption(binaryOpt));

        return new ReadCommand(
            inputPath,
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

    private static bool TryParseExtractValue(
        string? value,
        TextWriter error,
        out ushort group,
        out ushort element,
        out ExtractFormat format)
    {
        group = 0;
        element = 0;
        format = ExtractFormat.Base64;

        if (string.IsNullOrWhiteSpace(value))
        {
            error.WriteLine("--extract requires <tag>:<format>.");
            return false;
        }

        var parts = value.Split(':', 2);
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            error.WriteLine("--extract requires <tag>:<format>.");
            return false;
        }

        if (!TryParseTag(parts[0], out group, out element))
        {
            error.WriteLine("--extract tag must be 8 hex characters.");
            return false;
        }

        format = parts[1].ToLowerInvariant() switch
        {
            "base64" => ExtractFormat.Base64,
            "hex" => ExtractFormat.Hex,
            "xml" => ExtractFormat.Xml,
            _ => ExtractFormat.Base64
        };

        if (format == ExtractFormat.Base64 && !string.Equals(parts[1], "base64", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine("--extract format must be base64, hex, or xml.");
            return false;
        }

        return true;
    }

    private static bool TryParseTag(string value, out ushort group, out ushort element)
    {
        group = 0;
        element = 0;
        return value.Length == 8
            && ushort.TryParse(value[..4], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out group)
            && ushort.TryParse(value[4..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out element);
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
