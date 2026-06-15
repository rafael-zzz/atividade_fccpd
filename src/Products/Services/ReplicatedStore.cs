namespace Products.Services;

using Shared.Models;
using System.Text.Json;

/// <summary>
/// Replicated store managing two JSON replicas for Product data.
/// Reads use round-robin between replicas; writes go to both replicas
/// synchronously for strong consistency.
/// </summary>
public class ReplicatedStore
{
    private readonly string _replica1Path;
    private readonly string _replica2Path;
    private readonly object _lock = new();
    private long _counter;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public ReplicatedStore(string replica1Path, string replica2Path)
    {
        _replica1Path = replica1Path;
        _replica2Path = replica2Path;

        InitializeFile(replica1Path);
        InitializeFile(replica2Path);
    }

    private static void InitializeFile(string filePath)
    {
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, "[]");
    }

    /// <summary>
    /// Reads all products using round-robin between the two replicas.
    /// Even counter → replica1, odd counter → replica2.
    /// </summary>
    public List<Product> ReadAll()
    {
        lock (_lock)
        {
            var current = Interlocked.Increment(ref _counter);
            var path = (current % 2 == 0) ? _replica1Path : _replica2Path;

            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts) ?? new();
        }
    }

    /// <summary>
    /// Writes the full product list to both replicas under a lock.
    /// Only returns after both replicas are written (strong consistency).
    /// </summary>
    public void WriteAll(List<Product> items)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(items, _jsonOpts);
            File.WriteAllText(_replica1Path, json);
            File.WriteAllText(_replica2Path, json);
        }
    }

    /// <summary>
    /// Adds a product by reading the primary replica (replica1), appending
    /// the new product, and writing to both replicas under a lock.
    /// </summary>
    public void Add(Product product)
    {
        lock (_lock)
        {
            // Read from primary replica inside the lock
            var json = File.ReadAllText(_replica1Path);
            var items = JsonSerializer.Deserialize<List<Product>>(json, _jsonOpts) ?? new();

            items.Add(product);

            var updatedJson = JsonSerializer.Serialize(items, _jsonOpts);
            File.WriteAllText(_replica1Path, updatedJson);
            File.WriteAllText(_replica2Path, updatedJson);
        }
    }
}
