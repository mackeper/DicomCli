using System.CommandLine;
using System.CommandLine.IO;

namespace cli.Tests;

public sealed class CommandLineFlowTests
{
    [Fact]
    public async Task ImplicitRead_WithJsonFormat_InvokesReadFlow()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await ExecuteCommandAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.Output);
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
    public async Task ImplicitRead_WithUppercaseDicomExtension_InvokesReadFlow(string fileName)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-command-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, fileName);
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await ExecuteCommandAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task Read_WithBinaryFormat_InvokesReadFlow()
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
            Assert.Contains("\"InlineBinary\":\"AAECAw==\"", jsonResult.Output);
            Assert.Empty(jsonResult.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task OutputOption_WithInputAndOutput_InvokesWriteFlow()
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
    public async Task OutputLongOption_WithUppercaseExtensions_InvokesWriteFlow(string dicomFileName)
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

    [Theory]
    [InlineData("--format", "xml")]
    [InlineData("--binary-format", "raw")]
    public async Task Read_WithInvalidOptionValue_ReturnsParseFailure(params string[] option)
    {
        var result = await ExecuteCommandAsync(["does-not-exist.dcm", .. option]);

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task OutputOption_WithMissingOutputArgument_ReturnsParseFailure()
    {
        var result = await ExecuteCommandAsync("input.json", "-o");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Theory]
    [InlineData("read")]
    [InlineData("write")]
    public async Task RemovedCommands_ReturnParseFailure(string command)
    {
        var result = await ExecuteCommandAsync(command, "input.dcm");

        Assert.NotEqual(0, result.ExitCode);
        Assert.NotEmpty(result.Error);
    }

    [Fact]
    public async Task Read_WithInvalidInputExtension_ReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task Write_WithInvalidInputExtension_ReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.txt", "-o", "output.dcm");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".json", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Fact]
    public async Task Write_WithInvalidOutputExtension_ReturnsExtensionErrorBeforeFileNotFound()
    {
        var result = await ExecuteCommandAsync("missing.json", "-o", "output.txt");

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(".dcm or .dicom", result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    [Theory]
    [InlineData("--format", "json", "--format cannot be used when writing with -o/--output.")]
    [InlineData("--binary-format", "base64", "--binary-format cannot be used when writing with -o/--output.")]
    public async Task Write_WithReadOption_ReturnsFailure(string optionName, string optionValue, string expectedError)
    {
        var result = await ExecuteCommandAsync("input.json", "-o", "output.dcm", optionName, optionValue);

        Assert.NotEqual(0, result.ExitCode);
        Assert.Contains(expectedError, result.Error);
        Assert.DoesNotContain("File not found", result.Error);
    }

    private static async Task<CommandResult> ExecuteCommandAsync(params string[] arguments)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var rootCommand = DicomCliCommands.Build(output, error);

        var exitCode = await rootCommand.InvokeAsync(arguments, new TestConsole(output, error));

        return new CommandResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record CommandResult(int ExitCode, string Output, string Error);

    private sealed class TestConsole(TextWriter output, TextWriter error) : IConsole
    {
        public IStandardStreamWriter Out { get; } = new TextWriterStreamWriter(output);

        public bool IsOutputRedirected => true;

        public IStandardStreamWriter Error { get; } = new TextWriterStreamWriter(error);

        public bool IsErrorRedirected => true;

        public bool IsInputRedirected => true;
    }

    private sealed class TextWriterStreamWriter(TextWriter writer) : IStandardStreamWriter
    {
        public void Write(string? value)
        {
            writer.Write(value);
        }
    }
}
