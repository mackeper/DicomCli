using System.Text;
using System.Text.Json;
using FellowOakDicom;
using FellowOakDicom.IO;
using FellowOakDicom.IO.Writer;

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
            ValidateCommand validate => ExecuteValidate(validate, styledError),
            CompareCommand compare => ExecuteCompare(compare, output, styledError, outputColor),
            HelpCommand help => Write(help.Text, output),
            VersionCommand version => WriteLine(version.Text, output),
            _ => throw new InvalidOperationException($"Unknown command type: {command.GetType().Name}")
        };
    }

    private static int ExecuteValidate(ValidateCommand command, TextWriter error)
    {
        if (HasDicomExtension(command.FilePath))
        {
            return ValidateDicomFile(command.FilePath, error);
        }

        if (HasExtension(command.FilePath, ".json"))
        {
            return ValidateDicomwebJson(command.FilePath, error);
        }

        error.WriteLine("Input file for validate mode must have extension .dcm, .dicom, or .json.");
        return ExitCode.InvalidArguments;
    }

    private static int ValidateDicomFile(string filePath, TextWriter error)
    {
        if (!File.Exists(filePath))
        {
            error.WriteLine($"File not found: {filePath}");
            return ExitCode.InputUnavailable;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(filePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return ExitCode.InvalidDicom;
        }

        try
        {
            new DicomFile(file.Dataset).Save(Stream.Null);
            return ExitCode.Success;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"DICOM validation failed: {ex.Message}");
            return ExitCode.ValidationFailure;
        }
    }

    private static int ValidateDicomwebJson(string filePath, TextWriter error)
    {
        if (!File.Exists(filePath))
        {
            error.WriteLine($"File not found: {filePath}");
            return ExitCode.InputUnavailable;
        }

        DicomDataset dataset;
        try
        {
            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error.WriteLine("DICOMweb JSON root must be an object.");
                return ExitCode.InvalidJson;
            }

            dataset = DicomwebJsonReader.Read(document.RootElement);
        }
        catch (JsonException ex)
        {
            error.WriteLine($"Failed to parse JSON file: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to create DICOM file: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (FormatException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return ExitCode.InvalidJson;
        }

        try
        {
            new DicomFile(dataset).Save(Stream.Null);
            return ExitCode.Success;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"DICOMweb JSON validation failed: {ex.Message}");
            return ExitCode.ValidationFailure;
        }
    }

    private static int ExecuteExtract(ExtractCommand command, TextWriter output, TextWriter error)
    {
        if (!HasDicomExtension(command.FilePath))
        {
            error.WriteLine("Input file for extract mode must have extension .dcm or .dicom.");
            return ExitCode.InvalidArguments;
        }

        if (!File.Exists(command.FilePath))
        {
            error.WriteLine($"File not found: {command.FilePath}");
            return ExitCode.InputUnavailable;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(command.FilePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return ExitCode.InvalidDicom;
        }

        var tag = FormatTag(command.Group, command.Element);
        var item = file.Dataset.FirstOrDefault(item => item.Tag.Group == command.Group && item.Tag.Element == command.Element);
        if (item is null)
        {
            error.WriteLine($"Tag {tag} was not found.");
            return ExitCode.ValidationFailure;
        }

        if (!DicomExtractWriter.TryGetBinaryData(item, out var data))
        {
            error.WriteLine($"Tag {tag} has VR {item.ValueRepresentation.Code}; --extract only supports binary data.");
            return ExitCode.ValidationFailure;
        }

        try
        {
            DicomExtractWriter.Write(data, command.Format, output);
            return ExitCode.Success;
        }
        catch (DecoderFallbackException ex)
        {
            error.WriteLine($"Failed to decode tag {tag} as UTF-8 XML: {ex.Message}");
            return ExitCode.ValidationFailure;
        }
        catch (System.Xml.XmlException ex)
        {
            error.WriteLine($"Failed to parse tag {tag} as XML: {ex.Message}");
            return ExitCode.ValidationFailure;
        }
    }

    public static int ExecuteFailure(ParseFailure failure, TextWriter error, bool errorColor = false)
    {
        error.Write(AnsiColor.Colorize(failure.ErrorText, AnsiColor.Red, errorColor));
        return failure.ExitCode;
    }

    private static int ExecuteWrite(WriteCommand command, TextWriter error)
    {
        return ExecuteWrite(command.InputJsonPath, command.OutputDicomPath, command.Force, command.SkipValidation, error);
    }

    private static int ExecuteWrite(string inputPath, string outputPath, bool force, bool skipValidation, TextWriter error)
    {
        if (!HasExtension(inputPath, ".json"))
        {
            error.WriteLine("Input file for write mode must have extension .json.");
            return ExitCode.InvalidArguments;
        }

        if (!HasDicomExtension(outputPath))
        {
            error.WriteLine("Output file for write mode must have extension .dcm or .dicom.");
            return ExitCode.InvalidArguments;
        }

        if (!File.Exists(inputPath))
        {
            error.WriteLine($"File not found: {inputPath}");
            return ExitCode.InputUnavailable;
        }

        DicomDataset dataset;
        try
        {
            using var stream = File.OpenRead(inputPath);
            using var document = JsonDocument.Parse(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                error.WriteLine("DICOMweb JSON root must be an object.");
                return ExitCode.InvalidJson;
            }

            dataset = DicomwebJsonReader.Read(document.RootElement, validateItems: !skipValidation);
        }
        catch (JsonException ex)
        {
            error.WriteLine($"Failed to parse JSON file: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to read JSON file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to create DICOM file: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (FormatException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return ExitCode.InvalidJson;
        }
        catch (InvalidOperationException ex)
        {
            error.WriteLine($"Failed to parse DICOMweb JSON: {ex.Message}");
            return ExitCode.InvalidJson;
        }

        try
        {
            if (skipValidation)
            {
                using var stream = new FileStream(outputPath, force ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
                SaveBestEffortDicom(stream, dataset);
            }
            else
            {
                var file = new DicomFile(dataset);
                using var stream = new FileStream(outputPath, force ? FileMode.Create : FileMode.CreateNew, FileAccess.Write, FileShare.None);
                file.Save(stream);
            }

            return ExitCode.Success;
        }
        catch (IOException ex)
        {
            if (!force && File.Exists(outputPath))
            {
                error.WriteLine($"Output file already exists: {outputPath}. Use --force to overwrite.");
                return ExitCode.WriteFailure;
            }

            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return ExitCode.WriteFailure;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return ExitCode.WriteFailure;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to write DICOM file: {ex.Message}");
            return ExitCode.WriteFailure;
        }
    }

    private static void SaveBestEffortDicom(Stream stream, DicomDataset dataset)
    {
        var fileMetaInfo = CreateBestEffortFileMetaInformation(dataset);
        var target = new StreamByteTarget(stream);
        var writer = new DicomFileWriter(DicomWriteOptions.Default);

        writer.Write(target, fileMetaInfo, dataset);
    }

    private static DicomFileMetaInformation CreateBestEffortFileMetaInformation(DicomDataset dataset)
    {
        var fileMetaInfo = new DicomFileMetaInformation
        {
            Version = [0x00, 0x01],
            TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian,
            ImplementationClassUID = DicomImplementation.ClassUID,
            ImplementationVersionName = DicomImplementation.Version
        };

        TryCopyFileMetaUid(dataset, DicomTag.SOPClassUID, uid => fileMetaInfo.MediaStorageSOPClassUID = uid);
        TryCopyFileMetaUid(dataset, DicomTag.SOPInstanceUID, uid => fileMetaInfo.MediaStorageSOPInstanceUID = uid);

        return fileMetaInfo;
    }

    private static void TryCopyFileMetaUid(DicomDataset dataset, DicomTag tag, Action<DicomUID> setValue)
    {
        try
        {
            if (dataset.TryGetSingleValue(tag, out DicomUID uid))
            {
                setValue(uid);
            }
        }
        catch (DicomException)
        {
        }
        catch (FormatException)
        {
        }
        catch (InvalidOperationException)
        {
        }
    }

    private static int ExecuteCompare(CompareCommand command, TextWriter output, TextWriter error, bool outputColor)
    {
        if (!HasDicomExtension(command.LeftPath) || !HasDicomExtension(command.RightPath))
        {
            error.WriteLine("Input files for compare mode must have extension .dcm or .dicom.");
            return ExitCode.InvalidArguments;
        }

        if (!File.Exists(command.LeftPath))
        {
            error.WriteLine($"File not found: {command.LeftPath}");
            return ExitCode.InputUnavailable;
        }

        if (!File.Exists(command.RightPath))
        {
            error.WriteLine($"File not found: {command.RightPath}");
            return ExitCode.InputUnavailable;
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
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return ExitCode.InvalidDicom;
        }

        var differences = DicomDatasetComparer.Compare(leftFile.Dataset, rightFile.Dataset);
        DicomDatasetComparer.WriteDifferences(differences, output, outputColor);
        return differences.Count == 0 ? ExitCode.Success : ExitCode.CompareDifferent;
    }

    private static int ExecuteRead(ReadCommand command, TextWriter output, TextWriter error, bool outputColor)
    {
        if (command.Format == OutputFormat.Json && command.BinaryFormat != BinaryFormat.Base64)
        {
            error.WriteLine($"--binary-format {FormatBinary(command.BinaryFormat)} cannot be used with --format json. DICOMweb JSON requires base64 InlineBinary.");
            return ExitCode.InvalidArguments;
        }

        if (!HasDicomExtension(command.FilePath))
        {
            error.WriteLine("Input file for read mode must have extension .dcm or .dicom.");
            return ExitCode.InvalidArguments;
        }

        if (!File.Exists(command.FilePath))
        {
            error.WriteLine($"File not found: {command.FilePath}");
            return ExitCode.InputUnavailable;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(command.FilePath);
        }
        catch (IOException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (UnauthorizedAccessException ex)
        {
            error.WriteLine($"Failed to open DICOM file: {ex.Message}");
            return ExitCode.InputUnavailable;
        }
        catch (DicomException ex)
        {
            error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
            return ExitCode.InvalidDicom;
        }

        var dataset = file.Dataset;
        var transferSyntaxName = file.FileMetaInfo?.TransferSyntax?.UID?.Name ?? "Unknown";
        if (command.Format == OutputFormat.Json)
        {
            DicomwebJsonWriter.Write(dataset, output, command.CompactJson);
            return ExitCode.Success;
        }

        DicomTextWriter.Write(dataset, transferSyntaxName, command.BinaryFormat, output, outputColor);
        return ExitCode.Success;
    }

    internal static bool HasDicomExtension(string path)
    {
        return HasExtension(path, ".dcm") || HasExtension(path, ".dicom");
    }

    private static int WriteLine(string text, TextWriter output)
    {
        output.WriteLine(text);
        return ExitCode.Success;
    }

    private static int Write(string text, TextWriter output)
    {
        output.Write(text);
        return ExitCode.Success;
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
