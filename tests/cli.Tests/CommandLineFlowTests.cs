namespace cli.Tests;

public sealed class CommandLineFlowTests
{
    [Fact]
    public async Task VersionOptionPrintsVersionAndExitsSuccessfully()
    {
        var result = await ExecuteCommandAsync("--version");

        Assert.Equal(0, result.ExitCode);
        Assert.Single(result.Output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries));
        Assert.Contains("DicomCli", result.Output);
        Assert.Contains("0.1.1", result.Output);
        Assert.Empty(result.Error);
    }

    [Fact]
    public async Task ImplicitReadWithJsonFormatInvokesReadFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await ExecuteCommandAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00100010\": {", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ImplicitReadWithCompactJsonFormatInvokesReadFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await ExecuteCommandAsync(sampleFile, "--format", "json", "--compact");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.Output);
            Assert.DoesNotContain(Environment.NewLine + "  ", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("sample.DCM")]
    [InlineData("sample.DICOM")]
    public async Task ImplicitReadWithUppercaseDicomExtensionInvokesReadFlow(string fileName)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, fileName);
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await ExecuteCommandAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00100010\": {", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ReadWithBinaryFormatInvokesReadFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
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
            var writeResult = await ExecuteCommandAsync(jsonPath, "-o", dicomPath);
            Assert.Equal(0, writeResult.ExitCode);

            var result = await ExecuteCommandAsync(dicomPath, "--binary-format", "base64");
            var jsonResult = await ExecuteCommandAsync(dicomPath, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("AAECAw==", result.Output);
            Assert.Empty(result.Error);
            Assert.Equal(0, jsonResult.ExitCode);
            Assert.Contains("\"InlineBinary\": \"AAECAw==\"", jsonResult.Output);
            Assert.Empty(jsonResult.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task OutputOptionWithInputAndOutputInvokesWriteFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);

            var result = await ExecuteCommandAsync(jsonPath, "-o", dicomPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Error);
            Assert.True(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("output.DCM")]
    [InlineData("output.DICOM")]
    public async Task OutputLongOptionWithUppercaseExtensionsInvokesWriteFlow(string dicomFileName)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.JSON");
            var dicomPath = Path.Combine(workDirectory.FullName, dicomFileName);
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);

            var result = await ExecuteCommandAsync(jsonPath, "--output", dicomPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Error);
            Assert.True(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task CompareCommandWithLeftAndRightInvokesCompareFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(leftFile);
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(rightFile);

            var result = await ExecuteCommandAsync("compare", leftFile, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task OutputOptionWithForceOverwritesExistingOutputFile()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(dicomPath, "existing output", TestContext.Current.CancellationToken);

            var result = await ExecuteCommandAsync(jsonPath, "-o", dicomPath, "--force");

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Error);
            Assert.True(new FileInfo(dicomPath).Length > "existing output".Length);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ForceWithoutOutputOptionReturnsFailure()
    {
        var result = await ExecuteCommandAsync("input.dcm", "--force");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--force can only be used when writing", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--binary-format", "raw")]
    public async Task ReadWithInvalidOptionValueReturnsParseFailure(params string[] option)
    {
        var result = await ExecuteCommandAsync(["does-not-exist.dcm", .. option]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task OutputOptionWithMissingOutputArgumentReturnsParseFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task RemovedCommandsReturnParseFailure(string command)
    {
        var result = await ExecuteCommandAsync(command, "input.dcm");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task ReadWithInvalidInputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithInvalidInputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.txt", "-o", "output.dcm");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".json", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithInvalidOutputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json", "-o", "output.txt");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "json", "--format cannot be used when writing with -o/--output.")]
    [InlineData("--binary-format", "base64", "--binary-format cannot be used when writing with -o/--output.")]
    public async Task WriteWithReadOptionReturnsFailure(string optionName, string optionValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", optionName, optionValue);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithCompactOptionReturnsFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", "--compact");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains("--compact cannot be used when writing with -o/--output.", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    private static Task<CommandResult> ExecuteCommandAsync(params string[] arguments)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var parseResult = ArgumentParser.Parse(arguments);
        var exitCode = parseResult switch
        {
            ParseSuccess parsed => CommandExecutor.Execute(parsed.Command, output, error),
            ParseFailure failure => CommandExecutor.ExecuteFailure(failure, error),
            _ => throw new InvalidOperationException($"Unknown parse result type: {parseResult.GetType().Name}")
        };

        return Task.FromResult(new CommandResult(exitCode, output.ToString(), error.ToString()));
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);
}
