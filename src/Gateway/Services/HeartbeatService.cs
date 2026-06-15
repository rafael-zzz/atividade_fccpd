namespace Gateway.Services;

using System.Collections.Concurrent;
using System.Text.Json;

public class ServiceStatus
{
    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public bool IsHealthy { get; set; } = true;
    public DateTime LastCheck { get; set; } = DateTime.UtcNow;
    public int FailCount { get; set; }
}

public class HealthEvent
{
    public string Timestamp { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Service { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class HeartbeatService : BackgroundService
{
    private readonly ConcurrentDictionary<string, ServiceStatus> _services = new();
    private readonly List<HealthEvent> _events = new();
    private readonly object _eventsLock = new();
    private readonly HttpClient _httpClient;
    private readonly ILogger<HeartbeatService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(2);
    private const int MaxFailsBeforeDown = 2;

    public HeartbeatService(ILogger<HeartbeatService> logger)
    {
        _logger = logger;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler) { Timeout = RequestTimeout };

        var usersUrl = Environment.GetEnvironmentVariable("USERS_SERVICE_URL") ?? "https://localhost:5001";
        var productsUrl = Environment.GetEnvironmentVariable("PRODUCTS_SERVICE_URL") ?? "https://localhost:5002";
        var ordersUrl = Environment.GetEnvironmentVariable("ORDERS_SERVICE_URL") ?? "https://localhost:5003";

        _services["users"] = new ServiceStatus { Name = "users", Url = usersUrl };
        _services["products"] = new ServiceStatus { Name = "products", Url = productsUrl };
        _services["orders"] = new ServiceStatus { Name = "orders", Url = ordersUrl };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay to let services start
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var kvp in _services)
            {
                await CheckServiceHealth(kvp.Key, kvp.Value, stoppingToken);
            }

            await Task.Delay(CheckInterval, stoppingToken);
        }
    }

    private async Task CheckServiceHealth(string name, ServiceStatus status, CancellationToken ct)
    {
        var wasHealthy = status.IsHealthy;

        try
        {
            var response = await _httpClient.GetAsync($"{status.Url}/health", ct);
            response.EnsureSuccessStatusCode();

            // Success
            if (!wasHealthy)
            {
                var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
                Console.WriteLine($"[{timestamp}] RECUPERAÇÃO: Serviço {name} disponível");
                AddEvent(timestamp, "RECUPERAÇÃO", name, $"Serviço {name} disponível");
            }
            status.IsHealthy = true;
            status.FailCount = 0;
            status.LastCheck = DateTime.UtcNow;
        }
        catch (Exception)
        {
            status.FailCount++;
            status.LastCheck = DateTime.UtcNow;

            if (status.FailCount >= MaxFailsBeforeDown)
            {
                if (wasHealthy)
                {
                    var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
                    Console.WriteLine($"[{timestamp}] FALHA: Serviço {name} indisponível");
                    AddEvent(timestamp, "FALHA", name, $"Serviço {name} indisponível");
                }
                status.IsHealthy = false;
            }
        }
    }

    private void AddEvent(string timestamp, string type, string service, string message)
    {
        lock (_eventsLock)
        {
            _events.Add(new HealthEvent
            {
                Timestamp = timestamp,
                Type = type,
                Service = service,
                Message = message
            });

            // Keep only the last 100 events
            if (_events.Count > 100)
                _events.RemoveAt(0);
        }
    }

    public bool IsServiceHealthy(string serviceName)
    {
        return _services.TryGetValue(serviceName, out var status) && status.IsHealthy;
    }

    public string GetServiceUrl(string serviceName)
    {
        return _services.TryGetValue(serviceName, out var status) ? status.Url : string.Empty;
    }

    public object GetAllStatus()
    {
        var services = _services.Values.Select(s => new
        {
            name = s.Name,
            url = s.Url,
            isHealthy = s.IsHealthy,
            lastCheck = s.LastCheck.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            failCount = s.FailCount
        }).ToList();

        List<HealthEvent> eventsCopy;
        lock (_eventsLock)
        {
            eventsCopy = new List<HealthEvent>(_events);
        }

        return new
        {
            services,
            events = eventsCopy.Select(e => new
            {
                timestamp = e.Timestamp,
                type = e.Type,
                service = e.Service,
                message = e.Message
            }).ToList()
        };
    }
}
