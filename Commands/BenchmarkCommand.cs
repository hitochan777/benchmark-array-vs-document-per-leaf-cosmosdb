using Spectre.Console;
using Spectre.Console.Cli;
using CosmosDbBenchmark.Services;

namespace CosmosDbBenchmark.Commands;

public class BenchmarkCommand : AsyncCommand<BenchmarkSettings>
{
    public override async Task<int> ExecuteAsync(CommandContext context, BenchmarkSettings settings, CancellationToken cancellationToken)
    {
        AnsiConsole.Write(new Rule("[bold blue]CosmosDB Benchmark[/]").RuleStyle("blue"));
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[bold]Document per Device vs Array in Group[/]");
        AnsiConsole.WriteLine();

        // Display configuration
        var configTable = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Setting")
            .AddColumn("Value");

        configTable.AddRow("Database", settings.DatabaseName);
        configTable.AddRow("Groups", settings.GroupCount.ToString());
        configTable.AddRow("Devices per group", settings.DevicesPerGroup.ToString());
        configTable.AddRow("Total devices", (settings.GroupCount * settings.DevicesPerGroup).ToString());
        configTable.AddRow("Seed data", settings.Seed.ToString());
        configTable.AddRow("Cleanup after", settings.Cleanup.ToString());

        AnsiConsole.Write(configTable);
        AnsiConsole.WriteLine();

        string connectionString;
        try
        {
            connectionString = settings.GetConnectionString();
        }
        catch (InvalidOperationException ex)
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] {ex.Message}");
            return 1;
        }

        await using var cosmosService = new CosmosDbService(connectionString, settings.DatabaseName);

        try
        {
            await AnsiConsole.Status()
                .StartAsync("Initializing database...", async ctx =>
                {
                    await cosmosService.InitializeAsync();
                });

            if (settings.Seed)
            {
                await AnsiConsole.Status()
                    .StartAsync("Deleting existing containers...", async ctx =>
                    {
                        await cosmosService.DeleteContainersAsync();
                    });
            }

            Microsoft.Azure.Cosmos.Container approach1Container = null!;
            Microsoft.Azure.Cosmos.Container approach2Container = null!;

            await AnsiConsole.Status()
                .StartAsync("Creating containers...", async ctx =>
                {
                    approach1Container = await cosmosService.CreateApproach1ContainerAsync();
                    approach2Container = await cosmosService.CreateApproach2ContainerAsync();
                });

            if (settings.Seed)
            {
                var seeder = new DataSeeder(settings.GroupCount, settings.DevicesPerGroup);

                await AnsiConsole.Progress()
                    .StartAsync(async ctx =>
                    {
                        var task1 = ctx.AddTask("[green]Seeding Approach 1 (Document per Device)[/]");
                        var task2 = ctx.AddTask("[green]Seeding Approach 2 (Array in Group)[/]");

                        task1.IsIndeterminate = true;
                        await seeder.SeedApproach1Async(approach1Container);
                        task1.Value = 100;
                        task1.IsIndeterminate = false;

                        task2.IsIndeterminate = true;
                        await seeder.SeedApproach2Async(approach2Container);
                        task2.Value = 100;
                        task2.IsIndeterminate = false;
                    });

                AnsiConsole.MarkupLine("[grey]Waiting for indexing...[/]");
                await Task.Delay(5000);
            }

            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]Running Benchmarks[/]").RuleStyle("green"));
            AnsiConsole.WriteLine();

            var benchmarkService = new BenchmarkService(
                approach1Container, approach2Container,
                settings.GroupCount, settings.DevicesPerGroup);

            var allResults = new List<BenchmarkResult>();

            // Benchmark 1
            AnsiConsole.MarkupLine("[bold]1. Get All Devices User Has Access To[/]");
            AnsiConsole.MarkupLine("[grey]Scenario: User has access to 5 groups, retrieve all device IDs[/]");
            AnsiConsole.WriteLine();

            var results1 = await benchmarkService.BenchmarkGetAllDevicesAsync();
            allResults.AddRange(results1);
            PrintResultsTable(results1);

            // Benchmark 2
            AnsiConsole.MarkupLine("[bold]2. Get Group a Device Belongs To[/]");
            AnsiConsole.MarkupLine("[grey]Scenario: Find which group contains a specific device[/]");
            AnsiConsole.WriteLine();

            var results2 = await benchmarkService.BenchmarkGetDeviceGroupAsync();
            allResults.AddRange(results2);
            PrintResultsTable(results2);

            // Benchmark 3
            AnsiConsole.MarkupLine("[bold]3. Point Read Device by ID[/]");
            AnsiConsole.MarkupLine("[grey]Scenario: Direct lookup of a device document[/]");
            AnsiConsole.WriteLine();

            var results3 = await benchmarkService.BenchmarkPointReadAsync();
            allResults.AddRange(results3);
            PrintResultsTable(results3);
            AnsiConsole.MarkupLine("[grey]Note: Approach 2 cannot do point reads for devices (no separate document)[/]");
            AnsiConsole.WriteLine();

            // Summary
            PrintSummary(allResults);

            if (settings.Cleanup)
            {
                await AnsiConsole.Status()
                    .StartAsync("Cleaning up containers...", async ctx =>
                    {
                        await cosmosService.DeleteContainersAsync();
                    });
                AnsiConsole.MarkupLine("[green]Containers deleted.[/]");
            }
            else
            {
                AnsiConsole.MarkupLine("[grey]Containers preserved. Use --cleanup to delete them.[/]");
            }

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[bold green]Benchmark complete![/]");
            return 0;
        }
        catch (Exception ex)
        {
            AnsiConsole.WriteException(ex);
            return 1;
        }
    }

    private void PrintResultsTable(List<BenchmarkResult> results)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Approach")
            .AddColumn(new TableColumn("RUs").RightAligned())
            .AddColumn(new TableColumn("Queries").RightAligned())
            .AddColumn(new TableColumn("Docs").RightAligned())
            .AddColumn(new TableColumn("Time (ms)").RightAligned());

        foreach (var result in results)
        {
            var approachName = result.Approach.Contains("Approach 1")
                ? "[blue]Document per Device[/]"
                : "[green]Array in Group[/]";

            table.AddRow(
                approachName,
                $"[bold]{result.TotalRUs:F2}[/]",
                result.QueryCount.ToString(),
                result.DocumentsReturned.ToString(),
                result.ElapsedMs.ToString()
            );
        }

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
    }

    private void PrintSummary(List<BenchmarkResult> allResults)
    {
        AnsiConsole.Write(new Rule("[bold yellow]Summary[/]").RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var summaryTable = new Table()
            .Border(TableBorder.Double)
            .AddColumn("Query")
            .AddColumn(new TableColumn("Document per Device").Centered())
            .AddColumn(new TableColumn("Array in Group").Centered())
            .AddColumn(new TableColumn("Winner").Centered());

        var grouped = allResults.GroupBy(r => r.QueryName);

        foreach (var group in grouped)
        {
            var approach1 = group.FirstOrDefault(r => r.Approach.Contains("Approach 1"));
            var approach2 = group.FirstOrDefault(r => r.Approach.Contains("Approach 2"));

            var a1Value = approach1 != null ? $"{approach1.TotalRUs:F2} RUs" : "N/A";
            var a2Value = approach2 != null ? $"{approach2.TotalRUs:F2} RUs" : "N/A";

            string winner = "N/A";
            if (approach1 != null && approach2 != null)
            {
                if (approach1.TotalRUs < approach2.TotalRUs)
                {
                    winner = "[blue]Document per Device[/]";
                    a1Value = $"[bold green]{a1Value}[/]";
                }
                else
                {
                    winner = "[green]Array in Group[/]";
                    a2Value = $"[bold green]{a2Value}[/]";
                }
            }
            else if (approach1 != null)
            {
                winner = "[blue]Document per Device[/]";
            }

            summaryTable.AddRow(group.Key, a1Value, a2Value, winner);
        }

        AnsiConsole.Write(summaryTable);
        AnsiConsole.WriteLine();
    }
}
