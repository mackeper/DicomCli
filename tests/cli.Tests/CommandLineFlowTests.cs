using System.Text.Json;

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
    public async Task HelpOptionDocumentsExitCodes()
    {
        var result = await ExecuteCommandAsync("--help");

        Assert.Equal(ExitCode.Success, result.ExitCode);
        Assert.Contains("Exit codes:", result.Output);
        Assert.Contains("0  Success", result.Output);
        Assert.Contains("1  Validation failure", result.Output);
        Assert.Contains("2  Invalid arguments or options", result.Output);
        Assert.Contains("3  Input file missing or unreadable", result.Output);
        Assert.Contains("4  Invalid DICOM input", result.Output);
        Assert.Contains("5  Invalid JSON or DICOMweb JSON", result.Output);
        Assert.Contains("6  Write failure", result.Output);
        Assert.Contains("7  Compare found differences", result.Output);
        Assert.Empty(result.Error);
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

    [Fact]
    public async Task ImplicitReadWithUppercaseDicomExtensionInvokesReadFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.DICOM");
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
    public async Task ExtractOptionInvokesExtractFlow()
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
                  "32530010": { "vr": "LO", "Value": ["VARIAN"] },
                  "32531000": { "vr": "UN", "InlineBinary": "AAECAw==" }
                }
                """, TestContext.Current.CancellationToken);
            var writeResult = await ExecuteCommandAsync(jsonPath, "-o", dicomPath);
            Assert.Equal(0, writeResult.ExitCode);

            var result = await ExecuteCommandAsync(dicomPath, "--extract", "32531000:hex");

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("00010203" + Environment.NewLine, result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task OutputLongOptionWithUppercaseExtensionsInvokesWriteFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.JSON");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.DICOM");
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

    [Theory]
    [InlineData("-c")]
    [InlineData("--compare")]
    public async Task CompareOptionWithLeftAndRightInvokesCompareFlow(string compareOption)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var leftFile = Path.Combine(workDirectory.FullName, "left.dcm");
            var rightFile = Path.Combine(workDirectory.FullName, "right.dcm");
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(leftFile);
            await TestDicomFiles.WriteGoldenJsonSampleDicomAsync(rightFile);

            var result = await ExecuteCommandAsync(leftFile, compareOption, rightFile);

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("left.dcm", "-c")]
    [InlineData("-c", "right.dcm")]
    public async Task CompareOptionWithMissingArgumentsReturnsCompareParseFailure(params string[] arguments)
    {
        var result = await ExecuteCommandAsync(arguments);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Theory]
    [InlineData("--format", "json", "--format cannot be used when comparing with -c/--compare.")]
    [InlineData("--binary-format", "base64", "--binary-format cannot be used when comparing with -c/--compare.")]
    [InlineData("-o", "output.dcm", "-o/--output cannot be used when comparing with -c/--compare.")]
    public async Task CompareOptionWithIncompatibleValueOptionReturnsFailure(string optionName, string optionValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("left.dcm", "-c", "right.dcm", optionName, optionValue);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--compact", "--compact cannot be used when comparing with -c/--compare.")]
    [InlineData("--force", "--force cannot be used when comparing with -c/--compare.")]
    public async Task CompareOptionWithIncompatibleFlagReturnsFailure(string optionName, string expectedError)
    {
        var result = await ExecuteCommandAsync("left.dcm", "-c", "right.dcm", optionName);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
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
            var readResult = await ExecuteCommandAsync(dicomPath, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.Error);
            Assert.Equal(0, readResult.ExitCode);
            Assert.Empty(readResult.Error);
            using var readDocument = JsonDocument.Parse(readResult.Output);
            var root = readDocument.RootElement;
            Assert.Equal("Doe^Jane", root.GetProperty("00100010").GetProperty("Value")[0].GetProperty("Alphabetic").GetString());
            Assert.Equal("12345", root.GetProperty("00100020").GetProperty("Value")[0].GetString());
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

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("--force can only be used when writing", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--binary-format", "raw")]
    public async Task ReadWithInvalidOptionValueReturnsParseFailure(params string[] option)
    {
        var result = await ExecuteCommandAsync(["does-not-exist.dcm", .. option]);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Theory]
    [InlineData("32531000", "--extract requires <tag>:<format>.")]
    [InlineData("32531000:text", "--extract format must be base64, hex, or xml.")]
    [InlineData("3253100:xml", "--extract tag must be 8 hex characters.")]
    public async Task ExtractWithInvalidValueReturnsParseFailureBeforeFileAccess(string extractValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("missing.dcm", "--extract", extractValue);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "json", "--format cannot be used when extracting with --extract.")]
    [InlineData("--binary-format", "base64", "--binary-format cannot be used when extracting with --extract.")]
    public async Task ExtractOptionWithIncompatibleValueOptionReturnsFailure(string optionName, string optionValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("input.dcm", "--extract", "32531000:xml", optionName, optionValue);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--compact", "--compact cannot be used when extracting with --extract.")]
    [InlineData("--force", "--force cannot be used when extracting with --extract.")]
    public async Task ExtractOptionWithIncompatibleFlagReturnsFailure(string optionName, string expectedError)
    {
        var result = await ExecuteCommandAsync("input.dcm", "--extract", "32531000:xml", optionName);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task ExtractOptionWithOutputReturnsFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", "--extract", "32531000:xml");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("--extract cannot be used when writing with -o/--output.", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task ExtractOptionWithCompareReturnsFailure()
    {
        var result = await ExecuteCommandAsync("left.dcm", "-c", "right.dcm", "--extract", "32531000:xml");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains("--extract cannot be used when comparing with -c/--compare.", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task OutputOptionWithMissingOutputArgumentReturnsParseFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task RemovedCommandsReturnParseFailure()
    {
        var result = await ExecuteCommandAsync("read", "input.dcm");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task ReadWithInvalidInputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithInvalidInputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.txt", "-o", "output.dcm");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(".json", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithInvalidOutputExtensionReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json", "-o", "output.txt");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "json", "--format cannot be used when writing with -o/--output.")]
    [InlineData("--binary-format", "base64", "--binary-format cannot be used when writing with -o/--output.")]
    public async Task WriteWithReadOptionReturnsFailure(string optionName, string optionValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", optionName, optionValue);

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task WriteWithCompactOptionReturnsFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", "--compact");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
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
