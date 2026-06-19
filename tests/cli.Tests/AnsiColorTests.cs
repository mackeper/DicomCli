namespace cli.Tests;

public sealed class AnsiColorTests
{
    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("false")]
    public void ShouldUseColorReturnsFalseWhenNoColorIsNonEmpty(string noColor)
    {
        var result = AnsiColor.ShouldUseColor(
            isRedirected: false,
            noColor,
            term: "xterm-256color",
            isWindows: false,
            enableWindowsVirtualTerminal: () => true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseColorReturnsTrueWhenNoColorIsEmpty()
    {
        var result = AnsiColor.ShouldUseColor(
            isRedirected: false,
            noColor: string.Empty,
            term: "xterm-256color",
            isWindows: false,
            enableWindowsVirtualTerminal: () => false);

        Assert.True(result);
    }

    [Fact]
    public void ShouldUseColorReturnsFalseWhenOutputIsRedirected()
    {
        var result = AnsiColor.ShouldUseColor(
            isRedirected: true,
            noColor: null,
            term: "xterm-256color",
            isWindows: false,
            enableWindowsVirtualTerminal: () => true);

        Assert.False(result);
    }

    [Fact]
    public void ShouldUseColorReturnsFalseWhenTermIsDumb()
    {
        var result = AnsiColor.ShouldUseColor(
            isRedirected: false,
            noColor: null,
            term: "dumb",
            isWindows: false,
            enableWindowsVirtualTerminal: () => true);

        Assert.False(result);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ShouldUseColorOnWindowsRequiresVirtualTerminal(bool virtualTerminalEnabled)
    {
        var result = AnsiColor.ShouldUseColor(
            isRedirected: false,
            noColor: null,
            term: "xterm-256color",
            isWindows: true,
            enableWindowsVirtualTerminal: () => virtualTerminalEnabled);

        Assert.Equal(virtualTerminalEnabled, result);
    }
}
