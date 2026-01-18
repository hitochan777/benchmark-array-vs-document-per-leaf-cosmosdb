using Newtonsoft.Json;

namespace CosmosDbBenchmark.Models.Approach2;

/// <summary>
/// Approach 2: Array in group document
/// Group document with embedded device IDs array
/// </summary>
public class GroupWithDevices
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("deviceIds")]
    public List<string> DeviceIds { get; set; } = new();

    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;
}
