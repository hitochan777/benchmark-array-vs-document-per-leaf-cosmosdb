using System.Net;
using Microsoft.Azure.Cosmos;
using CosmosDbBenchmark.Models.Approach1;
using CosmosDbBenchmark.Models.Approach2;

namespace CosmosDbBenchmark.Services;

public class DataSeeder
{
    private readonly int _groupCount;
    private readonly int _devicesPerGroup;

    public DataSeeder(int groupCount = 100, int devicesPerGroup = 100)
    {
        _groupCount = groupCount;
        _devicesPerGroup = devicesPerGroup;
    }

    public async Task SeedApproach1Async(Container container)
    {
        var batchSize = 25; // Smaller batch to avoid rate limiting
        var documents = new List<object>();

        for (int g = 1; g <= _groupCount; g++)
        {
            var groupId = $"group-{g}";
            var groupPath = $"/root/group-{g}";
            var partitionKey = $"tenant-{(g % 10) + 1}";

            var group = new Group
            {
                Id = groupId,
                Name = $"Group {g}",
                Path = groupPath,
                PartitionKey = partitionKey
            };
            documents.Add(group);

            for (int d = 1; d <= _devicesPerGroup; d++)
            {
                var deviceId = $"device-{g}-{d}";
                var device = new Device
                {
                    Id = deviceId,
                    GroupId = groupId,
                    Path = $"{groupPath}/{deviceId}",
                    PartitionKey = partitionKey
                };
                documents.Add(device);
            }

            if (documents.Count >= batchSize)
            {
                await BulkInsertWithRetryAsync(container, documents);
                documents.Clear();
            }
        }

        if (documents.Count > 0)
        {
            await BulkInsertWithRetryAsync(container, documents);
        }
    }

    public async Task SeedApproach2Async(Container container)
    {
        var batchSize = 10; // Smaller batch for larger documents
        var documents = new List<GroupWithDevices>();

        for (int g = 1; g <= _groupCount; g++)
        {
            var groupId = $"group-{g}";
            var groupPath = $"/root/group-{g}";
            var partitionKey = $"tenant-{(g % 10) + 1}";

            var deviceIds = new List<string>();
            for (int d = 1; d <= _devicesPerGroup; d++)
            {
                deviceIds.Add($"device-{g}-{d}");
            }

            var group = new GroupWithDevices
            {
                Id = groupId,
                Name = $"Group {g}",
                Path = groupPath,
                DeviceIds = deviceIds,
                PartitionKey = partitionKey
            };
            documents.Add(group);

            if (documents.Count >= batchSize)
            {
                await BulkInsertWithRetryAsync(container, documents.Cast<object>().ToList());
                documents.Clear();
            }
        }

        if (documents.Count > 0)
        {
            await BulkInsertWithRetryAsync(container, documents.Cast<object>().ToList());
        }
    }

    private async Task BulkInsertWithRetryAsync(Container container, List<object> documents)
    {
        const int maxRetries = 10;
        var remaining = new List<object>(documents);

        for (int retry = 0; retry < maxRetries && remaining.Count > 0; retry++)
        {
            var failed = new List<object>();
            var tasks = new List<Task<(object doc, bool success)>>();

            foreach (var doc in remaining)
            {
                var partitionKey = GetPartitionKey(doc);
                tasks.Add(InsertWithResultAsync(container, doc, partitionKey));
            }

            var results = await Task.WhenAll(tasks);

            foreach (var (doc, success) in results)
            {
                if (!success)
                {
                    failed.Add(doc);
                }
            }

            remaining = failed;

            if (remaining.Count > 0)
            {
                // Exponential backoff
                var delay = (int)Math.Pow(2, retry) * 100;
                await Task.Delay(Math.Min(delay, 5000));
            }
        }

        if (remaining.Count > 0)
        {
            throw new Exception($"Failed to insert {remaining.Count} documents after {maxRetries} retries");
        }
    }

    private async Task<(object doc, bool success)> InsertWithResultAsync(Container container, object doc, string partitionKey)
    {
        try
        {
            await container.CreateItemAsync(doc, new PartitionKey(partitionKey));
            return (doc, true);
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.TooManyRequests)
        {
            return (doc, false);
        }
    }

    private string GetPartitionKey(object doc)
    {
        return doc switch
        {
            Group g => g.PartitionKey,
            Device d => d.PartitionKey,
            GroupWithDevices gwd => gwd.PartitionKey,
            _ => throw new ArgumentException("Unknown document type")
        };
    }
}
