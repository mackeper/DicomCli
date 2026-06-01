using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var fileArgument = new Argument<string>(
    name: "file",
    description: "Path to DICOM file");

var formatOption = new Option<string>(
    aliases: ["-f", "--format"],
    getDefaultValue: () => "text",
    description: "Output format: text or json")
    .FromAmong("text", "json");

var binaryFormatOption = new Option<string>(
    aliases: ["--binary-format"],
    getDefaultValue: () => "summary",
    description: "Binary value format: summary or hex")
    .FromAmong("summary", "hex");

var rootCommand = new RootCommand("Reads DICOM files and prints dataset tags")
{
    fileArgument,
    formatOption,
    binaryFormatOption
};

rootCommand.SetHandler(context =>
{
    var filePath = context.ParseResult.GetValueForArgument(fileArgument);
    var format = context.ParseResult.GetValueForOption(formatOption) ?? "text";
    var binaryFormat = context.ParseResult.GetValueForOption(binaryFormatOption) ?? "summary";

    context.ExitCode = ProcessDicomFile(filePath, format, binaryFormat);
});

return await rootCommand.InvokeAsync(args);

static int ProcessDicomFile(string filePath, string format, string binaryFormat)
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
        WriteJsonOutput(dataset, transferSyntaxName, binaryFormat);
        return 0;
    }

    Console.WriteLine($"Transfer Syntax: {transferSyntaxName}");
    Console.WriteLine();

    WriteTextDataset(dataset, 0, binaryFormat);

    return 0;
}

static void WriteJsonOutput(DicomDataset dataset, string transferSyntaxName, string binaryFormat)
{
    var output = new
    {
        transferSyntax = transferSyntaxName,
        tags = GetJsonDataset(dataset, binaryFormat)
    };

    Console.WriteLine(JsonSerializer.Serialize(output));
}

static void WriteTextDataset(DicomDataset dataset, int indent, string binaryFormat)
{
    foreach (var item in dataset)
    {
        var padding = new string(' ', indent);
        var tagStr = item.ToString();
        var valueStr = GetTextValueString(item, dataset, binaryFormat);
        Console.WriteLine($"{padding}{tagStr} = {valueStr}");

        if (item is DicomSequence sequence)
        {
            WriteTextSequence(sequence, indent + 2, binaryFormat);
        }
    }
}

static void WriteTextSequence(DicomSequence sequence, int indent, string binaryFormat)
{
    for (var i = 0; i < sequence.Items.Count; i++)
    {
        var padding = new string(' ', indent);
        Console.WriteLine($"{padding}Item {i + 1}:");
        WriteTextDataset(sequence.Items[i], indent + 2, binaryFormat);
    }
}

static string GetTextValueString(DicomItem item, DicomDataset dataset, string binaryFormat)
{
    return item switch
    {
        DicomSequence seq => $"[{seq.Items.Count} items]",
        DicomFragmentSequence frag => GetTextFragmentValueString(frag, binaryFormat),
        DicomElement elem => GetElementValueString(elem, dataset, binaryFormat),
        _ => "[unknown]"
    };
}

static IEnumerable<object> GetJsonDataset(DicomDataset dataset, string binaryFormat)
{
    return dataset.Select(item => new
    {
        group = item.Tag.Group.ToString("X4"),
        element = item.Tag.Element.ToString("X4"),
        name = item.Tag.DictionaryEntry?.Name ?? item.Tag.ToString(),
        value = GetJsonValue(item, dataset, binaryFormat)
    });
}

static object GetJsonValue(DicomItem item, DicomDataset dataset, string binaryFormat)
{
    return item switch
    {
        DicomSequence seq => new
        {
            items = seq.Items.Select(item => GetJsonDataset(item, binaryFormat))
        },
        DicomFragmentSequence frag => GetJsonFragmentValue(frag, binaryFormat),
        DicomElement elem => GetElementValueString(elem, dataset, binaryFormat),
        _ => "[unknown]"
    };
}

static string GetTextFragmentValueString(DicomFragmentSequence frag, string binaryFormat)
{
    return binaryFormat switch
    {
        "hex" => $"[{string.Join(", ", frag.Fragments.Select(b => Convert.ToHexString(b?.Data ?? Array.Empty<byte>())))}]",
        _ => GetBinarySummary(frag.Fragments.Sum(b => b?.Size ?? 0))
    };
}

static object GetJsonFragmentValue(DicomFragmentSequence frag, string binaryFormat)
{
    return binaryFormat switch
    {
        "hex" => frag.Fragments.Select(b => Convert.ToHexString(b?.Data ?? Array.Empty<byte>())).ToArray(),
        _ => GetBinarySummary(frag.Fragments.Sum(b => b?.Size ?? 0))
    };
}

static string GetElementValueString(DicomElement elem, DicomDataset dataset, string binaryFormat)
{
    if (IsBinaryVR(elem.ValueRepresentation))
    {
        return GetBinaryValueString(elem, binaryFormat);
    }

    return dataset.TryGetString(elem.Tag, out var s) && !string.IsNullOrEmpty(s)
        ? s
        : "[empty]";
}

static string GetBinaryValueString(DicomElement elem, string binaryFormat)
{
    return binaryFormat switch
    {
        "hex" => Convert.ToHexString(elem.Buffer?.Data ?? Array.Empty<byte>()),
        _ => GetBinarySummary(elem.Buffer?.Size ?? 0)
    };
}

static string GetBinarySummary(long size) => $"[{size} bytes]";

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
