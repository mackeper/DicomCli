using FellowOakDicom;

namespace cli.Tests;

internal static class TestDicomFiles
{
    private static readonly Lock SetupLock = new();
    private static bool isSetup;

    public static void EnsureDicomSetup()
    {
        lock (SetupLock)
        {
            if (isSetup)
            {
                return;
            }

            new DicomSetupBuilder()
                .RegisterServices(s => s.AddFellowOakDicom())
                .Build();
            isSetup = true;
        }
    }

    public static Task WriteSampleDicomAsync(string sampleFile)
    {
        EnsureDicomSetup();

        var dataset = new DicomDataset
        {
            { DicomTag.SOPClassUID, DicomUID.CTImageStorage },
            { DicomTag.SOPInstanceUID, DicomUID.Generate() },
            { DicomTag.Modality, "CT" },
            { DicomTag.PatientName, "Doe^Jane" },
            { DicomTag.PatientID, "12345" },
            { DicomTag.StudyInstanceUID, DicomUID.Generate() },
            { DicomTag.SeriesInstanceUID, DicomUID.Generate() }
        };

        return new DicomFile(dataset).SaveAsync(sampleFile);
    }

    public static string MinimalCtJson => """
        {
          "00080016": { "vr": "UI", "Value": ["1.2.840.10008.5.1.4.1.1.2"] },
          "00080018": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.1"] },
          "00080060": { "vr": "CS", "Value": ["CT"] },
          "00100010": { "vr": "PN", "Value": [{ "Alphabetic": "Doe^Jane", "Ideographic": "Ideo^Name", "Phonetic": "Phone^Name" }] },
          "00100020": { "vr": "LO", "Value": ["12345"] },
          "00720026": { "vr": "AT", "Value": ["00100010"] },
          "0020000D": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.2"] },
          "0020000E": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.3"] },
          "00280010": { "vr": "US", "Value": [1] },
          "00280011": { "vr": "US", "Value": [1] },
          "00280100": { "vr": "US", "Value": [8] },
          "00280101": { "vr": "US", "Value": [8] },
          "00280102": { "vr": "US", "Value": [7] },
          "00280103": { "vr": "US", "Value": [0] },
          "7FE00010": { "vr": "OB", "InlineBinary": "AA==" }
        }
        """;
}
