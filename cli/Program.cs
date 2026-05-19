using FellowOakDicom;

string filePath = args.Length > 0 ? args[0] : "0002.DCM";

if (!File.Exists(filePath))
{
    Console.WriteLine($"File not found: {filePath}");
    return 1;
}

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var file = DicomFile.Open(filePath);
var dataset = file.Dataset;

Console.WriteLine($"Transfer Syntax: {file.FileMetaInfo.TransferSyntax.UID.Name}");
Console.WriteLine();

foreach (var item in dataset)
{
    var tagStr = item.ToString();
    var valueStr = item switch
    {
        DicomSequence seq => $"[{seq.Items.Count} items]",
        DicomFragmentSequence frag => $"[{frag.Fragments.Sum(b => b.Size)} bytes]",
        DicomElement elem => GetElementValueString(elem, dataset),
        _ => "[unknown]"
    };
    Console.WriteLine($"{tagStr} = {valueStr}");
}

return 0;

static string GetElementValueString(DicomElement elem, DicomDataset dataset)
{
    if (IsBinaryVR(elem.ValueRepresentation))
    {
        return $"[{elem.Buffer.Size} bytes]";
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
