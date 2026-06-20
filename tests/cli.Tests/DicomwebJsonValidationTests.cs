namespace cli.Tests;

public sealed class DicomwebJsonValidationTests
{
    public static TheoryData<string, string> InvalidDicomwebJsonCases => new()
    {
        { """{ "00100020": "12345" }""", "must be an object" },
        { """{ "0010002": { "vr": "LO", "Value": ["12345"] } }""", "Invalid DICOM tag" },
        { """{ "00100020": { "Value": ["12345"] } }""", "must contain string property 'vr'" },
        { """{ "00100020": { "vr": "LO", "Value": "12345" } }""", "must be an array" },
        { """{ "00100020": { "vr": "LO", "Value": [{ "Alphabetic": "12345" }] } }""", "only supported for PN VR" },
        { """{ "00280010": { "vr": "US", "Value": [70000] } }""", "exceeds US maximum" },
        { """{ "00280010": { "vr": "US", "Value": ["x"] } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00280106": { "vr": "SS", "Value": [-32769] } }""", "outside SS range" },
        { """{ "00200013": { "vr": "UL", "Value": [-1] } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00200013": { "vr": "SL", "Value": [2147483648] } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00181150": { "vr": "FL", "Value": ["x"] } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00181151": { "vr": "FD", "Value": [{}] } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00720026": { "vr": "AT", "Value": ["ZZZZ0010"] } }""", "Invalid DICOM tag" },
        { """{ "00081110": { "vr": "SQ", "Value": {} } }""", "must be an array" },
        { """{ "00081110": { "vr": "SQ", "Value": [1] } }""", "contains a non-object item" },
        { """{ "7FE00010": { "vr": "OB", "BulkDataURI": "https://example.invalid/pixel-data" } }""", "unsupported BulkDataURI" },
        { """{ "7FE00010": { "vr": "OB", "InlineBinary": 1 } }""", "must be a base64 string" },
        { """{ "7FE00010": { "vr": "OB", "InlineBinary": "not-base64" } }""", "Failed to parse DICOMweb JSON" },
        { """{ "00100020": { "vr": "LO", "InlineBinary": "AA==" } }""", "InlineBinary is not supported for VR LO" },
        { """{ "7FE00010": { "vr": "OW", "InlineBinary": "AA==" } }""", "multiple of 2 bytes" },
        { """{ "7FE00010": { "vr": "OF", "InlineBinary": "AA==" } }""", "multiple of 4 bytes" },
        { """{ "7FE00010": { "vr": "OD", "InlineBinary": "AA==" } }""", "multiple of 8 bytes" },
        { """{ "7FE00010": { "vr": "OL", "InlineBinary": "AA==" } }""", "multiple of 4 bytes" },
        { """{ "7FE00010": { "vr": "OV", "InlineBinary": "AA==" } }""", "multiple of 8 bytes" }
    };

    [Theory]
    [MemberData(nameof(InvalidDicomwebJsonCases))]
    public async Task WriteInvalidDicomwebJsonReturnsFailureWithoutOutputFile(string json, string expectedError)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-validation-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, json, TestContext.Current.CancellationToken);

            var result = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(ExitCode.InvalidJson, result.ExitCode);
            Assert.Contains(expectedError, result.Error);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new WriteCommand(inputPath, outputPath, Force: false), TextWriter.Null, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
