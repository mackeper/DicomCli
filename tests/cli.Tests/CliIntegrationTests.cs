using System.Diagnostics;

namespace cli.Tests;

public sealed class CliIntegrationTests
{
    [Fact]
    public void BuiltExecutableUsesDicomcliName()
    {
        var repoRoot = GetRepoRoot();
        var configuration = GetBuildConfiguration();
        var executableName = OperatingSystem.IsWindows() ? "dicomcli.exe" : "dicomcli";
        var executablePath = Path.Combine(repoRoot, "src", "cli", "bin", configuration, "net10.0", executableName);

        Assert.True(File.Exists(executablePath), $"Expected built executable at '{executablePath}'.");
    }

    [Fact]
    public async Task ExecutableReadWithSampleDicomWritesJsonOutput()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-read-smoke-");
        try
        {
            var sampleFile = Path.Combine(workDirectory.FullName, "sample.dcm");
            await TestDicomFiles.WriteSampleDicomAsync(sampleFile);

            var result = await RunCliAsync(sampleFile, "--format", "json");

            Assert.Equal(0, result.ExitCode);
            Assert.Contains("\"00080016\":{\"vr\":\"UI\",\"name\":", result.StandardOutput);
            Assert.Contains("\"00100010\":{\"vr\":\"PN\",\"name\":", result.StandardOutput);
            Assert.Empty(result.StandardError);
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    [Fact]
    public async Task ExecutableWriteWithDicomwebJsonWritesReadableDicomFile()
    {
        var workDirectory = Directory.CreateTempSubdirectory("dicomcli-write-smoke-");
        try
        {
            var jsonPath = Path.Combine(workDirectory.FullName, "input.json");
            var dicomPath = Path.Combine(workDirectory.FullName, "output.dcm");
            await File.WriteAllTextAsync(jsonPath, TestDicomFiles.MinimalCtJson, TestContext.Current.CancellationToken);

            var result = await RunCliAsync(jsonPath, "-o", dicomPath);

            Assert.Equal(0, result.ExitCode);
            Assert.Empty(result.StandardError);
            Assert.True(File.Exists(dicomPath));
        }
        finally
        {
            workDirectory.Delete(recursive: true);
        }
    }

    private static async Task<CliResult> RunCliAsync(params string[] arguments)
    {
        var repoRoot = GetRepoRoot();
        var configuration = GetBuildConfiguration();
        var cliAssemblyPath = Path.Combine(repoRoot, "src", "cli", "bin", configuration, "net10.0", "dicomcli.dll");

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = GetDotnetHostPath(),
            WorkingDirectory = repoRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };
        process.StartInfo.ArgumentList.Add(cliAssemblyPath);

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

    private static string GetBuildConfiguration()
    {
        var pathParts = AppContext.BaseDirectory.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        return pathParts.Contains("Release") ? "Release" : "Debug";
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
