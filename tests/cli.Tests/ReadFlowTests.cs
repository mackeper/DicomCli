using System.Text.Json;

namespace cli.Tests;

public sealed class ReadFlowTests
{
    [Fact]
    public async Task ReadJsonWithColorEnabledWritesPlainJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "base64", colorOutput: true);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00080016\": {", result.Output);
            AssertDoesNotContainColorPrefix(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadJsonWithGoldenSampleMatchesStablePrettyDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "base64");

            const string expectedJson = """
                {
                  "00080016": {
                    "vr": "UI",
                    "name": "SOP Class UID",
                    "Value": [
                      "1.2.840.10008.5.1.4.1.1.2"
                    ]
                  },
                  "00080018": {
                    "vr": "UI",
                    "name": "SOP Instance UID",
                    "Value": [
                      "1.2.826.0.1.3680043.10.999.1"
                    ]
                  },
                  "00080060": {
                    "vr": "CS",
                    "name": "Modality",
                    "Value": [
                      "CT"
                    ]
                  },
                  "00100010": {
                    "vr": "PN",
                    "name": "Patient\u0027s Name",
                    "Value": [
                      {
                        "Alphabetic": "Doe^Jane"
                      }
                    ]
                  },
                  "00100020": {
                    "vr": "LO",
                    "name": "Patient ID",
                    "Value": [
                      "12345"
                    ]
                  },
                  "0020000D": {
                    "vr": "UI",
                    "name": "Study Instance UID",
                    "Value": [
                      "1.2.826.0.1.3680043.10.999.2"
                    ]
                  },
                  "0020000E": {
                    "vr": "UI",
                    "name": "Series Instance UID",
                    "Value": [
                      "1.2.826.0.1.3680043.10.999.3"
                    ]
                  }
                }
                """;
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
    public async Task ReadJsonWithCompactOptionWritesSingleLineDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "base64", compactJson: true);

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

        Assert.Equal(ExitCode.InputUnavailable, result.ExitCode);
        Assert.Contains("File not found: does-not-exist.dcm", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ReadMissingFileWithColorEnabledWritesAnsiError()
    {
        var result = ExecuteRead("does-not-exist.dcm", "text", "summary", colorError: true);

        Assert.Equal(ExitCode.InputUnavailable, result.ExitCode);
        Assert.Contains("\u001b[31mFile not found: does-not-exist.dcm\u001b[0m", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task ReadInvalidDicomReturnsInvalidDicomExitCode()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var invalidDicomPath = Path.Combine(workDirectory.FullName, "invalid.dcm");
            await File.WriteAllTextAsync(invalidDicomPath, "not dicom", TestContext.Current.CancellationToken);

            var result = ExecuteRead(invalidDicomPath, "text", "summary");

            Assert.Equal(ExitCode.InvalidDicom, result.ExitCode);
            Assert.Contains("Failed to parse DICOM file:", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadJsonWithNonBase64BinaryFormatReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "json", "summary");

            Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
            Assert.Contains("--binary-format summary cannot be used with --format json", result.Error);
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
            Assert.Contains("\"InlineBinary\": \"AAECAw==\"", jsonBase64Result.Output);
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
    public async Task ReadTextWithColorEnabledWritesAnsiOutput()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteRead(sampleFile, "text", "summary", colorOutput: true);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\u001b[36mTransfer Syntax\u001b[0m", result.Output);
            Assert.Empty(result.Error);
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

    private static FlowResult ExecuteRead(string filePath, string format, string binaryFormat, bool compactJson = false, bool colorOutput = false, bool colorError = false)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new ReadCommand(filePath, ParseOutputFormat(format), ParseBinaryFormat(binaryFormat), compactJson), output, error, colorOutput, colorError);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new WriteCommand(inputPath, outputPath, Force: false), TextWriter.Null, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private static OutputFormat ParseOutputFormat(string format)
    {
        return format == "json" ? OutputFormat.Json : OutputFormat.Text;
    }

    private static BinaryFormat ParseBinaryFormat(string binaryFormat)
    {
        return binaryFormat switch
        {
            "base64" => BinaryFormat.Base64,
            "hex" => BinaryFormat.Hex,
            _ => BinaryFormat.Summary
        };
    }

    private static void AssertDoesNotContainColorPrefix(string text)
    {
        Assert.DoesNotContain(AnsiColor.Red, text);
        Assert.DoesNotContain(AnsiColor.Green, text);
        Assert.DoesNotContain(AnsiColor.Yellow, text);
        Assert.DoesNotContain(AnsiColor.Cyan, text);
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
