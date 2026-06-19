using System.Runtime.InteropServices;

internal static class AnsiColor
{
    public const string Reset = "\u001b[0m";
    public const string Red = "\u001b[31m";
    public const string Green = "\u001b[32m";
    public const string Yellow = "\u001b[33m";
    public const string Cyan = "\u001b[36m";

    public static string Colorize(string text, string color, bool enabled)
    {
        return enabled && text.Length > 0 ? color + text + Reset : text;
    }

    public static bool ShouldUseColor(bool isRedirected, AnsiStream stream)
    {
        return ShouldUseColor(
            isRedirected,
            Environment.GetEnvironmentVariable("NO_COLOR"),
            Environment.GetEnvironmentVariable("TERM"),
            OperatingSystem.IsWindows(),
            () => TryEnableWindowsVirtualTerminal(stream));
    }

    internal static bool ShouldUseColor(
        bool isRedirected,
        string? noColor,
        string? term,
        bool isWindows,
        Func<bool> enableWindowsVirtualTerminal)
    {
        if (!string.IsNullOrEmpty(noColor) || isRedirected)
        {
            return false;
        }

        if (string.Equals(term, "dumb", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !isWindows || enableWindowsVirtualTerminal();
    }

    private static bool TryEnableWindowsVirtualTerminal(AnsiStream stream)
    {
        var handle = GetStdHandle(stream == AnsiStream.Error ? StandardErrorHandle : StandardOutputHandle);
        if (handle == IntPtr.Zero || handle == new IntPtr(-1))
        {
            return false;
        }

        if (!GetConsoleMode(handle, out var mode))
        {
            return false;
        }

        return (mode & EnableVirtualTerminalProcessing) != 0
            || SetConsoleMode(handle, mode | EnableVirtualTerminalProcessing);
    }

    private const int StandardOutputHandle = -11;
    private const int StandardErrorHandle = -12;
    private const uint EnableVirtualTerminalProcessing = 0x0004;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GetStdHandle(int nStdHandle);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetConsoleMode(IntPtr hConsoleHandle, out uint lpMode);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool SetConsoleMode(IntPtr hConsoleHandle, uint dwMode);
}

internal enum AnsiStream
{
    Output,
    Error
}
