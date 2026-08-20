namespace cli.Tests;

public sealed class ValidateFlowTests
{
    [Fact]
    public async Task ValidateDicomReturnsSuccessForReadableDicom()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteValidate(sampleFile);

            Assert.Equal(ExitCode.Success, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDicomReturnsInvalidDicomForUnreadableDicom()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var invalidDicomPath = Path.Combine(workDirectory.FullName, "invalid.dcm");
            await File.WriteAllTextAsync(invalidDicomPath, "not dicom", TestContext.Current.CancellationToken);

            var result = ExecuteValidate(invalidDicomPath);

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
    public async Task ValidateDicomReturnsValidationFailureForBestEffortDicom()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00100010": { "vr": "PN", "Value": [{ "Alphabetic": "Doe^Jane" }] }
                }
                """, TestContext.Current.CancellationToken);

            var writeResult = ExecuteWrite(jsonPath, dicomPath, skipValidation: true);
            var result = ExecuteValidate(dicomPath);

            Assert.Equal(ExitCode.Success, writeResult.ExitCode);
            Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
            Assert.Contains("DICOM validation failed:", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDicomwebJsonReturnsSuccessForWritableDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);

            var result = ExecuteValidate(jsonPath);

            Assert.Equal(ExitCode.Success, result.ExitCode);
            Assert.Empty(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDicomwebJsonReturnsInvalidJsonForMalformedJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            await File.WriteAllTextAsync(jsonPath, "{", TestContext.Current.CancellationToken);

            var result = ExecuteValidate(jsonPath);

            Assert.Equal(ExitCode.InvalidJson, result.ExitCode);
            Assert.Contains("Failed to parse JSON file:", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDicomwebJsonReturnsInvalidJsonForInvalidDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            await File.WriteAllTextAsync(jsonPath, """{ "00100020": "12345" }""", TestContext.Current.CancellationToken);

            var result = ExecuteValidate(jsonPath);

            Assert.Equal(ExitCode.InvalidJson, result.ExitCode);
            Assert.Contains("Attribute 00100020 must be an object.", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ValidateDicomwebJsonReturnsValidationFailureForIncompleteDicomwebJson()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-validate-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            await File.WriteAllTextAsync(jsonPath, """
                {
                  "00100010": { "vr": "PN", "Value": [{ "Alphabetic": "Doe^Jane" }] }
                }
                """, TestContext.Current.CancellationToken);

            var result = ExecuteValidate(jsonPath);

            Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
            Assert.Contains("DICOMweb JSON validation failed:", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public void ValidateMissingInputReturnsInputUnavailable()
    {
        var result = ExecuteValidate("does-not-exist.dcm");

        Assert.Equal(ExitCode.InputUnavailable, result.ExitCode);
        Assert.Contains("File not found: does-not-exist.dcm", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public void ValidateUnsupportedExtensionReturnsInvalidArguments()
    {
        var result = ExecuteValidate("input.txt");

        Assert.Equal(ExitCode.InvalidArguments, result.ExitCode);
        Assert.Contains(".dcm, .dicom, or .json", result.Error);
        Assert.Empty(result.Output);
    }

    private static FlowResult ExecuteValidate(string filePath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new ValidateCommand(filePath), output, error);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath, bool skipValidation)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new WriteCommand(inputPath, outputPath, false, skipValidation), output, error);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
