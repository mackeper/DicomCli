using System.Text.Json;

namespace cli.Tests;

public sealed class FixtureFlowTests
{
    [Fact]
    public void ReadTrackedDicomFixtureWritesExpectedDicomwebJson()
    {
        var result = ExecuteRead(TestDicomFiles.GetFixturePath("sample.dcm"), "json", "base64");

        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        AssertFixtureValues(result.Output);
    }

    [Fact]
    public void WriteTrackedDicomwebJsonFixtureWritesExpectedDicom()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-fixture-flow-");
        try
        {
            var dicomPath = Path.Combine(workDirectory.FullName, "sample.dcm");

            var writeResult = ExecuteWrite(TestDicomFiles.GetFixturePath("sample.dicomweb.json"), dicomPath);
            var readResult = ExecuteRead(dicomPath, "json", "base64");

            Assert.Equal(0, writeResult.ExitCode);
            Assert.Empty(writeResult.Error);
            Assert.Equal(0, readResult.ExitCode);
            Assert.Empty(readResult.Error);
            AssertFixtureValues(readResult.Output);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static void AssertFixtureValues(string output)
    {
        using var document = JsonDocument.Parse(output);
        var root = document.RootElement;
        Assert.Equal("1.2.840.10008.5.1.4.1.1.7", GetValueString(root, "00080016"));
        Assert.Equal("OT", GetValueString(root, "00080060"));
        Assert.Equal("SYN", GetValueString(root, "00080064"));
        Assert.Equal("DicomCli^Fixture", root.GetProperty("00100010").GetProperty("Value")[0].GetProperty("Alphabetic").GetString());
        Assert.Equal("SAMPLE", GetValueString(root, "00100020"));
        Assert.Equal("AA==", root.GetProperty("7FE00010").GetProperty("InlineBinary").GetString());
    }

    private static string? GetValueString(JsonElement root, string tag)
    {
        return root.GetProperty(tag).GetProperty("Value")[0].GetString();
    }

    private static FlowResult ExecuteRead(string filePath, string format, string binaryFormat)
    {
        TestDicomFiles.EnsureDicomSetup();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var exitCode = CommandExecutor.Execute(new ReadCommand(filePath, ParseOutputFormat(format), ParseBinaryFormat(binaryFormat)), output, error);

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

    private sealed record FlowResult(int ExitCode, string Output, string Error);
}
