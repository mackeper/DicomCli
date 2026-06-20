using System.Text.Json;

namespace cli.Tests;

public sealed class WriteFlowTests
{
    [Fact]
    public async Task WriteDicomwebJsonWritesReadableDicomAndSupportsRoundTrip()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            var roundTripJsonPath = Path.Combine(workDirectory.FullName, "roundtrip.json");
            var roundTripDicomPath = Path.Combine(workDirectory.FullName, "roundtrip.dcm");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);

            var writeResult = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(0, writeResult.ExitCode);
            Assert.Empty(writeResult.Error);
            Assert.True(File.Exists(dicomPath));

            var readResult = ExecuteRead(dicomPath, "json", "base64");

            Assert.Equal(0, readResult.ExitCode);
            using var readDocument = JsonDocument.Parse(readResult.Output);
            var root = readDocument.RootElement;
            var personName = root.GetProperty("00100010").GetProperty("Value")[0];
            Assert.Equal("Doe^Jane", personName.GetProperty("Alphabetic").GetString());
            Assert.Equal("Ideo^Name", personName.GetProperty("Ideographic").GetString());
            Assert.Equal("Phone^Name", personName.GetProperty("Phonetic").GetString());
            Assert.Equal("12345", root.GetProperty("00100020").GetProperty("Value")[0].GetString());
            Assert.Equal("00100010", root.GetProperty("00720026").GetProperty("Value")[0].GetString());
            Assert.Equal("AA==", root.GetProperty("7FE00010").GetProperty("InlineBinary").GetString());

            await File.WriteAllTextAsync(roundTripJsonPath, readResult.Output, TestContext.Current.CancellationToken);
            var roundTripWriteResult = ExecuteWrite(roundTripJsonPath, roundTripDicomPath);

            Assert.Equal(0, roundTripWriteResult.ExitCode);
            Assert.Empty(roundTripWriteResult.Error);
            Assert.True(File.Exists(roundTripDicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("{ \"00280010\": { \"vr\": \"US\", \"Value\": [70000] } }", "exceeds US maximum")]
    [InlineData("{ \"7FE00010\": { \"vr\": \"OB\", \"BulkDataURI\": \"https://example.invalid/pixel-data\" } }", "unsupported BulkDataURI")]
    [InlineData("{ \"7FE00010\": { \"vr\": \"OW\", \"InlineBinary\": \"AA==\" } }", "multiple of 2 bytes")]
    [InlineData("{ \"00100020\": { \"vr\": \"LO\", \"Value\": \"12345\" } }", "must be an array")]
    [InlineData("{ \"00100020\": { \"vr\": \"LO\", \"Value\": [{ \"Alphabetic\": \"12345\" }] } }", "only supported for PN VR")]
    [InlineData("{ \"00280010\": { \"vr\": \"US\", \"Value\": [\"x\"] } }", "Failed to parse DICOMweb JSON")]
    [InlineData("{ \"00081110\": { \"vr\": \"SQ\", \"Value\": {} } }", "must be an array")]
    public async Task WriteInvalidDicomwebJsonReturnsFailureWithoutOutputFile(string json, string expectedError)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
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

    [Fact]
    public async Task WriteInvalidJsonSyntaxReturnsInvalidJsonExitCode()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, "{", TestContext.Current.CancellationToken);

            var result = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(ExitCode.InvalidJson, result.ExitCode);
            Assert.Contains("Failed to parse JSON file:", result.Error);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJsonRootArrayReturnsInvalidJsonExitCode()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, "[]", TestContext.Current.CancellationToken);

            var result = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(ExitCode.InvalidJson, result.ExitCode);
            Assert.Contains("DICOMweb JSON root must be an object.", result.Error);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteDicomwebJsonWithUnknownPrivateTagUsesPrivateCreatorVr()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            var roundTripJsonPath = Path.Combine(workDirectory.FullName, "roundtrip.json");
            var roundTripDicomPath = Path.Combine(workDirectory.FullName, "roundtrip.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00080016": { "vr": "UI", "Value": ["1.2.840.10008.5.1.4.1.1.2"] },
                  "00080018": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.1"] },
                  "00191030": { "vr": "LO", "Value": ["private-value"] },
                  "00190010": { "vr": "LO", "Value": ["UNKNOWN_CREATOR"] }
                }
                """, TestContext.Current.CancellationToken);

            var writeResult = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(0, writeResult.ExitCode);
            Assert.Empty(writeResult.Error);

            var readResult = ExecuteRead(dicomPath, "json", "base64");
            await File.WriteAllTextAsync(roundTripJsonPath, readResult.Output, TestContext.Current.CancellationToken);
            var roundTripWriteResult = ExecuteWrite(roundTripJsonPath, roundTripDicomPath);
            var roundTripReadResult = ExecuteRead(roundTripDicomPath, "json", "base64");

            Assert.Equal(0, readResult.ExitCode);
            Assert.Empty(readResult.Error);
            using var readDocument = JsonDocument.Parse(readResult.Output);
            Assert.Equal("private-value", readDocument.RootElement.GetProperty("00191030").GetProperty("Value")[0].GetString());
            Assert.Equal(0, roundTripWriteResult.ExitCode);
            Assert.Empty(roundTripWriteResult.Error);
            Assert.True(File.Exists(roundTripDicomPath));
            Assert.Equal(0, roundTripReadResult.ExitCode);
            Assert.Empty(roundTripReadResult.Error);
            using var roundTripDocument = JsonDocument.Parse(roundTripReadResult.Output);
            Assert.Equal("private-value", roundTripDocument.RootElement.GetProperty("00191030").GetProperty("Value")[0].GetString());
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteDicomwebJsonWithoutForceDoesNotOverwriteExistingOutputFile()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            const string originalOutput = "existing output";
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(dicomPath, originalOutput, TestContext.Current.CancellationToken);

            var result = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(ExitCode.WriteFailure, result.ExitCode);
            Assert.Contains("Use --force to overwrite", result.Error);
            Assert.Equal(originalOutput, await File.ReadAllTextAsync(dicomPath, TestContext.Current.CancellationToken));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteDicomwebJsonWithForceOverwritesExistingOutputFile()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(dicomPath, "existing output", TestContext.Current.CancellationToken);

            var writeResult = ExecuteWrite(jsonPath, dicomPath, force: true);
            var readResult = ExecuteRead(dicomPath, "json", "base64");

            Assert.Equal(0, writeResult.ExitCode);
            Assert.Empty(writeResult.Error);
            Assert.Equal(0, readResult.ExitCode);
            Assert.Empty(readResult.Error);
            using var readDocument = JsonDocument.Parse(readResult.Output);
            Assert.True(readDocument.RootElement.TryGetProperty("00100020", out _));
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

        var exitCode = CommandExecutor.Execute(new ReadCommand(filePath, ParseOutputFormat(format), ParseBinaryFormat(binaryFormat)), output, error);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath, bool force = false)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new WriteCommand(inputPath, outputPath, force), TextWriter.Null, error);

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

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
