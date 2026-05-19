using System.CommandLine;
using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var fileArgument = new Argument<string>(
    name: "file",
    getDefaultValue: () => "0002.DCM",
    description: "Path to DICOM file");

var rootCommand = new RootCommand("Reads DICOM files and prints dataset tags")
{
    fileArgument
};

rootCommand.SetHandler((string filePath) => ProcessDicomFile(filePath), fileArgument);

return await rootCommand.InvokeAsync(args);

static int ProcessDicomFile(string filePath)
{
    if (!File.Exists(filePath))
    {
        Console.Error.WriteLine($"File not found: {filePath}");
        return 1;
    }

    DicomFile file;
    try
    {
        file = DicomFile.Open(filePath);
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"Failed to open DICOM file: {ex.Message}");
        return 1;
    }
    catch (UnauthorizedAccessException ex)
    {
        Console.Error.WriteLine($"Failed to open DICOM file: {ex.Message}");
        return 1;
    }
    catch (DicomException ex)
    {
        Console.Error.WriteLine($"Failed to parse DICOM file: {ex.Message}");
        return 1;
    }

    var dataset = file.Dataset;
    var transferSyntaxName = file.FileMetaInfo?.TransferSyntax?.UID?.Name ?? "Unknown";
    Console.WriteLine($"Transfer Syntax: {transferSyntaxName}");
    Console.WriteLine();

    foreach (var item in dataset)
    {
        var tagStr = item.ToString();
        var valueStr = item switch
        {
            DicomSequence seq => $"[{seq.Items.Count} items]",
            DicomFragmentSequence frag => $"[{frag.Fragments.Sum(b => b?.Size ?? 0)} bytes]",
            DicomElement elem => GetElementValueString(elem, dataset),
            _ => "[unknown]"
        };
        Console.WriteLine($"{tagStr} = {valueStr}");
    }

    return 0;
}

static string GetElementValueString(DicomElement elem, DicomDataset dataset)
{
    if (IsBinaryVR(elem.ValueRepresentation))
    {
        return $"[{elem.Buffer?.Size ?? 0} bytes]";
    }

    return dataset.TryGetString(elem.Tag, out var s) && !string.IsNullOrEmpty(s)
        ? s
        : "[empty]";
}

static bool IsBinaryVR(DicomVR vr)
{
    return vr == DicomVR.OB
        || vr == DicomVR.OW
        || vr == DicomVR.OD
        || vr == DicomVR.OF
        || vr == DicomVR.OL
        || vr == DicomVR.OV
        || vr == DicomVR.UN;
}
