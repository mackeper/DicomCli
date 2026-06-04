using System.CommandLine;
using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

var rootCommand = DicomCliCommands.Build(Console.Out, Console.Error);
return await rootCommand.InvokeAsync(args);
