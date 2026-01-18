using Spectre.Console.Cli;
using CosmosDbBenchmark.Commands;

var app = new CommandApp<BenchmarkCommand>();

app.Configure(config =>
{
    config.SetApplicationName("cosmosdb-benchmark");
    config.SetApplicationVersion("1.0.0");

    config.AddExample(["--seed", "-c", "AccountEndpoint=https://...;AccountKey=..."]);
    config.AddExample(["-c", "AccountEndpoint=https://...;AccountKey=...", "-g", "100", "--devices-per-group", "10"]);
    config.AddExample(["--cleanup"]);
});

return await app.RunAsync(args);
