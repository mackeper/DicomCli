internal abstract record CliCommand;

internal sealed record ReadCommand(
    string FilePath,
    OutputFormat Format,
    BinaryFormat BinaryFormat,
    bool CompactJson = false) : CliCommand;

internal sealed record ExtractCommand(
    string FilePath,
    ushort Group,
    ushort Element,
    ExtractFormat Format) : CliCommand;

internal sealed record WriteCommand(
    string InputJsonPath,
    string OutputDicomPath,
    bool Force,
    bool SkipValidation = false) : CliCommand;

internal sealed record ValidateCommand(string FilePath) : CliCommand;

internal sealed record CompareCommand(
    string LeftPath,
    string RightPath) : CliCommand;

internal sealed record HelpCommand(string Text) : CliCommand;

internal sealed record VersionCommand(string Text) : CliCommand;

internal abstract record ArgumentParseResult;

internal sealed record ParseSuccess(CliCommand Command) : ArgumentParseResult;

internal sealed record ParseFailure(int ExitCode, string ErrorText) : ArgumentParseResult;
