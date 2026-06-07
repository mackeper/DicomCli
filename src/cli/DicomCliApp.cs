using System.Text.Json;
using FellowOakDicom;

internal static class DicomCliApp
{
    public static int ExecuteWrite(string inputPath, string outputPath, bool force, TextWriter error)
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

    public static int ExecuteRead(string filePath, string format, string binaryFormat, TextWriter output, TextWriter error)
    {
        if (string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(binaryFormat, "base64", StringComparison.OrdinalIgnoreCase))
        {
            error.WriteLine($"--binary-format {binaryFormat} cannot be used with --format json. DICOMweb JSON requires base64 InlineBinary.");
            return 1;
        }

        var outputFormat = string.Equals(format, "json", StringComparison.OrdinalIgnoreCase)
            ? OutputFormat.Json
            : OutputFormat.Text;

        var binary = (binaryFormat ?? "").ToLowerInvariant() switch
        {
            "base64" => BinaryFormat.Base64,
            "hex" => BinaryFormat.Hex,
            _ => BinaryFormat.Summary
        };

        return ExecuteReadCore(filePath, outputFormat, binary, output, error);
    }

    private static int ExecuteReadCore(string filePath, OutputFormat format, BinaryFormat binaryFormat, TextWriter output, TextWriter error)
    {
        if (!HasDicomExtension(filePath))
        {
            error.WriteLine("Input file for read mode must have extension .dcm or .dicom.");
            return 1;
        }

        if (!File.Exists(filePath))
        {
            error.WriteLine($"File not found: {filePath}");
            return 1;
        }

        DicomFile file;
        try
        {
            file = DicomFile.Open(filePath);
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
        if (format == OutputFormat.Json)
        {
            DicomwebJsonWriter.Write(dataset, output);
            return 0;
        }

        DicomTextWriter.Write(dataset, transferSyntaxName, binaryFormat, output);
        return 0;
    }

    internal static string ResolveBinaryFormatDefault(string format, string? binaryFormat)
    {
        return binaryFormat ?? (format == "json" ? "base64" : "summary");
    }

    internal static bool HasDicomExtension(string path)
    {
        return HasExtension(path, ".dcm") || HasExtension(path, ".dicom");
    }

    private static bool HasExtension(string path, string extension)
    {
        return string.Equals(Path.GetExtension(path), extension, StringComparison.OrdinalIgnoreCase);
    }
}
