namespace Shared.Data;

using System.Text.Json;

/// <summary>
/// Store genérico que persiste List&lt;T&gt; em arquivo JSON com lock.
/// </summary>
public class JsonStore<T> where T : class
{
    private readonly string _filePath;
    private readonly object _lock = new();
    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public JsonStore(string filePath)
    {
        _filePath = filePath;
        var dir = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        if (!File.Exists(filePath))
            File.WriteAllText(filePath, "[]");
    }

    public List<T> ReadAll()
    {
        lock (_lock)
        {
            var json = File.ReadAllText(_filePath);
            return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts) ?? new();
        }
    }

    public void WriteAll(List<T> items)
    {
        lock (_lock)
        {
            var json = JsonSerializer.Serialize(items, _jsonOpts);
            File.WriteAllText(_filePath, json);
        }
    }

    public void Add(T item)
    {
        lock (_lock)
        {
            var items = ReadAll();
            items.Add(item);
            var json = JsonSerializer.Serialize(items, _jsonOpts);
            File.WriteAllText(_filePath, json);
        }
    }
}
