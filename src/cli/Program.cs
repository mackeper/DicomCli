using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var parseResult = ArgumentParser.Parse(args);
var outputColor = AnsiColor.ShouldUseColor(Console.IsOutputRedirected, AnsiStream.Output);
var errorColor = AnsiColor.ShouldUseColor(Console.IsErrorRedirected, AnsiStream.Error);
return parseResult switch
{
    ParseSuccess parsed => CommandExecutor.Execute(parsed.Command, Console.Out, Console.Error, outputColor, errorColor),
    ParseFailure failure => CommandExecutor.ExecuteFailure(failure, Console.Error, errorColor),
    _ => throw new InvalidOperationException($"Unknown parse result type: {parseResult.GetType().Name}")
};
