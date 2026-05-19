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
    Console.WriteLine(item);
}

return 0;
