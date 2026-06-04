namespace cli.Tests;

public sealed class WriteFlowTests
{
    [Fact]
    public async Task WriteDicomwebJson_WritesReadableDicomAndSupportsRoundTrip()
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
    public async Task WriteInvalidDicomwebJson_ReturnsFailureWithoutOutputFile(string json, string expectedError)
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
