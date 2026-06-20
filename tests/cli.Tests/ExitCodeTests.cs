namespace cli.Tests;

public sealed class ExitCodeTests
{
    [Fact]
    public void ExitCodesMatchDocumentedCliContract()
    {
        Assert.Equal(0, ExitCode.Success);
        Assert.Equal(1, ExitCode.ValidationFailure);
        Assert.Equal(2, ExitCode.InvalidArguments);
        Assert.Equal(3, ExitCode.InputUnavailable);
        Assert.Equal(4, ExitCode.InvalidDicom);
        Assert.Equal(5, ExitCode.InvalidJson);
        Assert.Equal(6, ExitCode.WriteFailure);
        Assert.Equal(7, ExitCode.CompareDifferent);
    }
}
