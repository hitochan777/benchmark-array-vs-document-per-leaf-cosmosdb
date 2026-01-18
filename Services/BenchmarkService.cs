using System.Diagnostics;
using Microsoft.Azure.Cosmos;

namespace CosmosDbBenchmark.Services;

public class BenchmarkResult
{
    public string QueryName { get; set; } = string.Empty;
    public string Approach { get; set; } = string.Empty;
    public double TotalRUs { get; set; }
    public int QueryCount { get; set; }
    public long ElapsedMs { get; set; }
    public int DocumentsReturned { get; set; }
}

public class BenchmarkService
{
    private readonly Container _approach1Container;
    private readonly Container _approach2Container;

    public BenchmarkService(Container approach1Container, Container approach2Container,
        int groupCount = 100, int devicesPerGroup = 10)
    {
        _approach1Container = approach1Container;
        _approach2Container = approach2Container;
    }

    /// <summary>
    /// Benchmark: Get all devices a user has access to
    /// Simulates a user having access to multiple groups (e.g., 5 groups)
    /// </summary>
    public async Task<List<BenchmarkResult>> BenchmarkGetAllDevicesAsync()
    {
        var results = new List<BenchmarkResult>();

        // Simulate user having access to 5 groups
        var accessibleGroups = Enumerable.Range(1, 5).Select(i => $"group-{i}").ToList();

        // Approach 1: Query devices by groupId
        var approach1Result = new BenchmarkResult
        {
            QueryName = "Get All Devices",
            Approach = "Approach 1 (Document per Device)"
        };

        var sw = Stopwatch.StartNew();
        double totalRUs1 = 0;
        int queryCount1 = 0;
        int docCount1 = 0;

        var groupsFilter = string.Join(", ", accessibleGroups.Select(g => $"'{g}'"));
        var query1 = $"SELECT c.id FROM c WHERE c.type = 'device' AND c.groupId IN ({groupsFilter})";

        var queryDef1 = new QueryDefinition(query1);
        var iterator1 = _approach1Container.GetItemQueryIterator<dynamic>(queryDef1);

        while (iterator1.HasMoreResults)
        {
            var response = await iterator1.ReadNextAsync();
            totalRUs1 += response.RequestCharge;
            queryCount1++;
            docCount1 += response.Count;
        }
        sw.Stop();

        approach1Result.TotalRUs = totalRUs1;
        approach1Result.QueryCount = queryCount1;
        approach1Result.ElapsedMs = sw.ElapsedMilliseconds;
        approach1Result.DocumentsReturned = docCount1;
        results.Add(approach1Result);

        // Approach 2: Query groups and extract deviceIds array
        var approach2Result = new BenchmarkResult
        {
            QueryName = "Get All Devices",
            Approach = "Approach 2 (Array in Group)"
        };

        sw.Restart();
        double totalRUs2 = 0;
        int queryCount2 = 0;
        int docCount2 = 0;

        var query2 = $"SELECT c.deviceIds FROM c WHERE c.id IN ({groupsFilter})";

        var queryDef2 = new QueryDefinition(query2);
        var iterator2 = _approach2Container.GetItemQueryIterator<dynamic>(queryDef2);

        while (iterator2.HasMoreResults)
        {
            var response = await iterator2.ReadNextAsync();
            totalRUs2 += response.RequestCharge;
            queryCount2++;

            foreach (var doc in response)
            {
                if (doc.deviceIds != null)
                {
                    docCount2 += ((Newtonsoft.Json.Linq.JArray)doc.deviceIds).Count;
                }
            }
        }
        sw.Stop();

        approach2Result.TotalRUs = totalRUs2;
        approach2Result.QueryCount = queryCount2;
        approach2Result.ElapsedMs = sw.ElapsedMilliseconds;
        approach2Result.DocumentsReturned = docCount2;
        results.Add(approach2Result);

        return results;
    }

    /// <summary>
    /// Benchmark: Get group a device belongs to
    /// </summary>
    public async Task<List<BenchmarkResult>> BenchmarkGetDeviceGroupAsync()
    {
        var results = new List<BenchmarkResult>();

        // Test with a specific device (device-50-5 belongs to group-50)
        var testDeviceId = "device-50-5";

        // Approach 1: Query device by ID to get groupId
        var approach1Result = new BenchmarkResult
        {
            QueryName = "Get Device Group",
            Approach = "Approach 1 (Document per Device)"
        };

        var sw = Stopwatch.StartNew();
        double totalRUs1 = 0;
        int queryCount1 = 0;
        int docCount1 = 0;

        var query1 = $"SELECT c.groupId, c.path FROM c WHERE c.type = 'device' AND c.id = '{testDeviceId}'";

        var queryDef1 = new QueryDefinition(query1);
        var iterator1 = _approach1Container.GetItemQueryIterator<dynamic>(queryDef1);

        while (iterator1.HasMoreResults)
        {
            var response = await iterator1.ReadNextAsync();
            totalRUs1 += response.RequestCharge;
            queryCount1++;
            docCount1 += response.Count;
        }
        sw.Stop();

        approach1Result.TotalRUs = totalRUs1;
        approach1Result.QueryCount = queryCount1;
        approach1Result.ElapsedMs = sw.ElapsedMilliseconds;
        approach1Result.DocumentsReturned = docCount1;
        results.Add(approach1Result);

        // Approach 2: Query groups where deviceIds array contains the device
        var approach2Result = new BenchmarkResult
        {
            QueryName = "Get Device Group",
            Approach = "Approach 2 (Array in Group)"
        };

        sw.Restart();
        double totalRUs2 = 0;
        int queryCount2 = 0;
        int docCount2 = 0;

        var query2 = $"SELECT c.id, c.name, c.path FROM c WHERE ARRAY_CONTAINS(c.deviceIds, '{testDeviceId}')";

        var queryDef2 = new QueryDefinition(query2);
        var iterator2 = _approach2Container.GetItemQueryIterator<dynamic>(queryDef2);

        while (iterator2.HasMoreResults)
        {
            var response = await iterator2.ReadNextAsync();
            totalRUs2 += response.RequestCharge;
            queryCount2++;
            docCount2 += response.Count;
        }
        sw.Stop();

        approach2Result.TotalRUs = totalRUs2;
        approach2Result.QueryCount = queryCount2;
        approach2Result.ElapsedMs = sw.ElapsedMilliseconds;
        approach2Result.DocumentsReturned = docCount2;
        results.Add(approach2Result);

        return results;
    }

    /// <summary>
    /// Benchmark: Point read - Get specific device by ID (only for Approach 1)
    /// </summary>
    public async Task<List<BenchmarkResult>> BenchmarkPointReadAsync()
    {
        var results = new List<BenchmarkResult>();

        var testDeviceId = "device-50-5";
        var partitionKey = "tenant-1"; // group-50 -> 50 % 10 + 1 = 1

        // Approach 1: Point read device document
        var approach1Result = new BenchmarkResult
        {
            QueryName = "Point Read Device",
            Approach = "Approach 1 (Document per Device)"
        };

        var sw = Stopwatch.StartNew();
        double totalRUs1 = 0;

        try
        {
            var response = await _approach1Container.ReadItemAsync<dynamic>(
                testDeviceId, new PartitionKey(partitionKey));
            totalRUs1 = response.RequestCharge;
        }
        catch (CosmosException)
        {
            // Handle not found
        }
        sw.Stop();

        approach1Result.TotalRUs = totalRUs1;
        approach1Result.QueryCount = 1;
        approach1Result.ElapsedMs = sw.ElapsedMilliseconds;
        approach1Result.DocumentsReturned = 1;
        results.Add(approach1Result);

        return results;
    }
}
