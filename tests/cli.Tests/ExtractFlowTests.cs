using System.Text;

namespace cli.Tests;

public sealed class ExtractFlowTests
{
    [Fact]
    public async Task ExtractXmlBinaryDataWritesFormattedXmlAndMasksSetupPhotoPicture()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            const string xml = "<ExtendedInterface xmlns:v=\"urn:varian\"><Name>Plan</Name><v:SetupPhotoPicture>AABBCCDD</v:SetupPhotoPicture></ExtendedInterface>";
            await WritePrivateBinaryDicomAsync(jsonPath, dicomPath, Encoding.UTF8.GetBytes(xml));

            var result = ExecuteExtract(dicomPath, 0x3253, 0x1000, ExtractFormat.Xml);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("<ExtendedInterface xmlns:v=\"urn:varian\">", result.Output);
            Assert.Contains("<Name>Plan</Name>", result.Output);
            Assert.Contains("<v:SetupPhotoPicture>[4 bytes]</v:SetupPhotoPicture>", result.Output);
            Assert.DoesNotContain("AABBCCDD", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExtractXmlTrimsTrailingNullPadding()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            var xmlWithPadding = Encoding.UTF8.GetBytes("<ExtendedInterface><Name>Plan</Name></ExtendedInterface>\0\0");
            await WritePrivateBinaryDicomAsync(jsonPath, dicomPath, xmlWithPadding);

            var result = ExecuteExtract(dicomPath, 0x3253, 0x1000, ExtractFormat.Xml);

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("<ExtendedInterface>", result.Output);
            Assert.Contains("<Name>Plan</Name>", result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData("base64", "AAECAw==")]
    [InlineData("hex", "00010203")]
    public async Task ExtractBinaryDataWritesRequestedFormat(string format, string expectedOutput)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await WritePrivateBinaryDicomAsync(jsonPath, dicomPath, [0, 1, 2, 3]);

            var result = ExecuteExtract(dicomPath, 0x3253, 0x1000, ParseExtractFormat(format));

            Assert.Equal(0, result.ExitCode);
            Assert.Equal(expectedOutput + Environment.NewLine, result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExtractBinaryDataWithColorEnabledWritesRawPayload()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await WritePrivateBinaryDicomAsync(jsonPath, dicomPath, [0, 1, 2, 3]);

            var result = ExecuteExtract(dicomPath, 0x3253, 0x1000, ExtractFormat.Hex, colorOutput: true);

            Assert.Equal(0, result.ExitCode);
            Assert.Equal("00010203" + Environment.NewLine, result.Output);
            AssertDoesNotContainColorPrefix(result.Output);
            Assert.Empty(result.Error);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExtractMalformedXmlReturnsValidationFailure()
    {
        var result = await ExecuteXmlExtractFailureAsync(Encoding.UTF8.GetBytes("<ExtendedInterface><Name>Plan</Name>"));

        Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
        Assert.Contains("Failed to parse tag 32531000 as XML:", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task ExtractXmlWithDtdReturnsValidationFailure()
    {
        const string xml = "<!DOCTYPE root [<!ENTITY value \"blocked\">]><root>&value;</root>";
        var result = await ExecuteXmlExtractFailureAsync(Encoding.UTF8.GetBytes(xml));

        Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
        Assert.Contains("Failed to parse tag 32531000 as XML:", result.Error);
        Assert.Empty(result.Output);
        Assert.DoesNotContain("blocked", result.Output);
    }

    [Fact]
    public async Task ExtractXmlWithExternalEntityReturnsValidationFailureWithoutResolvingEntity()
    {
        const string xml = "<!DOCTYPE root [<!ENTITY xxe SYSTEM \"file:///etc/passwd\">]><root>&xxe;</root>";
        var result = await ExecuteXmlExtractFailureAsync(Encoding.UTF8.GetBytes(xml));

        Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
        Assert.Contains("Failed to parse tag 32531000 as XML:", result.Error);
        Assert.Empty(result.Output);
        Assert.DoesNotContain("root:", result.Output);
    }

    [Fact]
    public async Task ExtractXmlWithInvalidUtf8ReturnsValidationFailure()
    {
        var result = await ExecuteXmlExtractFailureAsync([0xC3, 0x28]);

        Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
        Assert.Contains("Failed to decode tag 32531000 as UTF-8 XML:", result.Error);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task ExtractNonBinaryTagReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteExtract(sampleFile, 0x0010, 0x0010, ExtractFormat.Base64);

            Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
            Assert.Contains("Tag 00100010 has VR PN; --extract only supports binary data.", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExtractMissingTagReturnsFailure()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = ExecuteExtract(sampleFile, 0x3253, 0x1000, ExtractFormat.Base64);

            Assert.Equal(ExitCode.ValidationFailure, result.ExitCode);
            Assert.Contains("Tag 32531000 was not found.", result.Error);
            Assert.Empty(result.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static async Task<FlowResult> ExecuteXmlExtractFailureAsync(byte[] xmlData)
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-extract-flow-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await WritePrivateBinaryDicomAsync(jsonPath, dicomPath, xmlData);

            return ExecuteExtract(dicomPath, 0x3253, 0x1000, ExtractFormat.Xml);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static FlowResult ExecuteExtract(string filePath, ushort group, ushort element, ExtractFormat format, bool colorOutput = false)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new ExtractCommand(filePath, group, element, format), output, error, colorOutput);

        return new FlowResult(exitCode, output.ToString(), error.ToString());
    }

    private static FlowResult ExecuteWrite(string inputPath, string outputPath)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new WriteCommand(inputPath, outputPath, Force: false), TextWriter.Null, error);

        return new FlowResult(exitCode, string.Empty, error.ToString());
    }

    private static async Task WritePrivateBinaryDicomAsync(string jsonPath, string dicomPath, byte[] data)
    {
        var inlineBinary = Convert.ToBase64String(data);
        await File.WriteAllTextAsync(jsonPath, $$"""
            {
              "00080016": { "vr": "UI", "Value": ["1.2.840.10008.5.1.4.1.1.2"] },
              "00080018": { "vr": "UI", "Value": ["1.2.826.0.1.3680043.10.999.1"] },
              "32530010": { "vr": "LO", "Value": ["VARIAN"] },
              "32531000": { "vr": "UN", "InlineBinary": "{{inlineBinary}}" }
            }
            """, TestContext.Current.CancellationToken);

        var writeResult = ExecuteWrite(jsonPath, dicomPath);
        Assert.Equal(0, writeResult.ExitCode);
        Assert.Empty(writeResult.Error);
    }

    private static ExtractFormat ParseExtractFormat(string format)
    {
        return format == "hex" ? ExtractFormat.Hex : ExtractFormat.Base64;
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
