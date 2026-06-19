using System.Text;
using System.Text.Json;
using FellowOakDicom;

internal static class CommandExecutor
{
    public static int Execute(CliCommand command, TextWriter output, TextWriter error, bool outputColor = false, bool errorColor = false)
    {
        var styledError = errorColor ? new AnsiColorTextWriter(error, AnsiColor.Red) : error;
        return command switch
        {
            ReadCommand read => ExecuteRead(read, output, styledError, outputColor),
            ExtractCommand extract => ExecuteExtract(extract, output, styledError),
            WriteCommand write => ExecuteWrite(write, styledError),
            CompareCommand compare => ExecuteCompare(compare, output, styledError, outputColor),
            HelpCommand help => Write(help.Text, output),
            VersionCommand version => WriteLine(version.Text, output),
            _ => throw new InvalidOperationException($"Unknown command type: {command.GetType().Name}")
        };
    }

    private static int ExecuteExtract(ExtractCommand command, TextWriter output, TextWriter error)
    {
        if (!HasDicomExtension(command.FilePath))
        {
            error.WriteLine("Input file for extract mode must have extension .dcm or .dicom.");
            return 1;
        }

        if (!File.Exists(command.FilePath))
        {
            error.WriteLine($"File not found: {command.FilePath}");
            return 1;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(command.FilePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return 1;
        }

        var tag = FormatTag(command.Group, command.Element);
        var item = file.Dataset.FirstOrDefault(item => item.Tag.Group == command.Group && item.Tag.Element == command.Element);
        if (item is null)
        {
            error.WriteLine($"Tag {tag} was not found.");
            return 1;
        }

        if (!DicomExtractWriter.TryGetBinaryData(item, out var data))
        {
            error.WriteLine($"Tag {tag} has VR {item.ValueRepresentation.Code}; --extract only supports binary data.");
            return 1;
        }

        try
        {
            DicomExtractWriter.Write(data, command.Format, output);
            return 0;
        }
        catch (DecoderFallbackException ex)
        {
            error.WriteLine($"Failed to decode tag {tag} as UTF-8 XML: {ex.Message}");
            return 1;
        }
        catch (System.Xml.XmlException ex)
        {
            error.WriteLine($"Failed to parse tag {tag} as XML: {ex.Message}");
            return 1;
        }
    }

    public static int ExecuteFailure(ParseFailure failure, TextWriter error, bool errorColor = false)
    {
        error.Write(AnsiColor.Colorize(failure.ErrorText, AnsiColor.Red, errorColor));
        return failure.ExitCode;
    }

    private static int ExecuteWrite(WriteCommand command, TextWriter error)
    {
        return ExecuteWrite(command.InputJsonPath, command.OutputDicomPath, command.Force, error);
    }

    private static int ExecuteWrite(string inputPath, string outputPath, bool force, TextWriter error)
    {
        if (!HasExtension(inputPath, ".json"))
        {
            error.WriteLine("Input file for write mode must have extension .json.");
            return 1;
        }

        if (!HasDicomExtension(outputPath))
        {
            error.WriteLine("Output file for write mode must have extension .dcm or .dicom.");
            return 1;
        }

        if (!File.Exists(inputPath))
        {
            error.WriteLine($"File not found: {inputPath}");
            return 1;
        }

        DicomDataset dataset;
        try
        {
            using var stream = File.OpenRead(inputPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error.WriteLine("DICOMweb JSON root must be an object.");
                return 1;
            }

            dataset = DicomwebJsonReader.Read(document.RootElement);
        }
        catch (JsonException ex)
        {
            error.WriteLine($"Failed to parse JSON file: {ex.Message}");
            return 1;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to create DICOM file: {ex.Message}");
            return 1;
        }
        catch (FormatException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return 1;
        }

        try
        {
            var file = new DicomFile(dataset);
            using var stream = new FileStream(outputPath, force ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
            file.Save(stream);
            return 0;
        }
        catch (IOException ex)
        {
            if (!force && File.Exists(outputPath))
            {
                error.WriteLine($"Output file already exists: {outputPath}. Use --force to overwrite.");
                return 1;
            }

            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return 1;
        }
    }

    private static int ExecuteCompare(CompareCommand command, TextWriter output, TextWriter error, bool outputColor)
    {
        if (!HasDicomExtension(command.LeftPath) || !HasDicomExtension(command.RightPath))
        {
            error.WriteLine("Input files for compare mode must have extension .dcm or .dicom.");
            return 2;
        }

        if (!File.Exists(command.LeftPath))
        {
            error.WriteLine($"File not found: {command.LeftPath}");
            return 2;
        }

        if (!File.Exists(command.RightPath))
        {
            error.WriteLine($"File not found: {command.RightPath}");
            return 2;
        }

        DicomFile leftFile;
        DicomFile rightFile;
        try
        {
            leftFile = DicomFile.Open(command.LeftPath);
            rightFile = DicomFile.Open(command.RightPath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 2;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 2;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return 2;
        }

        var differences = DicomDatasetComparer.Compare(leftFile.Dataset, rightFile.Dataset);
        DicomDatasetComparer.WriteDifferences(differences, output, outputColor);
        return differences.Count == 0 ? 0 : 1;
    }

    private static int ExecuteRead(ReadCommand command, TextWriter output, TextWriter error, bool outputColor)
    {
        if (command.Format == OutputFormat.Json && command.BinaryFormat != BinaryFormat.Base64)
        {
            error.WriteLine($"--binary-format {FormatBinary(command.BinaryFormat)} cannot be used with --format json. DICOMweb JSON requires base64 InlineBinary.");
            return 1;
        }

        if (!HasDicomExtension(command.FilePath))
        {
            error.WriteLine("Input file for read mode must have extension .dcm or .dicom.");
            return 1;
        }

        if (!File.Exists(command.FilePath))
        {
            error.WriteLine($"File not found: {command.FilePath}");
            return 1;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(command.FilePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return 1;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return 1;
        }

        var dataset = file.Dataset;
        var transferSyntaxName = file.FileMetaInfo?.TransferSyntax?.UID?.Name ?? "Unknown";
        if (command.Format == OutputFormat.Json)
        {
            DicomwebJsonWriter.Write(dataset, output, command.CompactJson);
            return 0;
        }

        DicomTextWriter.Write(dataset, transferSyntaxName, command.BinaryFormat, output, outputColor);
        return 0;
    }

    internal static bool HasDicomExtension(string path)
    {
        return HasExtension(path, ".dcm") || HasExtension(path, ".dicom");
    }

    private static int WriteLine(string text, TextWriter output)
    {
        output.WriteLine(text);
        return 0;
    }

    private static int Write(string text, TextWriter output)
    {
        output.Write(text);
        return 0;
    }

    private static string FormatBinary(BinaryFormat binaryFormat)
    {
        return binaryFormat switch
        {
            BinaryFormat.Base64 => "base64",
            BinaryFormat.Hex => "hex",
            _ => "summary"
        };
    }

    private static string FormatTag(ushort group, ushort element) => $"{group:X4}{element:X4}";

    private static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class AnsiColorTextWriter(TextWriter writer, string color) : TextWriter
    {
        public override Encoding Encoding => writer.Encoding;

        public override IFormatProvider FormatProvider => writer.FormatProvider;

        public override void Write(string? value)
        {
            writer.Write(AnsiColor.Colorize(value ?? string.Empty, color, enabled: true));
        }

        public override void WriteLine(string? value)
        {
            writer.WriteLine(AnsiColor.Colorize(value ?? string.Empty, color, enabled: true));
        }
    }
}
