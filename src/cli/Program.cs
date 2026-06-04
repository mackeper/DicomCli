using System.CommandLine;
using FellowOakDicom;

new DicomSetupBuilder()
    .RegisterServices(s => s.AddFellowOakDicom())
    .Build();

return await DicomCliCommands.InvokeAsync(args, Console.Out, Console.Error);
