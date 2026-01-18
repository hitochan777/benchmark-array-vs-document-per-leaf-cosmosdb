using Newtonsoft.Json;

namespace CosmosDbBenchmark.Models.Approach1;

/// <summary>
/// Approach 1: Document per device
/// Group document - stores group metadata
/// </summary>
public class Group
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "group";

    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;
}

/// <summary>
/// Device document - stores device metadata with path to its group
/// </summary>
public class Device
{
    [JsonProperty("id")]
    public string Id { get; set; } = string.Empty;

    [JsonProperty("groupId")]
    public string GroupId { get; set; } = string.Empty;

    [JsonProperty("path")]
    public string Path { get; set; } = string.Empty;

    [JsonProperty("type")]
    public string Type { get; set; } = "device";

    [JsonProperty("partitionKey")]
    public string PartitionKey { get; set; } = string.Empty;
}
