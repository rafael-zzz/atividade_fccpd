using Gateway.Services;

var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel with HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    var certPath = Environment.GetEnvironmentVariable("CERT_PATH") ?? "certs/devcert.pfx";
    var certPassword = Environment.GetEnvironmentVariable("CERT_PASSWORD") ?? "devpassword";

    options.ListenAnyIP(5000, listenOptions =>
    {
        listenOptions.UseHttps(certPath, certPassword);
    });
});

// Register HeartbeatService as singleton so it can be injected AND run as hosted service
builder.Services.AddSingleton<HeartbeatService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<HeartbeatService>());

// Register ProxyService
builder.Services.AddSingleton<ProxyService>();

var app = builder.Build();

// Enable static files from wwwroot/
app.UseStaticFiles();

// Proxy routes to microservices
app.Map("/users/{**path}", async (HttpContext context, ProxyService proxy) =>
{
    await proxy.Forward(context, "users");
});

app.Map("/products/{**path}", async (HttpContext context, ProxyService proxy) =>
{
    await proxy.Forward(context, "products");
});

app.Map("/orders/{**path}", async (HttpContext context, ProxyService proxy) =>
{
    await proxy.Forward(context, "orders");
});

// Dashboard
app.MapGet("/dashboard", async (HttpContext context) =>
{
    var filePath = Path.Combine(app.Environment.ContentRootPath, "wwwroot", "dashboard.html");
    if (File.Exists(filePath))
    {
        context.Response.ContentType = "text/html";
        await context.Response.SendFileAsync(filePath);
    }
    else
    {
        context.Response.StatusCode = 404;
        await context.Response.WriteAsync("Dashboard não encontrado");
    }
});

// API do dashboard — status dos serviços
app.MapGet("/api/gateway/status", (HeartbeatService hb) => Results.Ok(hb.GetAllStatus()));

// Health do próprio gateway
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();
