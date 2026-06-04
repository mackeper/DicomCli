namespace cli.Tests;

public sealed class ReadFlowTests
{
    [Fact]
    public async Task ReadJson_WithSampleDicom_WritesDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "base64");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00080016\":{\"vr\":\"UI\",\"name\":", result.Output);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.Output);
            Assert.Contains("\"Value\":[{\"Alphabetic\":", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReadMissingFile_ReturnsFailureAndErrorMessage()
    {
        var result = ExecuteRead("does-not-exist.dcm", "text", "summary");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("File not found: does-not-exist.dcm", result.Error);
        Assert.Empty(result.Output);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("hex")]
    public async Task ReadJson_WithNonBase64BinaryFormat_ReturnsFailure(string binaryFormat)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", binaryFormat);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"--binary-format {binaryFormat} cannot be used with --format json", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadText_WithBinaryData_UsesRequestedBinaryFormat()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00080016": { "vr": "UI", "Value": ["1.2.840.10008.5.1.4.1.1.2"] },
                  "00080018": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.1"] },
                  "00280010": { "vr": "US", "Value": [1] },
                  "00280011": { "vr": "US", "Value": [4] },
                  "00280100": { "vr": "US", "Value": [8] },
                  "00280101": { "vr": "US", "Value": [8] },
                  "00280102": { "vr": "US", "Value": [7] },
                  "00280103": { "vr": "US", "Value": [0] },
                  "7FE00010": { "vr": "OB", "InlineBinary": "AAECAw==" }
                }
                """, TestContext.Current.CancellationToken);
            var writeResult = ExecuteWrite(jsonPath, dicomPath);
            Assert.Equal(0, writeResult.ExitCode);

            var defaultResult = ExecuteRead(dicomPath, "text", DicomCliApp.ResolveBinaryFormatDefault("text", null));
            var base64Result = ExecuteRead(dicomPath, "text", "base64");
            var jsonBase64Result = ExecuteRead(dicomPath, "json", DicomCliApp.ResolveBinaryFormatDefault("json", "base64"));

            Assert.Equal(0, defaultResult.ExitCode);
            Assert.Contains("[4 bytes]", defaultResult.Output);
            Assert.DoesNotContain("AAECAw==", defaultResult.Output);
            Assert.Equal(0, base64Result.ExitCode);
            Assert.Contains("AAECAw==", base64Result.Output);
            Assert.Equal(0, jsonBase64Result.ExitCode);
            Assert.Contains("\"InlineBinary\":\"AAECAw==\"", jsonBase64Result.Output);
            Assert.Empty(defaultResult.Error);
            Assert.Empty(base64Result.Error);
            Assert.Empty(jsonBase64Result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static FlowResult ExecuteRead(string filePath, string format, string binaryFormat)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = DicomCliApp.ExecuteRead(filePath, format, binaryFormat, output, error);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = DicomCliApp.ExecuteWrite(inputPath, outputPath, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
