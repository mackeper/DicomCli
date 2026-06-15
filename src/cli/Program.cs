using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var parseResult = ArgumentParser.Parse(args);
return parseResult switch
{
    ParseSuccess parsed => CommandExecutor.Execute(parsed.Command, Console.Out, Console.Error),
    ParseFailure failure => CommandExecutor.ExecuteFailure(failure, Console.Error),
    _ => throw new InvalidOperationException($"Unknown parse result type: {parseResult.GetType().Name}")
};
