namespace Gateway.Services;

using System.Text.Json;

public class ProxyService
{
    private readonly HeartbeatService _heartbeat;
    private readonly HttpClient _httpClient;

    public ProxyService(HeartbeatService heartbeat)
    {
        _heartbeat = heartbeat;

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        };
        _httpClient = new HttpClient(handler);
    }

    public async Task Forward(HttpContext context, string serviceName)
    {
        // Check if the service is healthy
        if (!_heartbeat.IsServiceHealthy(serviceName))
        {
            context.Response.StatusCode = 503;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = $"Serviço {serviceName} indisponível" });
            return;
        }

        var serviceUrl = _heartbeat.GetServiceUrl(serviceName);
        if (string.IsNullOrEmpty(serviceUrl))
        {
            context.Response.StatusCode = 502;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = $"URL do serviço {serviceName} não configurada" });
            return;
        }

        // Build the downstream path
        var path = context.Request.Path.Value ?? "";
        var queryString = context.Request.QueryString.Value ?? "";
        var targetUrl = $"{serviceUrl}{path}{queryString}";

        // Create the forwarded request
        var requestMessage = new HttpRequestMessage
        {
            Method = new HttpMethod(context.Request.Method),
            RequestUri = new Uri(targetUrl)
        };

        // Forward body for methods that have one
        if (context.Request.ContentLength > 0 ||
            context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Method.Equals("PUT", StringComparison.OrdinalIgnoreCase) ||
            context.Request.Method.Equals("PATCH", StringComparison.OrdinalIgnoreCase))
        {
            context.Request.EnableBuffering();
            var body = new MemoryStream();
            await context.Request.Body.CopyToAsync(body);
            body.Position = 0;
            requestMessage.Content = new StreamContent(body);

            // Forward Content-Type
            if (context.Request.ContentType != null)
            {
                requestMessage.Content.Headers.ContentType =
                    System.Net.Http.Headers.MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        // Forward Authorization header
        if (context.Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            requestMessage.Headers.TryAddWithoutValidation("Authorization", authHeader.ToString());
        }

        try
        {
            var response = await _httpClient.SendAsync(requestMessage);

            context.Response.StatusCode = (int)response.StatusCode;

            // Forward content type
            if (response.Content.Headers.ContentType != null)
            {
                context.Response.ContentType = response.Content.Headers.ContentType.ToString();
            }

            var responseBody = await response.Content.ReadAsByteArrayAsync();
            await context.Response.Body.WriteAsync(responseBody);
        }
        catch (Exception ex)
        {
            context.Response.StatusCode = 502;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { error = $"Erro ao comunicar com serviço {serviceName}: {ex.Message}" });
        }
    }
}
