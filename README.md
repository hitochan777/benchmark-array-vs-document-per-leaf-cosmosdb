# CosmosDB Benchmark: Document per Device vs Array in Group

Benchmarking two approaches for managing IoT device-to-group relationships in Azure CosmosDB.

## Approaches

### Approach 1: Document per Device
Each device is stored as a separate document with a reference to its group.

```json
// Group document
{ "id": "group-1", "name": "Group 1", "path": "/root/group-1", "type": "group" }

// Device document
{ "id": "device-1-1", "groupId": "group-1", "path": "/root/group-1/device-1-1", "type": "device" }
```

### Approach 2: Array in Group
Devices are stored as an array of IDs within the group document.

```json
// Group document with embedded device IDs
{ "id": "group-1", "name": "Group 1", "path": "/root/group-1", "deviceIds": ["device-1-1", "device-1-2", ...] }
```

## Benchmark Results

Tested with **100 groups** and **100 devices per group** (10,000 total devices).

| Query | Document per Device | Array in Group | Winner |
|-------|--------------------:|---------------:|--------|
| Get all devices (5 groups, 500 devices) | 16.57 RUs | **3.43 RUs** | Array in Group |
| Get group for a device | 2.92 RUs | **2.83 RUs** | Array in Group |
| Point read device by ID | **1.00 RUs** | N/A | Document per Device |

## Key Insights

- **Array in Group is ~5x more efficient** for fetching all devices across multiple groups
- Both approaches perform similarly for finding which group a device belongs to
- **Document per Device enables point reads** (1 RU) for direct device lookups, which is not possible with the array approach

## When to Use Each Approach

| Use Case | Recommended Approach |
|----------|---------------------|
| Frequently list devices by group | Array in Group |
| Frequently lookup individual devices | Document per Device |
| Devices have additional metadata | Document per Device |
| Groups have many devices (1000+) | Document per Device (avoid large documents) |
| Minimize RU consumption for group queries | Array in Group |

## Usage

```bash
# Set connection string
export COSMOS_CONNECTION_STRING="AccountEndpoint=https://...;AccountKey=..."

# First run - seed data
dotnet run -- --seed

# Subsequent runs - use existing data
dotnet run

# Cleanup containers after benchmark
dotnet run -- --cleanup
```

### CLI Options

| Option | Default | Description |
|--------|---------|-------------|
| `-c, --connection-string` | env var | CosmosDB connection string |
| `-d, --database` | `iot-benchmark` | Database name |
| `-g, --groups` | `100` | Number of groups |
| `--devices-per-group` | `100` | Devices per group |
| `--seed` | `false` | Seed data (deletes existing containers) |
| `--cleanup` | `false` | Delete containers after benchmark |
