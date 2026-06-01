using System.Diagnostics;

namespace cli.Tests;

public sealed class CliIntegrationTests
{
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
        var repoRoot = GetRepoRoot();
        var sampleFile = Path.Combine(repoRoot, "0002.DCM");

        var result = await RunCliAsync(sampleFile, "--format", "json");

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("\"transferSyntax\"", result.StandardOutput);
        Assert.Contains("\"tags\"", result.StandardOutput);
        Assert.Empty(result.StandardError);
    }

    private static async Task<CliResult> RunCliAsync(params string[] arguments)
    {
        var repoRoot = GetRepoRoot();
        var projectPath = Path.Combine(repoRoot, "cli", "cli.csproj");

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
        var completed = await Task.WhenAny(waitForExitTask, Task.Delay(TimeSpan.FromSeconds(30)));
        if (completed != waitForExitTask)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("CLI process did not exit within 30 seconds.");
        }

        return new CliResult(
            process.ExitCode,
            await standardOutputTask,
            await standardErrorTask);
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
