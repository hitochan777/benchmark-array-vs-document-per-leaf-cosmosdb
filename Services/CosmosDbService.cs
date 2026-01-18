using Microsoft.Azure.Cosmos;

namespace CosmosDbBenchmark.Services;

public class CosmosDbService : IAsyncDisposable
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;
    private Database? _database;

    public const string Approach1ContainerName = "approach1-document-per-device";
    public const string Approach2ContainerName = "approach2-array-in-group";

    public CosmosDbService(string connectionString, string databaseName)
    {
        _client = new CosmosClient(connectionString, new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        });
        _databaseName = databaseName;
    }

    public async Task InitializeAsync()
    {
        _database = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
    }

    public async Task<Container> CreateApproach1ContainerAsync()
    {
        if (_database == null) throw new InvalidOperationException("Database not initialized");

        var containerProperties = new ContainerProperties
        {
            Id = Approach1ContainerName,
            PartitionKeyPath = "/partitionKey",
            IndexingPolicy = new IndexingPolicy
            {
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/\"_etag\"/?" } }
            }
        };

        var container = await _database.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
        return container.Container;
    }

    public async Task<Container> CreateApproach2ContainerAsync()
    {
        if (_database == null) throw new InvalidOperationException("Database not initialized");

        var containerProperties = new ContainerProperties
        {
            Id = Approach2ContainerName,
            PartitionKeyPath = "/partitionKey",
            IndexingPolicy = new IndexingPolicy
            {
                Automatic = true,
                IndexingMode = IndexingMode.Consistent,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths = { new ExcludedPath { Path = "/\"_etag\"/?" } }
            }
        };

        var container = await _database.CreateContainerIfNotExistsAsync(containerProperties, throughput: 400);
        return container.Container;
    }

    public Container GetContainer(string containerName)
    {
        if (_database == null) throw new InvalidOperationException("Database not initialized");
        return _database.GetContainer(containerName);
    }

    public async Task DeleteContainersAsync()
    {
        if (_database == null) return;

        try
        {
            await _database.GetContainer(Approach1ContainerName).DeleteContainerAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container doesn't exist, that's fine
        }

        try
        {
            await _database.GetContainer(Approach2ContainerName).DeleteContainerAsync();
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            // Container doesn't exist, that's fine
        }
    }

    public async ValueTask DisposeAsync()
    {
        _client.Dispose();
        await Task.CompletedTask;
    }
}
