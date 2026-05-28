using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var fileArgument = new Argument<string>(
    name: "file",
    getDefaultValue: () => "0002.DCM",
    description: "Path to DICOM file");

var formatOption = new Option<string>(
    aliases: ["-f", "--format"],
    getDefaultValue: () => "text",
    description: "Output format: text or json")
    .FromAmong("text", "json");

var rootCommand = new RootCommand("Reads DICOM files and prints dataset tags")
{
    fileArgument,
    formatOption
};

rootCommand.SetHandler(context =>
{
    var filePath = context.ParseResult.GetValueForArgument(fileArgument);
    var format = context.ParseResult.GetValueForOption(formatOption) ?? "text";

    context.ExitCode = ProcessDicomFile(filePath, format);
});

return await rootCommand.InvokeAsync(args);

static int ProcessDicomFile(string filePath, string format)
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
    if (format == "json")
    {
        WriteJsonOutput(dataset, transferSyntaxName);
        return 0;
    }

    Console.WriteLine($"Transfer Syntax: {transferSyntaxName}");
    Console.WriteLine();

    WriteTextDataset(dataset, 0);

    return 0;
}

static void WriteJsonOutput(DicomDataset dataset, string transferSyntaxName)
{
    var output = new
    {
        transferSyntax = transferSyntaxName,
        tags = GetJsonDataset(dataset)
    };

    Console.WriteLine(JsonSerializer.Serialize(output));
}

static void WriteTextDataset(DicomDataset dataset, int indent)
{
    foreach (var item in dataset)
    {
        var padding = new string(' ', indent);
        var tagStr = item.ToString();
        var valueStr = GetTextValueString(item, dataset);
        Console.WriteLine($"{padding}{tagStr} = {valueStr}");

        if (item is DicomSequence sequence)
        {
            WriteTextSequence(sequence, indent + 2);
        }
    }
}

static void WriteTextSequence(DicomSequence sequence, int indent)
{
    for (var i = 0; i < sequence.Items.Count; i++)
    {
        var padding = new string(' ', indent);
        Console.WriteLine($"{padding}Item {i + 1}:");
        WriteTextDataset(sequence.Items[i], indent + 2);
    }
}

static string GetTextValueString(DicomItem item, DicomDataset dataset)
{
    return item switch
    {
        DicomSequence seq => $"[{seq.Items.Count} items]",
        DicomFragmentSequence frag => $"[{frag.Fragments.Sum(b => b?.Size ?? 0)} bytes]",
        DicomElement elem => GetElementValueString(elem, dataset),
        _ => "[unknown]"
    };
}

static IEnumerable<object> GetJsonDataset(DicomDataset dataset)
{
    return dataset.Select(item => new
    {
        group = item.Tag.Group.ToString("X4"),
        element = item.Tag.Element.ToString("X4"),
        name = item.Tag.DictionaryEntry?.Name ?? item.Tag.ToString(),
        value = GetJsonValue(item, dataset)
    });
}

static object GetJsonValue(DicomItem item, DicomDataset dataset)
{
    return item switch
    {
        DicomSequence seq => new
        {
            items = seq.Items.Select(GetJsonDataset)
        },
        DicomFragmentSequence frag => $"[{frag.Fragments.Sum(b => b?.Size ?? 0)} bytes]",
        DicomElement elem => GetElementValueString(elem, dataset),
        _ => "[unknown]"
    };
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
