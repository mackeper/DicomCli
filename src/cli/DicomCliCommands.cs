using System.CommandLine;
using System.CommandLine.Invocation;

public static class DicomCliCommands
{
    public static RootCommand Build(TextWriter output, TextWriter error)
    {
        var readFileArgument = new Argument<string>(
            name: "file",
            description: "Path to DICOM file")
        {
            Arity = ArgumentArity.ExactlyOne
        };

        var jsonInputArgument = new Argument<string>(
            name: "input-json",
            description: "Path to DICOMweb JSON file");

        var dicomOutputArgument = new Argument<string>(
            name: "output-dicom",
            description: "Path to output DICOM file");

        var formatOption = new Option<string>(
            aliases: ["-f", "--format"],
            getDefaultValue: () => "text",
            description: "Output format: text or json")
            .FromAmong("text", "json");

        var binaryFormatOption = new Option<string>(
            aliases: ["--binary-format"],
            description: "Binary value format: base64, summary, or hex")
            .FromAmong("base64", "summary", "hex");

        var readCommand = new Command("read", "Reads a DICOM file and prints dataset tags")
        {
            readFileArgument,
            formatOption,
            binaryFormatOption
        };

        readCommand.SetHandler(context =>
        {
            var filePath = context.ParseResult.GetValueForArgument(readFileArgument);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "text";
            var binaryFormat = DicomCliApp.ResolveBinaryFormatDefault(format, context.ParseResult.GetValueForOption(binaryFormatOption));

            context.ExitCode = DicomCliApp.ExecuteRead(filePath, format, binaryFormat, output, error);
        });

        var writeCommand = new Command("write", "Reads DICOMweb JSON and writes a DICOM file")
        {
            jsonInputArgument,
            dicomOutputArgument
        };

        writeCommand.SetHandler(context =>
        {
            var inputPath = context.ParseResult.GetValueForArgument(jsonInputArgument);
            var outputPath = context.ParseResult.GetValueForArgument(dicomOutputArgument);

            context.ExitCode = DicomCliApp.ExecuteWrite(inputPath, outputPath, error);
        });

        var rootCommand = new RootCommand("Reads DICOM files and writes DICOM files from DICOMweb JSON")
        {
            readCommand,
            writeCommand
        };

        rootCommand.SetHandler((InvocationContext context) =>
        {
            var filePath = context.ParseResult.GetValueForArgument(readFileArgument);
            var format = context.ParseResult.GetValueForOption(formatOption) ?? "text";
            var binaryFormat = DicomCliApp.ResolveBinaryFormatDefault(format, context.ParseResult.GetValueForOption(binaryFormatOption));

            context.ExitCode = DicomCliApp.ExecuteRead(filePath, format, binaryFormat, output, error);
        });

        rootCommand.AddArgument(readFileArgument);
        rootCommand.AddOption(formatOption);
        rootCommand.AddOption(binaryFormatOption);

        return rootCommand;
    }
}
