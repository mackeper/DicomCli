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
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", readResult.Output);
            Assert.Contains("\"Value\":[{\"Alphabetic\":\"Doe^Jane\",\"Ideographic\":\"Ideo^Name\",\"Phonetic\":\"Phone^Name\"}]}", readResult.Output);
            Assert.Contains("\"00100020\":{\"vr\":\"LO\",\"name\":", readResult.Output);
            Assert.Contains("\"Value\":[\"12345\"]}", readResult.Output);
            Assert.Contains("\"00720026\":{\"vr\":\"AT\",\"name\":", readResult.Output);
            Assert.Contains("\"Value\":[\"00100010\"]}", readResult.Output);
            Assert.Contains("\"7FE00010\":{\"vr\":\"OB\",\"name\":", readResult.Output);
            Assert.Contains("\"InlineBinary\":\"AA==\"}", readResult.Output);

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
    public async Task WriteInvalidDicomwebJsonReturnsFailureWithoutOutputFile(string json, string expectedError)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, json, TestContext.Current.CancellationToken);

            var result = ExecuteWrite(jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains(expectedError, result.Error);
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
            Assert.Contains("\"00191030\":{\"vr\":\"LO\",\"name\":", readResult.Output);
            Assert.Contains("\"Value\":[\"private-value\"]", readResult.Output);
            Assert.Equal(0, roundTripWriteResult.ExitCode);
            Assert.Empty(roundTripWriteResult.Error);
            Assert.True(File.Exists(roundTripDicomPath));
            Assert.Equal(0, roundTripReadResult.ExitCode);
            Assert.Empty(roundTripReadResult.Error);
            Assert.Contains("\"00191030\":{\"vr\":\"LO\",\"name\":", roundTripReadResult.Output);
            Assert.Contains("\"Value\":[\"private-value\"]", roundTripReadResult.Output);
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

            Assert.Equal(1, result.ExitCode);
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
            Assert.Contains("\"00100020\":{\"vr\":\"LO\",\"name\":", readResult.Output);
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

    private static FlowResult ExecuteWrite(string inputPath, string outputPath, bool force = false)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = DicomCliApp.ExecuteWrite(inputPath, outputPath, force, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
