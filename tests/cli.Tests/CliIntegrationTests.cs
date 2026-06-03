using System.Diagnostics;
using FellowOakDicom;

namespace cli.Tests;

public sealed class CliIntegrationTests
{
    static CliIntegrationTests()
    {
        new DicomSetupBuilder()
            .RegisterServices(s => s.AddFellowOakDicom())
            .Build();
    }

    [Fact]
    public async Task MissingFile_ReturnsFailureAndErrorMessage()
    {
        var result = await RunCliAsync("does-not-exist.dcm");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("File not found: does-not-exist.dcm", result.StandardError);
    }

    [Fact]
    public async Task SampleDicom_WithJsonFormat_WritesJsonOutput()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-sample-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await WriteSampleDicomAsync(sampleFile);

            var result = await RunCliAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00080016\":{\"vr\":\"UI\",\"name\":", result.StandardOutput);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.StandardOutput);
            Assert.Contains("\"Value\":[{\"Alphabetic\":", result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithDicomwebJson_WritesReadableDicomFile()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
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
                """, TestContext.Current.CancellationToken);

            var writeResult = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(0, writeResult.ExitCode);
            Assert.Empty(writeResult.StandardError);
            Assert.True(File.Exists(dicomPath));

            var readResult = await RunCliAsync(dicomPath, "--format", "json");

            Assert.Equal(0, readResult.ExitCode);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", readResult.StandardOutput);
            Assert.Contains("\"Value\":[{\"Alphabetic\":\"Doe^Jane\",\"Ideographic\":\"Ideo^Name\",\"Phonetic\":\"Phone^Name\"}]}", readResult.StandardOutput);
            Assert.Contains("\"00100020\":{\"vr\":\"LO\",\"name\":", readResult.StandardOutput);
            Assert.Contains("\"Value\":[\"12345\"]}", readResult.StandardOutput);
            Assert.Contains("\"00720026\":{\"vr\":\"AT\",\"name\":", readResult.StandardOutput);
            Assert.Contains("\"Value\":[\"00100010\"]}", readResult.StandardOutput);
            Assert.Contains("\"7FE00010\":{\"vr\":\"OB\",\"name\":", readResult.StandardOutput);
            Assert.Contains("\"InlineBinary\":\"AA==\"}", readResult.StandardOutput);

            await File.WriteAllTextAsync(roundTripJsonPath, readResult.StandardOutput, TestContext.Current.CancellationToken);
            var roundTripWriteResult = await RunCliAsync("write-json", roundTripJsonPath, roundTripDicomPath);

            Assert.Equal(0, roundTripWriteResult.ExitCode);
            Assert.Empty(roundTripWriteResult.StandardError);
            Assert.True(File.Exists(roundTripDicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadText_WithBinaryData_DefaultsToBase64AndSupportsExplicitBase64()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-binary-");
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

            var writeResult = await RunCliAsync("write-json", jsonPath, dicomPath);
            Assert.Equal(0, writeResult.ExitCode);

            var defaultResult = await RunCliAsync(dicomPath);
            var base64Result = await RunCliAsync(dicomPath, "--binary-format", "base64");

            Assert.Equal(0, defaultResult.ExitCode);
            Assert.Contains("AAECAw==", defaultResult.StandardOutput);
            Assert.DoesNotContain("[4 bytes]", defaultResult.StandardOutput);
            Assert.Equal(0, base64Result.ExitCode);
            Assert.Contains("AAECAw==", base64Result.StandardOutput);
            Assert.Empty(defaultResult.StandardError);
            Assert.Empty(base64Result.StandardError);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("hex")]
    public async Task ReadJson_WithNonBase64BinaryFormat_ReturnsFailure(string binaryFormat)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-binary-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await WriteSampleDicomAsync(sampleFile);

            var result = await RunCliAsync(sampleFile, "--format", "json", "--binary-format", binaryFormat);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains($"--binary-format {binaryFormat} cannot be used with --format json", result.StandardError);
            Assert.Empty(result.StandardOutput);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithOutOfRangeUnsignedShort_ReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00280010": { "vr": "US", "Value": [70000] }
                }
                """, TestContext.Current.CancellationToken);

            var result = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("exceeds US maximum", result.StandardError);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithBulkDataUri_ReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "7FE00010": { "vr": "OB", "BulkDataURI": "https://example.invalid/pixel-data" }
                }
                """, TestContext.Current.CancellationToken);

            var result = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("unsupported BulkDataURI", result.StandardError);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithOddLengthOtherWordInlineBinary_ReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "7FE00010": { "vr": "OW", "InlineBinary": "AA==" }
                }
                """, TestContext.Current.CancellationToken);

            var result = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("multiple of 2 bytes", result.StandardError);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithNonArrayValue_ReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00100020": { "vr": "LO", "Value": "12345" }
                }
                """, TestContext.Current.CancellationToken);

            var result = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("must be an array", result.StandardError);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task WriteJson_WithObjectValueForNonPersonName_ReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-json-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00100020": { "vr": "LO", "Value": [{ "Alphabetic": "12345" }] }
                }
                """, TestContext.Current.CancellationToken);

            var result = await RunCliAsync("write-json", jsonPath, dicomPath);

            Assert.Equal(1, result.ExitCode);
            Assert.Contains("only supported for PN VR", result.StandardError);
            Assert.False(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static async Task<CliResult> RunCliAsync(params string[] arguments)
    {
        var repoRoot = GetRepoRoot();
        var projectPath = Path.Combine(repoRoot, "src", "cli", "cli.csproj");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = GetDotnetHostPath(),
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.ArgumentList.Add("run");
        process.StartInfo.ArgumentList.Add("--project");
        process.StartInfo.ArgumentList.Add(projectPath);
        process.StartInfo.ArgumentList.Add("--");

        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        process.Start();

        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        var waitForExitTask = process.WaitForExitAsync();
        var completed = await Task.WhenAny(waitForExitTask, Task.Delay(TimeSpan.FromSeconds(60)));
        if (completed != waitForExitTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI process did not exit within 60 seconds.");
        }

        return new CliResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
    }

    private static Task WriteSampleDicomAsync(string sampleFile)
    {
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

    private static string GetRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "DicomCli.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("Could not locate repository root.");
    }

    private static string GetDotnetHostPath()
    {
        var hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
        if (!string.IsNullOrWhiteSpace(hostPath))
        {
            return hostPath;
        }

        return File.Exists("/usr/bin/dotnet") ? "/usr/bin/dotnet" : "dotnet";
    }

    private sealed record CliResult(int ExitCode, string StandardOutput, string StandardError);
}
