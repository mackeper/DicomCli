using System.Text.Json;

namespace cli.Tests;

public sealed class ReadFlowTests
{
    [Fact]
    public async Task ReadJsonWithSampleDicomWritesDicomwebJson()
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
    public async Task ReadJsonWithGoldenSampleMatchesStableDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "base64");

            const string expectedJson = "{\"00080016\":{\"vr\":\"UI\",\"name\":\"SOP Class UID\",\"Value\":[\"1.2.840.10008.5.1.4.1.1.2\"]},\"00080018\":{\"vr\":\"UI\",\"name\":\"SOP Instance UID\",\"Value\":[\"1.2.826.0.1.3680043.10.999.1\"]},\"00080060\":{\"vr\":\"CS\",\"name\":\"Modality\",\"Value\":[\"CT\"]},\"00100010\":{\"vr\":\"PN\",\"name\":\"Patient\\u0027s Name\",\"Value\":[{\"Alphabetic\":\"Doe^Jane\"}]},\"00100020\":{\"vr\":\"LO\",\"name\":\"Patient ID\",\"Value\":[\"12345\"]},\"0020000D\":{\"vr\":\"UI\",\"name\":\"Study Instance UID\",\"Value\":[\"1.2.826.0.1.3680043.10.999.2\"]},\"0020000E\":{\"vr\":\"UI\",\"name\":\"Series Instance UID\",\"Value\":[\"1.2.826.0.1.3680043.10.999.3\"]}}";
            Assert.Equal(0, result.ExitCode);
            Assert.Equal(expectedJson + Environment.NewLine, result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ReadMissingFileReturnsFailureAndErrorMessage()
    {
        var result = ExecuteRead("does-not-exist.dcm", "text", "summary");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("File not found: does-not-exist.dcm", result.Error);
        Assert.Empty(result.Output);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("hex")]
    public async Task ReadJsonWithNonBase64BinaryFormatReturnsFailure(string binaryFormat)
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
    public async Task ReadTextWithBinaryDataUsesRequestedBinaryFormat()
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

            var defaultResult = ExecuteRead(dicomPath, "text", "summary");
            var base64Result = ExecuteRead(dicomPath, "text", "base64");
            var jsonBase64Result = ExecuteRead(dicomPath, "json", "base64");

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

    [Fact]
    public async Task ReadTextWithModestLargeBinaryUsesSummaryAndJsonUsesBase64()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            var pixelData = Enumerable.Range(0, 64 * 1024).Select(i => (byte)(i % 256)).ToArray();
            var inlineBinary = Convert.ToBase64String(pixelData);
            await File.WriteAllTextAsync(jsonPath, $$"""
                {
                  "00080016": { "vr": "UI", "Value": ["1.2.840.10008.5.1.4.1.1.2"] },
                  "00080018": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.1"] },
                  "00280010": { "vr": "US", "Value": [256] },
                  "00280011": { "vr": "US", "Value": [256] },
                  "00280100": { "vr": "US", "Value": [8] },
                  "00280101": { "vr": "US", "Value": [8] },
                  "00280102": { "vr": "US", "Value": [7] },
                  "00280103": { "vr": "US", "Value": [0] },
                  "7FE00010": { "vr": "OB", "InlineBinary": "{{inlineBinary}}" }
                }
                """, TestContext.Current.CancellationToken);
            var writeResult = ExecuteWrite(jsonPath, dicomPath);
            Assert.Equal(0, writeResult.ExitCode);

            var defaultResult = ExecuteRead(dicomPath, "text", "summary");
            var jsonResult = ExecuteRead(dicomPath, "json", "base64");

            Assert.Equal(0, defaultResult.ExitCode);
            Assert.Contains("[65536 bytes]", defaultResult.Output);
            Assert.DoesNotContain(inlineBinary, defaultResult.Output);
            Assert.Equal(0, jsonResult.ExitCode);
            using var document = JsonDocument.Parse(jsonResult.Output);
            var roundTripInlineBinary = document.RootElement
                .GetProperty("7FE00010")
                .GetProperty("InlineBinary")
                .GetString();
            Assert.Equal(pixelData, Convert.FromBase64String(roundTripInlineBinary ?? string.Empty));
            Assert.Empty(defaultResult.Error);
            Assert.Empty(jsonResult.Error);
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

        var exitCode = DicomCliApp.ExecuteWrite(inputPath, outputPath, force: false, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
