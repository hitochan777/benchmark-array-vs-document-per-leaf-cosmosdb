using System.ComponentModel;
using Spectre.Console.Cli;

namespace CosmosDbBenchmark.Commands;

public class BenchmarkSettings : CommandSettings
{
    [CommandOption("-c|--connection-string <CONNECTION_STRING>")]
    [Description("CosmosDB connection string. Can also be set via COSMOS_CONNECTION_STRING env var")]
    public string? ConnectionString { get; set; }

    [CommandOption("-d|--database <DATABASE>")]
    [Description("Database name")]
    [DefaultValue("iot-benchmark")]
    public string DatabaseName { get; set; } = "iot-benchmark";

    [CommandOption("-g|--groups <COUNT>")]
    [Description("Number of groups to create")]
    [DefaultValue(100)]
    public int GroupCount { get; set; } = 100;

    [CommandOption("--devices-per-group <COUNT>")]
    [Description("Number of devices per group")]
    [DefaultValue(100)]
    public int DevicesPerGroup { get; set; } = 100;

    [CommandOption("--seed")]
    [Description("Seed data before benchmark (deletes existing containers)")]
    [DefaultValue(false)]
    public bool Seed { get; set; } = false;

    [CommandOption("--cleanup")]
    [Description("Delete containers after benchmark")]
    [DefaultValue(false)]
    public bool Cleanup { get; set; } = false;

    public string GetConnectionString()
    {
        return ConnectionString
            ?? Environment.GetEnvironmentVariable("COSMOS_CONNECTION_STRING")
            ?? throw new InvalidOperationException(
                "Connection string is required. Use --connection-string or set COSMOS_CONNECTION_STRING env var.");
    }
}
