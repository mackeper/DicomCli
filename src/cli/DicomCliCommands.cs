using System.CommandLine;
using System.CommandLine.Invocation;

public static class DicomCliCommands
{
    public static RootCommand Build(TextWriter output, TextWriter error)
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

        var outputOption = new Option<string?>(
            aliases: ["-o", "--output"],
            description: "Path to output DICOM file. When present, input file must be DICOMweb JSON.");

        var rootCommand = new RootCommand("Reads DICOM files and writes DICOM files from DICOMweb JSON")
        {
            fileArgument,
            formatOption,
            binaryFormatOption,
            outputOption
        };

        rootCommand.SetHandler((InvocationContext context) =>
        {
            var outputPath = context.ParseResult.GetValueForOption(outputOption);
            if (context.ParseResult.FindResultFor(outputOption)?.Tokens.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(outputPath))
                {
                    error.WriteLine("-o/--output requires an output DICOM path.");
                    context.ExitCode = 1;
                    return;
                }

                context.ExitCode = ExecuteWriteFromContext(context, fileArgument, formatOption, binaryFormatOption, outputPath, error);
                return;
            }

            context.ExitCode = ExecuteReadFromContext(context, fileArgument, formatOption, binaryFormatOption, output, error);
        });

        return rootCommand;
    }

    private static int ExecuteWriteFromContext(
        InvocationContext context,
        Argument<string> fileArg,
        Option<string> formatOpt,
        Option<string> binaryOpt,
        string outputPath,
        TextWriter error)
    {
        if (context.ParseResult.FindResultFor(formatOpt)?.Tokens.Count > 0)
        {
            error.WriteLine("--format cannot be used when writing with -o/--output.");
            return 1;
        }

        if (context.ParseResult.FindResultFor(binaryOpt)?.Tokens.Count > 0)
        {
            error.WriteLine("--binary-format cannot be used when writing with -o/--output.");
            return 1;
        }

        var inputPath = context.ParseResult.GetValueForArgument(fileArg);
        return DicomCliApp.ExecuteWrite(inputPath, outputPath, error);
    }

    private static int ExecuteReadFromContext(
        InvocationContext context,
        Argument<string> fileArg,
        Option<string> formatOpt,
        Option<string> binaryOpt,
        TextWriter output,
        TextWriter error)
    {
        var filePath = context.ParseResult.GetValueForArgument(fileArg);
        var formatStr = context.ParseResult.GetValueForOption(formatOpt) ?? "text";
        var binaryFormatStr = DicomCliApp.ResolveBinaryFormatDefault(formatStr, context.ParseResult.GetValueForOption(binaryOpt));
        return DicomCliApp.ExecuteRead(filePath, formatStr, binaryFormatStr, output, error);
    }
}
